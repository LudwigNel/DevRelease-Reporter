namespace DevReleaseReporter.Core.Models;

public sealed record ReportSummary
{
    public required int TotalItems { get; init; }

    public required int FeatureCount { get; init; }

    public required int BugFixCount { get; init; }

    public required int TechnicalCount { get; init; }

    public required int PerformanceCount { get; init; }

    public required int OtherCount { get; init; }

    public required int FeaturesDeliveredCount { get; init; }

    public required int BugsFixedCount { get; init; }

    public required int ImprovementsCount { get; init; }

    public required int PullRequestCount { get; init; }

    public required int CommitCount { get; init; }

    public required int WorkItemCount { get; init; }
}

