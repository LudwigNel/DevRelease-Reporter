using DevReleaseReporter.Core.Models;

namespace DevReleaseReporter.Core.Reporting;

public static class ReleaseDataSetFilter
{
    public static IReadOnlyList<string> GetAvailableContributors(ReleaseDataSet releaseData)
    {
        ArgumentNullException.ThrowIfNull(releaseData);

        return releaseData.Commits
            .SelectMany(static commit => GetContributorCandidates(commit.AuthorName, commit.AuthorEmail))
            .Concat(releaseData.PullRequests.SelectMany(static pullRequest => GetContributorCandidates(pullRequest.CreatedBy, pullRequest.CreatedByUniqueName)))
            .Where(static contributor => !string.IsNullOrWhiteSpace(contributor))
            .Select(static contributor => contributor!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static contributor => contributor, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static ReleaseDataSet FilterByContributor(ReleaseDataSet releaseData, string? contributorName)
    {
        ArgumentNullException.ThrowIfNull(releaseData);

        var normalizedContributor = NormalizeText(contributorName);
        if (normalizedContributor is null)
        {
            return releaseData;
        }

        var pullRequests = releaseData.PullRequests
            .Where(pullRequest => MatchesContributor(normalizedContributor, pullRequest.CreatedBy, pullRequest.CreatedByUniqueName))
            .ToArray();

        var pullRequestCommitIds = pullRequests
            .SelectMany(static pullRequest => pullRequest.CommitIds)
            .Where(static commitId => !string.IsNullOrWhiteSpace(commitId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var commits = releaseData.Commits
            .Where(commit =>
                MatchesContributor(normalizedContributor, commit.AuthorName, commit.AuthorEmail) &&
                !pullRequestCommitIds.Contains(commit.Id))
            .ToArray();

        var standaloneCommitIds = commits
            .Select(static commit => commit.Id)
            .Where(static commitId => !string.IsNullOrWhiteSpace(commitId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var workItems = releaseData.WorkItems
            .Where(workItem => workItem.RelatedCommitIds.Any(standaloneCommitIds.Contains))
            .ToArray();

        return new ReleaseDataSet
        {
            Commits = commits,
            PullRequests = pullRequests,
            WorkItems = workItems,
        };
    }

    private static IEnumerable<string> GetContributorCandidates(params string?[] values) =>
        values.Select(NormalizeText)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!);

    private static bool MatchesContributor(string contributorName, params string?[] candidates) =>
        GetContributorCandidates(candidates)
            .Any(candidate => string.Equals(candidate, contributorName, StringComparison.OrdinalIgnoreCase));

    private static string? NormalizeText(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? null
            : text.Trim();
}
