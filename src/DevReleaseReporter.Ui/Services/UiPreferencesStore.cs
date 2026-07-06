using System.Text.Json;
using DevReleaseReporter.Ui.Models;

namespace DevReleaseReporter.Ui.Services;

public sealed class UiPreferencesStore
{
    public const string LastUsedProfileName = "Last used";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string preferencesPath;

    public UiPreferencesStore()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        preferencesPath = Path.Combine(appDataPath, "DevReleaseReporter", "ui-preferences.json");
    }

    public UiPreferences Load()
    {
        if (!File.Exists(preferencesPath))
        {
            return CreateDefaultPreferences();
        }

        var json = File.ReadAllText(preferencesPath);
        var preferences = JsonSerializer.Deserialize<UiPreferences>(json, JsonOptions) ?? CreateDefaultPreferences();
        return Normalize(preferences);
    }

    public void Save(UiPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var directory = Path.GetDirectoryName(preferencesPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(preferences, JsonOptions);
        File.WriteAllText(preferencesPath, json);
    }

    private static UiPreferences Normalize(UiPreferences preferences)
    {
        var profiles = preferences.Profiles
            .Where(static profile => !string.IsNullOrWhiteSpace(profile.Name))
            .Select(static profile => profile with { Name = profile.Name.Trim() })
            .DistinctBy(static profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (profiles.Count == 0)
        {
            profiles.Add(new UiProfile
            {
                Name = LastUsedProfileName,
                OrganizationUrl = preferences.OrganizationUrl ?? "https://dev.azure.com/your-org",
                ProjectName = preferences.ProjectName ?? string.Empty,
                RepositoryName = preferences.RepositoryName ?? string.Empty,
                BranchName = preferences.BranchName ?? string.Empty,
                ContributorName = preferences.ContributorName ?? "All contributors",
                FromDate = preferences.FromDate ?? string.Empty,
                ToDate = preferences.ToDate ?? string.Empty,
                OutputPath = preferences.OutputPath ?? string.Empty,
                IncludeWorkItems = preferences.IncludeWorkItems,
                IncludeTechnicalAppendix = preferences.IncludeTechnicalAppendix,
            });
        }

        if (!profiles.Any(static profile => string.Equals(profile.Name, LastUsedProfileName, StringComparison.OrdinalIgnoreCase)))
        {
            profiles.Insert(0, CreateDefaultProfile());
        }

        var selectedProfileName = profiles.Any(profile => string.Equals(profile.Name, preferences.SelectedProfileName, StringComparison.OrdinalIgnoreCase))
            ? preferences.SelectedProfileName
            : LastUsedProfileName;

        return new UiPreferences
        {
            Theme = string.IsNullOrWhiteSpace(preferences.Theme) ? "System" : preferences.Theme,
            SelectedProfileName = selectedProfileName,
            Profiles = profiles,
            IncludeWorkItems = preferences.IncludeWorkItems,
            IncludeTechnicalAppendix = preferences.IncludeTechnicalAppendix,
        };
    }

    private static UiPreferences CreateDefaultPreferences() =>
        new()
        {
            Profiles = [CreateDefaultProfile()],
        };

    private static UiProfile CreateDefaultProfile() =>
        new()
        {
            Name = LastUsedProfileName,
        };
}
