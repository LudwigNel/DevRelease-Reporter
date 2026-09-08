using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DevReleaseReporter.Core.Models;

namespace DevReleaseReporter.Infrastructure.AzureDevOps;

public sealed class AzureDevOpsReleaseSource(HttpClient httpClient) : IAzureDevOpsReleaseSource
{
    private const string ApiVersion = "7.1";
    private const int WorkItemBatchSize = 200;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ReleaseDataSet> GetReleaseDataAsync(
        AzureDevOpsConnectionOptions connectionOptions,
        AzureDevOpsReleaseQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionOptions);
        ArgumentNullException.ThrowIfNull(query);

        ValidateConnection(connectionOptions);
        ValidateQuery(query);

        ReportProgress(query, "Fetching commits...");
        var commits = await GetCommitsAsync(connectionOptions, query, cancellationToken);

        ReportProgress(query, "Fetching pull requests...");
        var pullRequests = await GetPullRequestsAsync(connectionOptions, query, cancellationToken);

        var pullRequestCommitMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var enrichedPullRequests = new List<PullRequest>(pullRequests.Count);

        for (var index = 0; index < pullRequests.Count; index++)
        {
            var pullRequest = pullRequests[index];
            if (index == 0 || index == pullRequests.Count - 1 || (index + 1) % 5 == 0)
            {
                ReportProgress(query, $"Enriching pull requests ({index + 1}/{pullRequests.Count})...");
            }

            var commitIds = await GetPullRequestCommitIdsAsync(connectionOptions, pullRequest.Id, query.PageSize, cancellationToken);
            foreach (var commitId in commitIds)
            {
                pullRequestCommitMap[commitId] = pullRequest.Id;
            }

            var workItems = query.IncludeWorkItems
                ? await GetPullRequestWorkItemsAsync(connectionOptions, pullRequest.Id, cancellationToken)
                : Array.Empty<WorkItem>();

            enrichedPullRequests.Add(pullRequest with
            {
                CommitIds = commitIds,
                WorkItems = workItems,
            });
        }

        var enrichedCommits = commits
            .Select(commit => pullRequestCommitMap.TryGetValue(commit.Id, out var pullRequestId)
                ? commit with { PullRequestId = pullRequestId }
                : commit)
            .ToArray();

        if (query.IncludeWorkItems)
        {
            ReportProgress(query, "Fetching standalone linked work items...");
        }

        var standaloneWorkItems = query.IncludeWorkItems
            ? await GetStandaloneCommitWorkItemsAsync(connectionOptions, enrichedCommits, pullRequestCommitMap, cancellationToken)
            : Array.Empty<WorkItem>();

