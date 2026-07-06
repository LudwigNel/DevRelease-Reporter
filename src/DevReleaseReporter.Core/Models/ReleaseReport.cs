namespace DevReleaseReporter.Core.Models;

public sealed record ReleaseReport
{
    public required ReleaseReportMetadata Metadata { get; init; }

    public required ReportSummary Summary { get; init; }

    public IReadOnlyList<ReleaseHighlight> Highlights { get; init; } = Array.Empty<ReleaseHighlight>();

    public IReadOnlyList<ReportSection> Sections { get; init; } = Array.Empty<ReportSection>();

    public IReadOnlyList<TechnicalAppendixItem> TechnicalAppendix { get; init; } = Array.Empty<TechnicalAppendixItem>();
}

