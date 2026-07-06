namespace DevReleaseReporter.Core.Models;

public sealed record ReleaseReportRequest
{
    public required string OrganizationName { get; init; }

    public required string ProjectName { get; init; }

    public required string RepositoryName { get; init; }

    public required DateTimeOffset FromDate { get; init; }

    public required DateTimeOffset ToDate { get; init; }

    public string? BranchName { get; init; }

    public string? ContributorName { get; init; }

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public bool IncludeTechnicalAppendix { get; init; } = true;

    public int HighlightLimit { get; init; } = 5;
}
