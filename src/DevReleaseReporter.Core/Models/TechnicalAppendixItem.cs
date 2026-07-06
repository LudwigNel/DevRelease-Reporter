namespace DevReleaseReporter.Core.Models;

public sealed record TechnicalAppendixItem
{
    public required string Title { get; init; }

    public required ReportItemSource Source { get; init; }

    public required string SourceIdentifier { get; init; }

    public string? SourceUrl { get; init; }

    public required WorkCategory Category { get; init; }

    public IReadOnlyList<string> RelatedCommitIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<WorkItem> RelatedWorkItems { get; init; } = Array.Empty<WorkItem>();
}

