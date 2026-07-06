namespace DevReleaseReporter.Ui.Models;

public sealed record UiPreferences
{
    public string Theme { get; init; } = "System";

    public string SelectedProfileName { get; init; } = "Last used";

    public IReadOnlyList<UiProfile> Profiles { get; init; } = Array.Empty<UiProfile>();

    // Legacy fields retained for migration from the single-profile settings format.
    public string? OrganizationUrl { get; init; }

    public string? ProjectName { get; init; }

    public string? RepositoryName { get; init; }

    public string? BranchName { get; init; }

    public string? ContributorName { get; init; }

    public string? FromDate { get; init; }

    public string? ToDate { get; init; }

    public string? OutputPath { get; init; }

    public bool IncludeWorkItems { get; init; } = true;

    public bool IncludeTechnicalAppendix { get; init; } = true;
}
