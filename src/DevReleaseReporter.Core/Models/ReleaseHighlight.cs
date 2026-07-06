namespace DevReleaseReporter.Core.Models;

public sealed record ReleaseHighlight
{
    public required string Title { get; init; }

    public string? Summary { get; init; }

    public required WorkCategory Category { get; init; }

    public required ReportItemSource Source { get; init; }

    public required string SourceIdentifier { get; init; }
}

