namespace DevReleaseReporter.Core.Models;

public sealed record Commit
{
    public required string Id { get; init; }

    public required string Message { get; init; }

    public string? Description { get; init; }

    public string? AuthorName { get; init; }

    public string? AuthorEmail { get; init; }

    public DateTimeOffset? CommittedAt { get; init; }

    public string? BranchName { get; init; }

    public string? Url { get; init; }

    public int? PullRequestId { get; init; }
}
