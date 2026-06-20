namespace DevReleaseReporter.Domain.Models;

public sealed record ReleaseReportDocument(
    DateTimeOffset GeneratedAt,
    ReleaseQuery Query,
    IReadOnlyList<ClassifiedCommit> Commits,
    IReadOnlyList<PullRequestInfo> PullRequests,
    IReadOnlyList<WorkItemInfo> WorkItems)
{
    public IReadOnlyDictionary<ChangeCategory, int> CategorySummary { get; } =
        Commits
            .GroupBy(c => c.Category)
            .ToDictionary(group => group.Key, group => group.Count());
}
