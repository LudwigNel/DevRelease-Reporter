namespace DevReleaseReporter.Core.Models;

public sealed record PullRequest
{
    public required int Id { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public string? CreatedBy { get; init; }

    public string? CreatedByUniqueName { get; init; }

    public DateTimeOffset? ClosedAt { get; init; }

    public string? SourceBranch { get; init; }

    public string? TargetBranch { get; init; }

    public string? Url { get; init; }

    public IReadOnlyList<string> CommitIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<WorkItem> WorkItems { get; init; } = Array.Empty<WorkItem>();
}
