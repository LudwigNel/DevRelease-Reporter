namespace DevReleaseReporter.Core.Models;

public sealed record ReleaseClassificationResult
{
    public IReadOnlyList<ReportItem> Items { get; init; } = Array.Empty<ReportItem>();

    public IReadOnlyList<ReportSection> Sections { get; init; } = Array.Empty<ReportSection>();

    public required ReportSummary Summary { get; init; }
}

