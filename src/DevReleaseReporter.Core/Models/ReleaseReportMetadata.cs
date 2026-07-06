namespace DevReleaseReporter.Core.Models;

public sealed record ReleaseReportMetadata
{
    public required string OrganizationName { get; init; }

    public required string ProjectName { get; init; }

    public required string RepositoryName { get; init; }

    public required DateTimeOffset FromDate { get; init; }

    public required DateTimeOffset ToDate { get; init; }

    public string? BranchName { get; init; }

    public string? ContributorName { get; init; }

    public required DateTimeOffset GeneratedAt { get; init; }
}
