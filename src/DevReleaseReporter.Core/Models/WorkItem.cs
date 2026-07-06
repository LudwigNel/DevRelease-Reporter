namespace DevReleaseReporter.Core.Models;

public sealed record WorkItem
{
    public required int Id { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public string? Type { get; init; }

    public string? State { get; init; }

    public string? Url { get; init; }

    public IReadOnlyList<string> RelatedCommitIds { get; init; } = Array.Empty<string>();
}
