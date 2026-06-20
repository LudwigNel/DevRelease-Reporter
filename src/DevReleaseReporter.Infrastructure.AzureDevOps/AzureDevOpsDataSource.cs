using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DevReleaseReporter.Application.Abstractions;
using DevReleaseReporter.Domain.Models;

namespace DevReleaseReporter.Infrastructure.AzureDevOps;

public sealed class AzureDevOpsDataSource(HttpClient httpClient, AzureDevOpsOptions options) : IReleaseDataSource
{
    public async Task<ReleaseDataSnapshot> GetSnapshotAsync(ReleaseQuery query, CancellationToken cancellationToken = default)
    {
        ApplyAuthenticationHeader();

        var commits = await GetCommitsAsync(query, cancellationToken).ConfigureAwait(false);
        var pullRequests = await GetPullRequestsAsync(query, cancellationToken).ConfigureAwait(false);

        var workItems = query.IncludeWorkItems ? await GetWorkItemsAsync(cancellationToken).ConfigureAwait(false) : [];

        return new ReleaseDataSnapshot(
            commits,
            pullRequests,
            workItems);
    }

    private void ApplyAuthenticationHeader()
    {
        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{options.PersonalAccessToken}"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
    }

    private async Task<IReadOnlyList<CommitInfo>> GetCommitsAsync(ReleaseQuery query, CancellationToken cancellationToken)
    {
        var commits = new List<CommitInfo>();
        const int pageSize = 200;

        for (var skip = 0; commits.Count < query.MaxCommits; skip += pageSize)
        {
            var requestUrl =
                $"https://dev.azure.com/{options.Organization}/{options.Project}/_apis/git/repositories/{options.Repository}/commits" +
                $"?searchCriteria.fromDate={query.From:O}&searchCriteria.toDate={query.To:O}" +
                $"&searchCriteria.$top={pageSize}&searchCriteria.$skip={skip}&api-version=7.1-preview.1";

            using var response = await httpClient.GetAsync(requestUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                break;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("value", out var items) || items.GetArrayLength() == 0)
            {
                break;
            }

            foreach (var item in items.EnumerateArray())
            {
                if (commits.Count >= query.MaxCommits)
                {
                    break;
                }

                commits.Add(new CommitInfo(
                    item.TryGetProperty("commitId", out var commitId) ? commitId.GetString() ?? string.Empty : string.Empty,
                    item.TryGetProperty("comment", out var message) ? message.GetString() ?? string.Empty : string.Empty,
                    item.TryGetProperty("author", out var author) && author.TryGetProperty("name", out var authorName)
                        ? authorName.GetString() ?? "unknown"
                        : "unknown",
                    item.TryGetProperty("author", out var authorNode) && authorNode.TryGetProperty("date", out var date)
                        ? date.GetDateTimeOffset()
                        : DateTimeOffset.MinValue,
                    []));
            }

            if (items.GetArrayLength() < pageSize)
            {
                break;
            }
        }

        return commits;
    }

    private async Task<IReadOnlyList<PullRequestInfo>> GetPullRequestsAsync(ReleaseQuery query, CancellationToken cancellationToken)
    {
        var requestUrl =
            $"https://dev.azure.com/{options.Organization}/{options.Project}/_apis/git/repositories/{options.Repository}/pullrequests" +
            $"?searchCriteria.status=all&$top={query.MaxPullRequests}&api-version=7.1-preview.1";

        using var response = await httpClient.GetAsync(requestUrl, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("value", out var pullRequests))
        {
            return [];
        }

        return pullRequests
            .EnumerateArray()
            .Select(item => new PullRequestInfo(
                item.TryGetProperty("pullRequestId", out var id) ? id.GetInt32() : 0,
                item.TryGetProperty("title", out var title) ? title.GetString() ?? string.Empty : string.Empty,
                item.TryGetProperty("status", out var status) ? status.GetString() ?? "unknown" : "unknown",
                item.TryGetProperty("createdBy", out var createdBy) && createdBy.TryGetProperty("displayName", out var displayName)
                    ? displayName.GetString() ?? "unknown"
                    : "unknown",
                item.TryGetProperty("creationDate", out var createdAt) ? createdAt.GetDateTimeOffset() : DateTimeOffset.MinValue,
                item.TryGetProperty("closedDate", out var closedAt) ? closedAt.GetDateTimeOffset() : null))
            .Where(pr => pr.CreatedAt >= query.From && pr.CreatedAt <= query.To)
            .Take(query.MaxPullRequests)
            .ToList();
    }

    private static Task<IReadOnlyList<WorkItemInfo>> GetWorkItemsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<WorkItemInfo>>([]);
    }
}