        return new ReleaseDataSet
        {
            Commits = enrichedCommits,
            PullRequests = enrichedPullRequests,
            WorkItems = standaloneWorkItems,
        };
    }

    private static void ReportProgress(AzureDevOpsReleaseQuery query, string message) =>
        query.Progress?.Report(message);

    private async Task<IReadOnlyList<Commit>> GetCommitsAsync(
        AzureDevOpsConnectionOptions connectionOptions,
        AzureDevOpsReleaseQuery query,
        CancellationToken cancellationToken)
    {
        var commits = new List<Commit>();
        var skip = 0;

        while (true)
        {
            var parameters = new Dictionary<string, string?>
            {
                ["searchCriteria.fromDate"] = query.FromDate.ToString("O"),
                ["searchCriteria.toDate"] = query.ToDate.ToString("O"),
                ["searchCriteria.$skip"] = skip.ToString(),
                ["searchCriteria.$top"] = query.PageSize.ToString(),
            };

            var branchName = NormalizeBranchName(query.BranchName);
            if (branchName is not null)
            {
                // Unlike searchCriteria.targetRefName on the pull requests endpoint, this
                // endpoint's itemVersion.version expects a plain branch name, not a full ref.
                parameters["searchCriteria.itemVersion.versionType"] = "branch";
                parameters["searchCriteria.itemVersion.version"] = StripRefsHeadsPrefix(branchName);
            }

            var response = await SendForJsonAsync<AzureDevOpsCollectionResponse<GitCommitDto>>(
                connectionOptions,
                HttpMethod.Get,
                BuildRepositoryUri(connectionOptions, "commits", parameters),
                cancellationToken: cancellationToken);

            var batch = response.Value
                .Select(commit => MapCommit(commit, branchName))
                .ToArray();

            commits.AddRange(batch);

            if (batch.Length < query.PageSize)
            {
                break;
            }

            skip += batch.Length;
        }

        return commits;
    }

    private async Task<IReadOnlyList<PullRequest>> GetPullRequestsAsync(
        AzureDevOpsConnectionOptions connectionOptions,
        AzureDevOpsReleaseQuery query,
        CancellationToken cancellationToken)
    {
        var pullRequests = new List<PullRequest>();
        string? continuationToken = null;

        do
        {
            var parameters = new Dictionary<string, string?>
            {
                ["searchCriteria.status"] = "completed",
                ["searchCriteria.minTime"] = query.FromDate.ToString("O"),
                ["searchCriteria.maxTime"] = query.ToDate.ToString("O"),
                ["searchCriteria.queryTimeRangeType"] = "closed",
                ["$top"] = query.PageSize.ToString(),
                ["continuationToken"] = continuationToken,
            };

            var branchName = NormalizeBranchName(query.BranchName);
            if (branchName is not null)
            {
                parameters["searchCriteria.targetRefName"] = branchName;
            }

            using var response = await SendAsync(
                connectionOptions,
                HttpMethod.Get,
                BuildRepositoryUri(connectionOptions, "pullrequests", parameters),
                cancellationToken);

            var payload = await DeserializeAsync<AzureDevOpsCollectionResponse<GitPullRequestDto>>(response, cancellationToken)
                ?? throw new AzureDevOpsClientException("Azure DevOps returned an empty pull request payload.");

            pullRequests.AddRange(payload.Value.Select(MapPullRequest));
            continuationToken = TryGetContinuationToken(response);
        }
        while (!string.IsNullOrWhiteSpace(continuationToken));

        return pullRequests;
    }

    private async Task<IReadOnlyList<string>> GetPullRequestCommitIdsAsync(
        AzureDevOpsConnectionOptions connectionOptions,
        int pullRequestId,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var commitIds = new List<string>();
        var skip = 0;

        while (true)
        {
            var response = await SendForJsonAsync<AzureDevOpsCollectionResponse<GitCommitDto>>(
                connectionOptions,
                HttpMethod.Get,
                BuildRepositoryUri(
                    connectionOptions,
                    $"pullRequests/{pullRequestId}/commits",
                    new Dictionary<string, string?>
                    {
                        ["$top"] = pageSize.ToString(),
                        ["$skip"] = skip.ToString(),
                    }),
                cancellationToken: cancellationToken);

            var batch = response.Value
                .Select(static commit => commit.CommitId)
                .Where(static commitId => !string.IsNullOrWhiteSpace(commitId))
                .ToArray();

            commitIds.AddRange(batch);

            if (batch.Length < pageSize)
            {
                break;
            }

            skip += batch.Length;
        }

        return commitIds
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<IReadOnlyList<WorkItem>> GetPullRequestWorkItemsAsync(
        AzureDevOpsConnectionOptions connectionOptions,
        int pullRequestId,
        CancellationToken cancellationToken)
    {
        var workItemRefs = await SendForJsonAsync<AzureDevOpsCollectionResponse<WorkItemReferenceDto>>(
            connectionOptions,
            HttpMethod.Get,
            BuildRepositoryUri(connectionOptions, $"pullRequests/{pullRequestId}/workitems"),
            cancellationToken: cancellationToken);

        return await GetWorkItemsByIdsAsync(
            connectionOptions,
            workItemRefs.Value.Select(static workItem => workItem.Id),
            cancellationToken);
    }

    private async Task<IReadOnlyList<WorkItem>> GetStandaloneCommitWorkItemsAsync(
        AzureDevOpsConnectionOptions connectionOptions,
        IReadOnlyList<Commit> commits,
        IReadOnlyDictionary<string, int> pullRequestCommitMap,
        CancellationToken cancellationToken)
    {
        var workItemCommitMap = new Dictionary<int, HashSet<string>>();

        foreach (var commit in commits)
        {
            if (pullRequestCommitMap.ContainsKey(commit.Id))
            {
                continue;
            }

            var commitDetail = await SendForJsonAsync<GitCommitDto>(
                connectionOptions,
                HttpMethod.Get,
                BuildRepositoryUri(connectionOptions, $"commits/{commit.Id}"),
                cancellationToken: cancellationToken);

            var commitWorkItems = commitDetail.WorkItems ?? Array.Empty<WorkItemReferenceDto>();

            foreach (var workItemId in commitWorkItems.Select(static workItem => workItem.Id))
            {
                if (!workItemCommitMap.TryGetValue(workItemId, out var relatedCommitIds))
                {
                    relatedCommitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    workItemCommitMap[workItemId] = relatedCommitIds;
                }

                relatedCommitIds.Add(commit.Id);
            }
        }

        return await GetWorkItemsByIdsAsync(
            connectionOptions,
            workItemCommitMap.ToDictionary(
                static pair => pair.Key,
                static pair => (IReadOnlyCollection<string>)pair.Value),
            cancellationToken);
    }

    private async Task<IReadOnlyList<WorkItem>> GetWorkItemsByIdsAsync(
        AzureDevOpsConnectionOptions connectionOptions,
        IEnumerable<int> ids,
        CancellationToken cancellationToken)
    {
        var distinctIds = ids
            .Where(static id => id > 0)
            .Distinct()
            .ToArray();

        return await GetWorkItemsByIdsAsync(
            connectionOptions,
            distinctIds.ToDictionary(static id => id, static _ => (IReadOnlyCollection<string>)Array.Empty<string>()),
            cancellationToken);
    }

    private async Task<IReadOnlyList<WorkItem>> GetWorkItemsByIdsAsync(
        AzureDevOpsConnectionOptions connectionOptions,
        IReadOnlyDictionary<int, IReadOnlyCollection<string>> workItemCommitMap,
        CancellationToken cancellationToken)
    {
        var distinctIds = workItemCommitMap.Keys
            .Where(static id => id > 0)
            .ToArray();

        if (distinctIds.Length == 0)
        {
            return Array.Empty<WorkItem>();
        }

        var workItems = new List<WorkItem>(distinctIds.Length);

        foreach (var batch in distinctIds.Chunk(WorkItemBatchSize))
        {
            var request = new WorkItemBatchRequestDto
            {
                Ids = batch,
                Fields =
                [
                    "System.Id",
                    "System.Title",
                    "System.Description",
                    "System.WorkItemType",
                    "System.State",
                ],
            };

            var response = await SendForJsonAsync<WorkItemBatchResponseDto>(
                connectionOptions,
                HttpMethod.Post,
                BuildWorkItemBatchUri(connectionOptions),
                JsonContent.Create(request, options: JsonOptions),
                cancellationToken);

            workItems.AddRange(response.Value.Select(workItem =>
                MapWorkItem(
                    workItem,
                    workItemCommitMap.TryGetValue(workItem.Id, out var relatedCommitIds)
                        ? relatedCommitIds
                        : Array.Empty<string>())));
        }

        return workItems
            .GroupBy(static workItem => workItem.Id)
            .Select(static group => group.First())
            .ToArray();
    }

    private async Task<T> SendForJsonAsync<T>(
        AzureDevOpsConnectionOptions connectionOptions,
        HttpMethod method,
        Uri uri,
        HttpContent? content = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(connectionOptions, method, uri, cancellationToken, content);
        return await DeserializeAsync<T>(response, cancellationToken)
            ?? throw new AzureDevOpsClientException($"Azure DevOps returned an empty payload for {uri}.");
    }

    private async Task<HttpResponseMessage> SendAsync(
        AzureDevOpsConnectionOptions connectionOptions,
        HttpMethod method,
        Uri uri,
        CancellationToken cancellationToken,
        HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = CreateAuthorizationHeader(connectionOptions.PersonalAccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = content;

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        response.Dispose();

        throw new AzureDevOpsClientException(
            $"Azure DevOps request to '{uri}' failed with status code {(int)response.StatusCode} ({response.ReasonPhrase}). Response: {responseBody}");
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);

    private static Commit MapCommit(GitCommitDto commit, string? branchName) =>
        new()
        {
            Id = commit.CommitId,
            Message = string.IsNullOrWhiteSpace(commit.Comment) ? commit.CommitId : commit.Comment,
            AuthorName = commit.Author?.Name,
            AuthorEmail = commit.Author?.Email,
            CommittedAt = commit.Author?.Date,
            BranchName = branchName,
            Url = commit.RemoteUrl ?? commit.Url,
        };

    private static PullRequest MapPullRequest(GitPullRequestDto pullRequest) =>
        new()
        {
            Id = pullRequest.PullRequestId,
            Title = string.IsNullOrWhiteSpace(pullRequest.Title)
                ? $"Pull request {pullRequest.PullRequestId}"
                : pullRequest.Title,
            Description = NormalizeDescription(pullRequest.Description),
            CreatedBy = pullRequest.CreatedBy?.DisplayName,
            CreatedByUniqueName = pullRequest.CreatedBy?.UniqueName,
            ClosedAt = pullRequest.ClosedDate,
            SourceBranch = pullRequest.SourceRefName,
            TargetBranch = pullRequest.TargetRefName,
            Url = pullRequest.RemoteUrl ?? pullRequest.Url,
        };

    private static WorkItem MapWorkItem(WorkItemDto workItem, IReadOnlyCollection<string> relatedCommitIds)
    {
        var title = workItem.Fields.TryGetValue("System.Title", out var titleValue)
            ? titleValue?.ToString()
            : null;

        return new WorkItem
        {
            Id = workItem.Id,
            Title = string.IsNullOrWhiteSpace(title) ? $"Work item {workItem.Id}" : title,
            Description = GetFieldValue(workItem.Fields, "System.Description"),
            Type = GetFieldValue(workItem.Fields, "System.WorkItemType"),
            State = GetFieldValue(workItem.Fields, "System.State"),
            Url = workItem.Url,
            RelatedCommitIds = relatedCommitIds
                .Where(static commitId => !string.IsNullOrWhiteSpace(commitId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
        };
    }

    private static string? GetFieldValue(IReadOnlyDictionary<string, object?> fields, string fieldName) =>
        fields.TryGetValue(fieldName, out var value)
            ? value?.ToString()
            : null;

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static AuthenticationHeaderValue CreateAuthorizationHeader(string personalAccessToken)
    {
        var tokenBytes = Encoding.ASCII.GetBytes($":{personalAccessToken}");
        return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(tokenBytes));
    }

    private static string? TryGetContinuationToken(HttpResponseMessage response) =>
        response.Headers.TryGetValues("x-ms-continuationtoken", out var values)
            ? values.FirstOrDefault()
            : null;

    private static Uri BuildRepositoryUri(
        AzureDevOpsConnectionOptions connectionOptions,
        string relativePath,
        IReadOnlyDictionary<string, string?>? queryParameters = null)
    {
        var baseUrl = connectionOptions.OrganizationUrl.ToString().TrimEnd('/');
        var encodedProject = Uri.EscapeDataString(connectionOptions.ProjectName);
        var encodedRepository = Uri.EscapeDataString(connectionOptions.RepositoryName);
        var path = relativePath.Trim('/');
        var uri = $"{baseUrl}/{encodedProject}/_apis/git/repositories/{encodedRepository}/{path}";
        return AppendQueryString(uri, queryParameters);
    }

    private static Uri BuildWorkItemBatchUri(
        AzureDevOpsConnectionOptions connectionOptions,
        IReadOnlyDictionary<string, string?>? queryParameters = null)
    {
        var baseUrl = connectionOptions.OrganizationUrl.ToString().TrimEnd('/');
        var encodedProject = Uri.EscapeDataString(connectionOptions.ProjectName);
        var uri = $"{baseUrl}/{encodedProject}/_apis/wit/workitemsbatch";
        return AppendQueryString(uri, queryParameters);
    }

    private static Uri AppendQueryString(string uri, IReadOnlyDictionary<string, string?>? queryParameters = null)
    {
        var parameters = new List<KeyValuePair<string, string?>>
        {
            new("api-version", ApiVersion),
        };

        if (queryParameters is not null)
        {
            parameters.AddRange(queryParameters);
        }

        var queryString = string.Join(
            "&",
            parameters
                .Where(static parameter => !string.IsNullOrWhiteSpace(parameter.Value))
                .Select(static parameter => $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value!)}"));

        return new Uri($"{uri}?{queryString}", UriKind.Absolute);
    }

    private static string StripRefsHeadsPrefix(string branchRef) =>
        branchRef.StartsWith("refs/heads/", StringComparison.OrdinalIgnoreCase)
            ? branchRef["refs/heads/".Length..]
            : branchRef;

    private static string? NormalizeBranchName(string? branchName)
    {
        if (string.IsNullOrWhiteSpace(branchName))
        {
            return null;
        }

        var normalized = branchName.Trim();

        if (normalized.StartsWith("refs/heads/", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        if (normalized.StartsWith("refs/remotes/", StringComparison.OrdinalIgnoreCase))
        {
            var afterRemotes = normalized["refs/remotes/".Length..];
            var remoteSeparatorIndex = afterRemotes.IndexOf('/');
            normalized = remoteSeparatorIndex >= 0
                ? afterRemotes[(remoteSeparatorIndex + 1)..]
                : afterRemotes;
        }
        else if (normalized.StartsWith("origin/", StringComparison.OrdinalIgnoreCase))
        {
            // Azure DevOps branch refs have no remote prefix; "origin/" is a local
            // Git remote-tracking convention that users commonly paste by mistake.
            normalized = normalized["origin/".Length..];
        }

        return $"refs/heads/{normalized}";
    }

    private static void ValidateConnection(AzureDevOpsConnectionOptions connectionOptions)
    {
        if (!connectionOptions.OrganizationUrl.IsAbsoluteUri)
        {
            throw new ArgumentException("OrganizationUrl must be an absolute URI.", nameof(connectionOptions));
        }

        if (string.IsNullOrWhiteSpace(connectionOptions.ProjectName))
        {
            throw new ArgumentException("ProjectName is required.", nameof(connectionOptions));
        }

        if (string.IsNullOrWhiteSpace(connectionOptions.RepositoryName))
        {
            throw new ArgumentException("RepositoryName is required.", nameof(connectionOptions));
        }

        if (string.IsNullOrWhiteSpace(connectionOptions.PersonalAccessToken))
        {
            throw new ArgumentException("PersonalAccessToken is required.", nameof(connectionOptions));
        }
    }

    private static void ValidateQuery(AzureDevOpsReleaseQuery query)
    {
        if (query.ToDate < query.FromDate)
        {
            throw new ArgumentException("ToDate must be greater than or equal to FromDate.", nameof(query));
        }

        if (query.PageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "PageSize must be greater than zero.");
        }
    }
}
