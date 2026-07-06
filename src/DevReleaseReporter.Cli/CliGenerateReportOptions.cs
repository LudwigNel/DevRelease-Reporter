namespace DevReleaseReporter.Cli;

internal sealed record CliGenerateReportOptions
{
    public required Uri OrganizationUrl { get; init; }

    public required string ProjectName { get; init; }

    public required string RepositoryName { get; init; }

    public required string PersonalAccessToken { get; init; }

    public required DateTimeOffset FromDate { get; init; }

    public required DateTimeOffset ToDate { get; init; }

    public required string OutputPath { get; init; }

    public string? BranchName { get; init; }

    public string? ContributorName { get; init; }

    public bool IncludeWorkItems { get; init; } = true;

    public bool IncludeTechnicalAppendix { get; init; } = true;

    public int HighlightLimit { get; init; } = 5;

    public int PageSize { get; init; } = 100;
}
