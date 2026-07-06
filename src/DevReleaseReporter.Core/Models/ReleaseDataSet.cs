namespace DevReleaseReporter.Core.Models;

public sealed record ReleaseDataSet
{
    public IReadOnlyList<Commit> Commits { get; init; } = Array.Empty<Commit>();

    public IReadOnlyList<PullRequest> PullRequests { get; init; } = Array.Empty<PullRequest>();

    public IReadOnlyList<WorkItem> WorkItems { get; init; } = Array.Empty<WorkItem>();
}

