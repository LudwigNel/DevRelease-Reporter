namespace DevReleaseReporter.Core.Models;

public sealed record ReportSection
{
    public required ReportSectionKind Kind { get; init; }

    public required string Title { get; init; }

    public IReadOnlyList<ReportItem> Items { get; init; } = Array.Empty<ReportItem>();
}

