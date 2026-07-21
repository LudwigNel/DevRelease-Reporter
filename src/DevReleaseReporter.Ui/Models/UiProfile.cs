namespace DevReleaseReporter.Ui.Models;

public sealed record UiProfile
{
    public required string Name { get; init; }

    public string OrganizationUrl { get; init; } = "https://dev.azure.com/your-org";

    public string ProjectName { get; init; } = string.Empty;

    public string RepositoryName { get; init; } = string.Empty;

    public string BranchName { get; init; } = string.Empty;

    public string ContributorName { get; init; } = "All contributors";

    public string FromDate { get; init; } = string.Empty;

    public string ToDate { get; init; } = string.Empty;

    public string OutputDirectory { get; init; } = string.Empty;

    public string ReportName { get; init; } = "release-report";

    // Retained to migrate profiles saved before the export folder and report name were separated.
    public string? OutputPath { get; init; }

    public bool IncludeWorkItems { get; init; } = true;

    public bool IncludeTechnicalAppendix { get; init; } = true;
}
