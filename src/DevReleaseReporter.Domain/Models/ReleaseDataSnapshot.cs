namespace DevReleaseReporter.Domain.Models;

public sealed record ReleaseDataSnapshot(
    IReadOnlyList<CommitInfo> Commits,
    IReadOnlyList<PullRequestInfo> PullRequests,
    IReadOnlyList<WorkItemInfo> WorkItems);
