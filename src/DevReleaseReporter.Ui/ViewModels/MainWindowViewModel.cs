using System.Collections.ObjectModel;
using System.IO;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia;
using Avalonia.Styling;
using DevReleaseReporter.Core.Models;
using DevReleaseReporter.Core.Reporting;
using DevReleaseReporter.Infrastructure.AzureDevOps;
using DevReleaseReporter.Ui.Models;
using DevReleaseReporter.Ui.Services;

namespace DevReleaseReporter.Ui.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const string AllContributorsOption = "All contributors";
    private const string LastUsedProfileName = UiPreferencesStore.LastUsedProfileName;
    private const string ThemeSystem = "System";
    private const string ThemeLight = "Light";
    private const string ThemeDark = "Dark";
    private readonly ReleaseReportWorkflowService workflowService;
    private readonly UiPreferencesStore preferencesStore;
    private readonly IPersonalAccessTokenStore personalAccessTokenStore;
    private bool isApplyingProfile;
    private bool isSynchronizingDates;
    private ReleaseDataSet? fetchedReleaseData;
    private ReleaseReport? generatedReport;
    private string? generatedHtml;

    public MainWindowViewModel()
        : this(new ReleaseReportWorkflowService(), new UiPreferencesStore(), new PersonalAccessTokenStore())
    {
    }

    internal MainWindowViewModel(
        ReleaseReportWorkflowService workflowService,
        UiPreferencesStore preferencesStore,
        IPersonalAccessTokenStore personalAccessTokenStore)
    {
        this.workflowService = workflowService;
        this.preferencesStore = preferencesStore;
        this.personalAccessTokenStore = personalAccessTokenStore;

        organizationUrl = "https://dev.azure.com/your-org";
        projectName = string.Empty;
        repositoryName = string.Empty;
        branchName = string.Empty;
        personalAccessToken = string.Empty;
        fromDate = DateTime.Today.AddDays(-7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        toDate = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        fromDateSelection = new DateTimeOffset(DateTime.Today.AddDays(-7));
        toDateSelection = new DateTimeOffset(DateTime.Today);
        availableContributors = new ObservableCollection<string> { AllContributorsOption };
        profileOptions = new ObservableCollection<string> { LastUsedProfileName };
        themeOptions = [ThemeSystem, ThemeLight, ThemeDark];
        selectedTheme = ThemeSystem;
        selectedProfileName = LastUsedProfileName;
        profileNameInput = string.Empty;
        selectedContributor = AllContributorsOption;
        outputDirectory = BuildDefaultOutputDirectory();
        reportName = "release-report";
        statusMessage = "Ready to fetch Azure DevOps release data.";
        fetchSummary = "No release data fetched yet.";
        lastActivityText = "No activity yet";
        previewTitle = "Release report preview";
        previewSubtitle = "Fetch Azure DevOps activity to see the release scope and contributor options.";
        previewText = "Fetch data, generate the report, and this panel will show a stakeholder-friendly preview.";
        statOneLabel = "Date range";
        statOneValue = "Last 7 days";
        statTwoLabel = "Theme";
        statTwoValue = ThemeSystem;
        statThreeLabel = "Profile";
        statThreeValue = LastUsedProfileName;
        statFourLabel = "Output";
        statFourValue = "HTML / Excel";

        FetchDataCommand = new AsyncRelayCommand(FetchDataAsync, CanFetchData);
        GenerateReportCommand = new AsyncRelayCommand(GenerateReportAsync, CanGenerateReport);
        ExportHtmlCommand = new AsyncRelayCommand(ExportHtmlAsync, CanExportHtml);
        ExportExcelCommand = new AsyncRelayCommand(ExportExcelAsync, CanExportExcel);
        SaveProfileCommand = new RelayCommand(SaveProfile, CanSaveProfile);
        DeleteProfileCommand = new RelayCommand(DeleteProfile, CanDeleteProfile);
        SavePersonalAccessTokenCommand = new RelayCommand(SavePersonalAccessToken, CanSavePersonalAccessToken);
        ClearSavedPersonalAccessTokenCommand = new RelayCommand(ClearSavedPersonalAccessToken, CanClearSavedPersonalAccessToken);

        LoadPreferences();
        UpdateScopeSummary();
        ApplyThemeSelection();
        UpdatePersonalAccessTokenStorageState();
    }

    public string Heading => "DevRelease Reporter";

    public bool IsIdle => !IsBusy;

    public bool HasFetchedReleaseData => fetchedReleaseData is not null;

    public string ContributorFilterInstruction => HasFetchedReleaseData
        ? "Optionally select a contributor now to generate a contributor-specific report from the fetched dataset."
        : "Fetch data first. Contributor names are loaded from the fetched dataset after the fetch completes.";

    public string FetchButtonText => IsBusy && string.Equals(BusyOperation, "fetch", StringComparison.Ordinal)
        ? "Fetching..."
        : "Fetch Data";

    public string GenerateButtonText => IsBusy && string.Equals(BusyOperation, "generate", StringComparison.Ordinal)
        ? "Generating..."
        : "Generate Report";

    public string ExportButtonText => IsBusy && string.Equals(BusyOperation, "export", StringComparison.Ordinal)
        ? "Exporting..."
        : "Export as HTML";

    public string ExportExcelButtonText => IsBusy && string.Equals(BusyOperation, "export-excel", StringComparison.Ordinal)
        ? "Exporting..."
        : "Export as Excel";

    public IAsyncRelayCommand FetchDataCommand { get; }

    public IAsyncRelayCommand GenerateReportCommand { get; }

    public IAsyncRelayCommand ExportHtmlCommand { get; }

    public IAsyncRelayCommand ExportExcelCommand { get; }

    public IRelayCommand SaveProfileCommand { get; }

    public IRelayCommand DeleteProfileCommand { get; }

    public IRelayCommand SavePersonalAccessTokenCommand { get; }

    public IRelayCommand ClearSavedPersonalAccessTokenCommand { get; }

    public bool IsPersonalAccessTokenStorageSupported => personalAccessTokenStore.IsSupported;

    [ObservableProperty]
    private string organizationUrl;

    [ObservableProperty]
    private string projectName;

    [ObservableProperty]
    private string repositoryName;

    [ObservableProperty]
    private string branchName;

    [ObservableProperty]
    private string personalAccessToken;

    [ObservableProperty]
    private bool hasSavedPersonalAccessToken;

    [ObservableProperty]
    private string personalAccessTokenStorageMessage = string.Empty;

    [ObservableProperty]
    private string fromDate;

    [ObservableProperty]
    private string toDate;

    [ObservableProperty]
    private DateTimeOffset? fromDateSelection;

    [ObservableProperty]
    private DateTimeOffset? toDateSelection;

    [ObservableProperty]
    private ObservableCollection<string> availableContributors;

    [ObservableProperty]
    private ObservableCollection<string> profileOptions;

    [ObservableProperty]
    private IReadOnlyList<string> themeOptions;

    [ObservableProperty]
    private string selectedTheme;

    [ObservableProperty]
    private string selectedProfileName;

    [ObservableProperty]
    private string profileNameInput;

    [ObservableProperty]
    private string selectedContributor;

    [ObservableProperty]
    private string outputDirectory;

    [ObservableProperty]
    private string reportName;

    [ObservableProperty]
    private bool includeWorkItems = true;

    [ObservableProperty]
    private bool includeTechnicalAppendix = true;

    [ObservableProperty]
    private string statusMessage;

    [ObservableProperty]
    private string fetchSummary;

    [ObservableProperty]
    private string previewTitle;

    [ObservableProperty]
    private string previewSubtitle;

    [ObservableProperty]
    private string previewText;

    [ObservableProperty]
    private string statOneLabel;

    [ObservableProperty]
    private string statOneValue;

    [ObservableProperty]
    private string statTwoLabel;

    [ObservableProperty]
    private string statTwoValue;

    [ObservableProperty]
    private string statThreeLabel;

    [ObservableProperty]
    private string statThreeValue;

    [ObservableProperty]
    private string statFourLabel;

    [ObservableProperty]
    private string statFourValue;

    [ObservableProperty]
    private string lastActivityText;

    [ObservableProperty]
    private string busyOperation = string.Empty;

    [ObservableProperty]
    private string busyDetail = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    private bool CanFetchData() => !IsBusy;

    private bool CanGenerateReport() => !IsBusy && fetchedReleaseData is not null;

    private bool CanExportHtml() => !IsBusy && generatedReport is not null && !string.IsNullOrWhiteSpace(generatedHtml);

    private bool CanExportExcel() => !IsBusy && generatedReport is not null;

    private bool CanSaveProfile() => !IsBusy && !string.IsNullOrWhiteSpace(ProfileNameInput);

    private bool CanDeleteProfile() =>
        !IsBusy &&
        !string.IsNullOrWhiteSpace(SelectedProfileName) &&
        !string.Equals(SelectedProfileName, LastUsedProfileName, StringComparison.OrdinalIgnoreCase);

    private bool CanSavePersonalAccessToken() =>
        !IsBusy &&
        IsPersonalAccessTokenStorageSupported &&
        !string.IsNullOrWhiteSpace(SelectedProfileName) &&
        !string.IsNullOrWhiteSpace(PersonalAccessToken);

    private bool CanClearSavedPersonalAccessToken() =>
        !IsBusy &&
        IsPersonalAccessTokenStorageSupported &&
        !string.IsNullOrWhiteSpace(SelectedProfileName) &&
        (HasSavedPersonalAccessToken || !string.IsNullOrWhiteSpace(PersonalAccessToken));

    partial void OnIsBusyChanged(bool value)
    {
        NotifyCommandStateChanged();
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(FetchButtonText));
        OnPropertyChanged(nameof(GenerateButtonText));
        OnPropertyChanged(nameof(ExportButtonText));
        OnPropertyChanged(nameof(ExportExcelButtonText));
    }

    partial void OnBusyOperationChanged(string value)
    {
        OnPropertyChanged(nameof(FetchButtonText));
        OnPropertyChanged(nameof(GenerateButtonText));
        OnPropertyChanged(nameof(ExportButtonText));
        OnPropertyChanged(nameof(ExportExcelButtonText));
    }

    partial void OnSelectedThemeChanged(string value)
    {
        ApplyThemeSelection();
        StatTwoValue = value;
        SavePreferences();
    }

    partial void OnSelectedProfileNameChanged(string value)
    {
        if (isApplyingProfile)
        {
            return;
        }

        ApplySelectedProfile(value);
        NotifyCommandStateChanged();
    }

    partial void OnProfileNameInputChanged(string value) => NotifyCommandStateChanged();

    partial void OnPersonalAccessTokenChanged(string value)
    {
        NotifyCommandStateChanged();
    }

    partial void OnSelectedContributorChanged(string value)
    {
        PersistCurrentProfile();
    }

    partial void OnOrganizationUrlChanged(string value) => HandleFetchScopeChanged();

    partial void OnProjectNameChanged(string value) => HandleFetchScopeChanged();

    partial void OnRepositoryNameChanged(string value) => HandleFetchScopeChanged();

    partial void OnBranchNameChanged(string value) => HandleFetchScopeChanged();

    partial void OnFromDateChanged(string value)
    {
        if (isSynchronizingDates)
        {
            return;
        }

        isSynchronizingDates = true;
        FromDateSelection = ParseDateSelection(value);
        isSynchronizingDates = false;
        UpdateScopeSummary();
        HandleFetchScopeChanged();
    }

    partial void OnToDateChanged(string value)
    {
        if (isSynchronizingDates)
        {
            return;
        }

        isSynchronizingDates = true;
        ToDateSelection = ParseDateSelection(value);
        isSynchronizingDates = false;
        UpdateScopeSummary();
        HandleFetchScopeChanged();
    }

    partial void OnFromDateSelectionChanged(DateTimeOffset? value)
    {
        if (isSynchronizingDates)
        {
            return;
        }

        isSynchronizingDates = true;
        FromDate = FormatDateSelection(value);
        isSynchronizingDates = false;
        UpdateScopeSummary();
        HandleFetchScopeChanged();
    }

    partial void OnToDateSelectionChanged(DateTimeOffset? value)
    {
        if (isSynchronizingDates)
        {
            return;
        }

        isSynchronizingDates = true;
        ToDate = FormatDateSelection(value);
        isSynchronizingDates = false;
        UpdateScopeSummary();
        HandleFetchScopeChanged();
    }

    partial void OnOutputDirectoryChanged(string value) => PersistCurrentProfile();

    partial void OnReportNameChanged(string value) => PersistCurrentProfile();

    partial void OnIncludeWorkItemsChanged(bool value) => HandleFetchScopeChanged();

    partial void OnIncludeTechnicalAppendixChanged(bool value) => PersistCurrentProfile();

    private async Task FetchDataAsync()
    {
        try
        {
            SetBusyState("fetch", "Fetching Azure DevOps data...", "Connecting to Azure DevOps...");

            var request = BuildWorkflowRequest();
            var progress = new Progress<string>(message =>
            {
                BusyDetail = message;
                StatusMessage = message;
            });

            fetchedReleaseData = await workflowService.FetchReleaseDataAsync(request, progress);
            NotifyFetchStateChanged();
            generatedReport = null;
            generatedHtml = null;
            SyncContributorOptions(workflowService.GetAvailableContributors(fetchedReleaseData));
            SelectedContributor = AvailableContributors.Contains(SelectedContributor, StringComparer.OrdinalIgnoreCase)
                ? SelectedContributor
                : AllContributorsOption;

            FetchSummary =
                $"Fetched {fetchedReleaseData.PullRequests.Count} pull requests, {fetchedReleaseData.Commits.Count} commits, and {fetchedReleaseData.WorkItems.Count} standalone work items.";
            UpdateFetchPreview(fetchedReleaseData);
            PersistPersonalAccessTokenIfAvailable();
            StatusMessage = "Release data fetched. Optionally choose a contributor, then generate the report.";
            LastActivityText = $"Fetched at {DateTimeOffset.Now:dd MMM yyyy HH:mm}";
            PersistCurrentProfile();
        }
        catch (ArgumentException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (FormatException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (AzureDevOpsClientException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (HttpRequestException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (TaskCanceledException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (InvalidOperationException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (PlatformNotSupportedException exception)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            ClearBusyState();
        }
    }

    private async Task ExportExcelAsync()
    {
        try
        {
            SetBusyState("export-excel", "Exporting Excel report...", "Writing the Excel workbook to disk...");
            await Task.Yield();

            var request = BuildWorkflowRequest();
            var outputPath = await workflowService.ExportExcelAsync(request, generatedReport!);

            PersistPersonalAccessTokenIfAvailable();
            StatusMessage = $"Excel report exported to {outputPath}";
            LastActivityText = $"Exported at {DateTimeOffset.Now:dd MMM yyyy HH:mm}";
            PersistCurrentProfile();
        }
        catch (ArgumentException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (IOException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (UnauthorizedAccessException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (InvalidOperationException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (PlatformNotSupportedException exception)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            ClearBusyState();
        }
    }

    private async Task GenerateReportAsync()
    {
        try
        {
            SetBusyState("generate", "Generating release report...", "Classifying items and composing the preview...");
            await Task.Yield();

            var request = BuildWorkflowRequest();
            var reportArtifacts = await Task.Run(() =>
            {
                var report = workflowService.BuildReport(request, fetchedReleaseData!);
                var html = workflowService.RenderHtml(report);
                return (Report: report, Html: html);
            });

            generatedReport = reportArtifacts.Report;
            generatedHtml = reportArtifacts.Html;

            UpdateReportPreview(generatedReport);
            PersistPersonalAccessTokenIfAvailable();
            StatusMessage = "Report generated. Review the preview and export the file as HTML or Excel when ready.";
            LastActivityText = $"Report generated at {DateTimeOffset.Now:dd MMM yyyy HH:mm}";
            PersistCurrentProfile();
        }
        catch (ArgumentException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (InvalidOperationException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (PlatformNotSupportedException exception)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            ClearBusyState();
        }
    }

    private async Task ExportHtmlAsync()
    {
        try
        {
            SetBusyState("export", "Exporting HTML report...", "Writing the HTML report to disk...");
            await Task.Yield();

            var request = BuildWorkflowRequest();
            var outputPath = await workflowService.ExportHtmlAsync(request, generatedHtml!);

            PersistPersonalAccessTokenIfAvailable();
            StatusMessage = $"HTML report exported to {outputPath}";
            LastActivityText = $"Exported at {DateTimeOffset.Now:dd MMM yyyy HH:mm}";
            PersistCurrentProfile();
        }
        catch (ArgumentException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (IOException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (UnauthorizedAccessException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (InvalidOperationException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (PlatformNotSupportedException exception)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            ClearBusyState();
        }
    }

    private ReleaseReportWorkflowRequest BuildWorkflowRequest() =>
        new()
        {
            OrganizationUrl = ParseOrganizationUrl(OrganizationUrl),
            ProjectName = RequireValue(ProjectName, nameof(ProjectName)),
            RepositoryName = RequireValue(RepositoryName, nameof(RepositoryName)),
            PersonalAccessToken = RequireValue(PersonalAccessToken, nameof(PersonalAccessToken)),
            FromDate = ParseDate(FromDate, endOfDay: false, nameof(FromDate)),
            ToDate = ParseDate(ToDate, endOfDay: true, nameof(ToDate)),
            BranchName = NormalizeOptionalValue(BranchName),
            ContributorName = NormalizeContributorSelection(SelectedContributor),
            OutputDirectory = RequireValue(OutputDirectory, nameof(OutputDirectory)),
            ReportName = RequireValue(ReportName, nameof(ReportName)),
            IncludeWorkItems = IncludeWorkItems,
            IncludeTechnicalAppendix = IncludeTechnicalAppendix,
        };

    private static Uri ParseOrganizationUrl(string value)
    {
        if (!Uri.TryCreate(RequireValue(value, nameof(OrganizationUrl)), UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Organization URL must be a valid absolute URL.");
        }

        return uri;
    }

    private static DateTimeOffset ParseDate(string value, bool endOfDay, string fieldName)
    {
        var normalized = RequireValue(value, fieldName);

        if (DateOnly.TryParseExact(normalized, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
        {
            var timeOnly = endOfDay
                ? new TimeOnly(23, 59, 59, 999).Add(TimeSpan.FromTicks(9999))
                : TimeOnly.MinValue;

            return new DateTimeOffset(dateOnly.ToDateTime(timeOnly, DateTimeKind.Local));
        }

        if (DateTimeOffset.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dateTimeOffset))
        {
            return dateTimeOffset;
        }

        throw new FormatException($"{fieldName} must be a valid yyyy-MM-dd or ISO 8601 date.");
    }

    private static DateTimeOffset? ParseDateSelection(string value)
    {
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
        {
            return new DateTimeOffset(dateOnly.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local));
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date)
            ? date
            : null;
    }

    private static string FormatDateSelection(DateTimeOffset? value) =>
        value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string RequireValue(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required.");
        }

        return value.Trim();
    }

    private static string? NormalizeOptionalValue(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    private static string? NormalizeContributorSelection(string value)
    {
        var normalized = NormalizeOptionalValue(value);
        return string.Equals(normalized, AllContributorsOption, StringComparison.Ordinal)
            ? null
            : normalized;
    }

    private void UpdateFetchPreview(ReleaseDataSet releaseData)
    {
        var builder = new StringBuilder();
        var contributors = ReleaseDataSetFilter.GetAvailableContributors(releaseData);
        PreviewTitle = "Fetched Azure DevOps activity";
        PreviewSubtitle = "Use the contributor filter or generate the release report from this dataset.";
        StatOneLabel = "Pull requests";
        StatOneValue = releaseData.PullRequests.Count.ToString(CultureInfo.InvariantCulture);
        StatTwoLabel = "Commits";
        StatTwoValue = releaseData.Commits.Count.ToString(CultureInfo.InvariantCulture);
        StatThreeLabel = "Work items";
        StatThreeValue = releaseData.WorkItems.Count.ToString(CultureInfo.InvariantCulture);
        StatFourLabel = "Contributors";
        StatFourValue = contributors.Count.ToString(CultureInfo.InvariantCulture);

        builder.AppendLine("Data fetch complete");
        builder.AppendLine();
        builder.AppendLine($"Pull requests: {releaseData.PullRequests.Count}");
        builder.AppendLine($"Commits: {releaseData.Commits.Count}");
        builder.AppendLine($"Standalone work items: {releaseData.WorkItems.Count}");

        if (contributors.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Contributors found:");
            foreach (var contributor in contributors.Take(5))
            {
                builder.AppendLine($"- {contributor}");
            }
        }

        var samplePullRequests = releaseData.PullRequests.Take(3).Select(static pullRequest => pullRequest.Title).ToArray();
        if (samplePullRequests.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Sample pull requests:");
            foreach (var title in samplePullRequests)
            {
                builder.AppendLine($"- {title}");
            }
        }

        PreviewText = builder.ToString().TrimEnd();
    }

    private void UpdateReportPreview(ReleaseReport report)
    {
        var builder = new StringBuilder();
        PreviewTitle = $"{report.Metadata.ProjectName} release report";
        PreviewSubtitle = string.IsNullOrWhiteSpace(report.Metadata.ContributorName)
            ? $"{report.Metadata.FromDate:dd MMM yyyy} to {report.Metadata.ToDate:dd MMM yyyy}"
            : $"{report.Metadata.FromDate:dd MMM yyyy} to {report.Metadata.ToDate:dd MMM yyyy} · {report.Metadata.ContributorName}";
        StatOneLabel = "Items delivered";
        StatOneValue = report.Summary.TotalItems.ToString(CultureInfo.InvariantCulture);
        StatTwoLabel = "Features";
        StatTwoValue = report.Summary.FeatureCount.ToString(CultureInfo.InvariantCulture);
        StatThreeLabel = "Bug fixes";
        StatThreeValue = report.Summary.BugFixCount.ToString(CultureInfo.InvariantCulture);
        StatFourLabel = "Improvements";
        StatFourValue = report.Summary.ImprovementsCount.ToString(CultureInfo.InvariantCulture);

        builder.AppendLine($"{report.Metadata.ProjectName} release report");
        builder.AppendLine($"{report.Metadata.FromDate:dd MMM yyyy} to {report.Metadata.ToDate:dd MMM yyyy}");

        if (!string.IsNullOrWhiteSpace(report.Metadata.ContributorName))
        {
            builder.AppendLine($"Contributor: {report.Metadata.ContributorName}");
        }

        builder.AppendLine();
        builder.AppendLine($"Total items delivered: {report.Summary.TotalItems}");
        builder.AppendLine($"Features: {report.Summary.FeatureCount}");
        builder.AppendLine($"Bug fixes: {report.Summary.BugFixCount}");
        builder.AppendLine($"Improvements: {report.Summary.ImprovementsCount}");

        if (report.Highlights.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Highlights");
            foreach (var highlight in report.Highlights)
            {
                builder.AppendLine($"- {highlight.Title}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Detailed sections");
        foreach (var section in report.Sections)
        {
            builder.AppendLine($"- {section.Title}: {section.Items.Count}");
        }

        if (report.TechnicalAppendix.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine($"Technical appendix items: {report.TechnicalAppendix.Count}");
        }

        PreviewText = builder.ToString().TrimEnd();
    }

    private void NotifyCommandStateChanged()
    {
        FetchDataCommand.NotifyCanExecuteChanged();
        GenerateReportCommand.NotifyCanExecuteChanged();
        ExportHtmlCommand.NotifyCanExecuteChanged();
        ExportExcelCommand.NotifyCanExecuteChanged();
        SaveProfileCommand.NotifyCanExecuteChanged();
        DeleteProfileCommand.NotifyCanExecuteChanged();
        SavePersonalAccessTokenCommand.NotifyCanExecuteChanged();
        ClearSavedPersonalAccessTokenCommand.NotifyCanExecuteChanged();
    }

    private void NotifyFetchStateChanged()
    {
        OnPropertyChanged(nameof(HasFetchedReleaseData));
        OnPropertyChanged(nameof(ContributorFilterInstruction));
        NotifyCommandStateChanged();
    }

    private static string BuildDefaultOutputDirectory()
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(documentsPath))
        {
            documentsPath = AppContext.BaseDirectory;
        }

        return Path.Combine(documentsPath, "DevReleaseReporter");
    }

    private void SyncContributorOptions(IReadOnlyList<string> contributors)
    {
        ArgumentNullException.ThrowIfNull(contributors);

        AvailableContributors.Clear();
        AvailableContributors.Add(AllContributorsOption);

        foreach (var contributor in contributors)
        {
            AvailableContributors.Add(contributor);
        }
    }

    private void SetBusyState(string operation, string status, string detail)
    {
        BusyOperation = operation;
        BusyDetail = detail;
        StatusMessage = status;
        IsBusy = true;
    }

    private void ClearBusyState()
    {
        BusyDetail = string.Empty;
        BusyOperation = string.Empty;
        IsBusy = false;
    }

    private void LoadPreferences()
    {
        try
        {
            var preferences = preferencesStore.Load();
            ProfileOptions.Clear();
            foreach (var profile in preferences.Profiles.OrderBy(static profile => profile.Name, StringComparer.OrdinalIgnoreCase))
            {
                ProfileOptions.Add(profile.Name);
            }

            SelectedTheme = ThemeOptions.Contains(preferences.Theme, StringComparer.OrdinalIgnoreCase)
                ? preferences.Theme
                : ThemeSystem;
            StatTwoValue = SelectedTheme;
            ApplySelectedProfile(preferences.SelectedProfileName);
        }
        catch (IOException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (UnauthorizedAccessException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (JsonException exception)
        {
            StatusMessage = exception.Message;
        }
    }

    private void SavePreferences()
    {
        try
        {
            preferencesStore.Save(new UiPreferences
            {
                Theme = SelectedTheme,
                SelectedProfileName = SelectedProfileName,
                Profiles = BuildProfilesSnapshot(),
            });
        }
        catch (IOException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (UnauthorizedAccessException exception)
        {
            StatusMessage = exception.Message;
        }
    }

    private void ApplyThemeSelection()
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.RequestedThemeVariant = SelectedTheme switch
        {
            ThemeLight => ThemeVariant.Light,
            ThemeDark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }

    private void UpdateScopeSummary()
    {
        StatOneValue = $"{FromDate} -> {ToDate}";
        StatThreeValue = SelectedProfileName;
    }

    private void HandleFetchScopeChanged()
    {
        PersistCurrentProfile();

        if (isApplyingProfile)
        {
            return;
        }

        InvalidateFetchedData();
    }

    private void SaveProfile()
    {
        try
        {
            var normalizedName = ProfileNameInput.Trim();
            var personalAccessToken = PersonalAccessToken;

            if (!ProfileOptions.Contains(normalizedName, StringComparer.OrdinalIgnoreCase))
            {
                ProfileOptions.Add(normalizedName);
            }

            SelectedProfileName = normalizedName;
            PersonalAccessToken = personalAccessToken;
            PersistCurrentProfile();
            PersistPersonalAccessTokenIfAvailable();
            StatusMessage = HasSavedPersonalAccessToken
                ? $"Saved profile '{normalizedName}' and stored its PAT securely."
                : $"Saved profile '{normalizedName}'.";
        }
        catch (InvalidOperationException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (PlatformNotSupportedException exception)
        {
            StatusMessage = exception.Message;
        }
    }

    private void DeleteProfile()
    {
        if (string.Equals(SelectedProfileName, LastUsedProfileName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var profileToRemove = SelectedProfileName;
        DeleteSavedPersonalAccessToken(profileToRemove);
        ProfileOptions.Remove(profileToRemove);
        SelectedProfileName = LastUsedProfileName;
        ProfileNameInput = string.Empty;
        SavePreferences();
        StatusMessage = $"Deleted profile '{profileToRemove}' and removed any saved PAT.";
    }

    private void ApplySelectedProfile(string profileName)
    {
        var profile = BuildProfilesSnapshot()
            .FirstOrDefault(existingProfile => string.Equals(existingProfile.Name, profileName, StringComparison.OrdinalIgnoreCase));

        if (profile is null)
        {
            return;
        }

        isApplyingProfile = true;
        try
        {
            OrganizationUrl = profile.OrganizationUrl;
            ProjectName = profile.ProjectName;
            RepositoryName = profile.RepositoryName;
            BranchName = profile.BranchName;
            FromDate = string.IsNullOrWhiteSpace(profile.FromDate) ? FromDate : profile.FromDate;
            ToDate = string.IsNullOrWhiteSpace(profile.ToDate) ? ToDate : profile.ToDate;
            OutputDirectory = string.IsNullOrWhiteSpace(profile.OutputDirectory) ? OutputDirectory : profile.OutputDirectory;
            ReportName = string.IsNullOrWhiteSpace(profile.ReportName) ? ReportName : profile.ReportName;
            IncludeWorkItems = profile.IncludeWorkItems;
            IncludeTechnicalAppendix = profile.IncludeTechnicalAppendix;
            EnsureContributorOption(profile.ContributorName);
            SelectedContributor = string.IsNullOrWhiteSpace(profile.ContributorName) ? AllContributorsOption : profile.ContributorName;
            SelectedProfileName = profile.Name;
            ProfileNameInput = string.Equals(profile.Name, LastUsedProfileName, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : profile.Name;
            LoadSavedPersonalAccessToken(profile.Name);
            UpdateScopeSummary();
        }
        finally
        {
            isApplyingProfile = false;
        }

        InvalidateFetchedData();
        SavePreferences();
    }

    private void PersistCurrentProfile()
    {
        if (isApplyingProfile)
        {
            return;
        }

        SavePreferences();
        UpdateScopeSummary();
    }

    private IReadOnlyList<UiProfile> BuildProfilesSnapshot()
    {
        var profiles = preferencesStore.Load().Profiles
            .Where(static profile => !string.IsNullOrWhiteSpace(profile.Name))
            .Where(profile => ProfileOptions.Contains(profile.Name, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(static profile => profile.Name, StringComparer.OrdinalIgnoreCase);

        profiles[SelectedProfileName] = CreateCurrentProfile(SelectedProfileName);

        if (!profiles.ContainsKey(LastUsedProfileName))
        {
            profiles[LastUsedProfileName] = CreateCurrentProfile(LastUsedProfileName);
        }

        return profiles.Values
            .OrderBy(static profile => string.Equals(profile.Name, LastUsedProfileName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(static profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void SavePersonalAccessToken()
    {
        try
        {
            personalAccessTokenStore.Save(SelectedProfileName, RequireValue(PersonalAccessToken, nameof(PersonalAccessToken)));
            HasSavedPersonalAccessToken = true;
            UpdatePersonalAccessTokenStorageState();
            StatusMessage = $"Stored the PAT securely for profile '{SelectedProfileName}'.";
        }
        catch (InvalidOperationException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (PlatformNotSupportedException exception)
        {
            StatusMessage = exception.Message;
        }
    }

    private void ClearSavedPersonalAccessToken()
    {
        try
        {
            DeleteSavedPersonalAccessToken(SelectedProfileName);
            PersonalAccessToken = string.Empty;
            StatusMessage = $"Removed the saved PAT for profile '{SelectedProfileName}'.";
        }
        catch (InvalidOperationException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (PlatformNotSupportedException exception)
        {
            StatusMessage = exception.Message;
        }
    }

    private void LoadSavedPersonalAccessToken(string profileName)
    {
        if (!IsPersonalAccessTokenStorageSupported)
        {
            HasSavedPersonalAccessToken = false;
            UpdatePersonalAccessTokenStorageState();
            return;
        }

        try
        {
            PersonalAccessToken = personalAccessTokenStore.Load(profileName) ?? string.Empty;
            HasSavedPersonalAccessToken = !string.IsNullOrWhiteSpace(PersonalAccessToken);
            UpdatePersonalAccessTokenStorageState();
        }
        catch (InvalidOperationException exception)
        {
            HasSavedPersonalAccessToken = false;
            UpdatePersonalAccessTokenStorageState();
            StatusMessage = exception.Message;
        }
        catch (PlatformNotSupportedException exception)
        {
            HasSavedPersonalAccessToken = false;
            UpdatePersonalAccessTokenStorageState();
            StatusMessage = exception.Message;
        }
    }

    private void PersistPersonalAccessTokenIfAvailable()
    {
        if (!IsPersonalAccessTokenStorageSupported || string.IsNullOrWhiteSpace(PersonalAccessToken))
        {
            UpdatePersonalAccessTokenStorageState();
            return;
        }

        personalAccessTokenStore.Save(SelectedProfileName, PersonalAccessToken);
        HasSavedPersonalAccessToken = true;
        UpdatePersonalAccessTokenStorageState();
    }

    private void DeleteSavedPersonalAccessToken(string profileName)
    {
        if (!IsPersonalAccessTokenStorageSupported)
        {
            HasSavedPersonalAccessToken = false;
            UpdatePersonalAccessTokenStorageState();
            return;
        }

        personalAccessTokenStore.Delete(profileName);
        HasSavedPersonalAccessToken = false;
        UpdatePersonalAccessTokenStorageState();
    }

    private void UpdatePersonalAccessTokenStorageState()
    {
        PersonalAccessTokenStorageMessage = !IsPersonalAccessTokenStorageSupported
            ? "Secure PAT storage is currently only available on Windows."
            : HasSavedPersonalAccessToken
                ? "Stored securely in Windows Credential Manager for the selected profile."
                : "Not saved yet. Use the secure storage buttons to persist this PAT for the selected profile.";

        NotifyCommandStateChanged();
    }

    private void InvalidateFetchedData()
    {
        if (fetchedReleaseData is null && generatedReport is null && string.IsNullOrWhiteSpace(generatedHtml))
        {
            SyncContributorOptions(Array.Empty<string>());
            EnsureContributorOption(SelectedContributor);
            NotifyFetchStateChanged();
            return;
        }

        fetchedReleaseData = null;
        generatedReport = null;
        generatedHtml = null;
        FetchSummary = "No release data fetched yet.";
        PreviewTitle = "Release report preview";
        PreviewSubtitle = "Fetch Azure DevOps activity to see the release scope and contributor options.";
        PreviewText = "Fetch data, generate the report, and this panel will show a stakeholder-friendly preview.";
        StatOneLabel = "Date range";
        StatOneValue = $"{FromDate} -> {ToDate}";
        StatTwoLabel = "Theme";
        StatTwoValue = SelectedTheme;
        StatThreeLabel = "Profile";
        StatThreeValue = SelectedProfileName;
        StatFourLabel = "Output";
        StatFourValue = "HTML / Excel";
        SyncContributorOptions(Array.Empty<string>());
        EnsureContributorOption(SelectedContributor);
        NotifyFetchStateChanged();
    }

    private UiProfile CreateCurrentProfile(string profileName) =>
        new()
        {
            Name = profileName,
            OrganizationUrl = OrganizationUrl,
            ProjectName = ProjectName,
            RepositoryName = RepositoryName,
            BranchName = BranchName,
            ContributorName = SelectedContributor,
            FromDate = FromDate,
            ToDate = ToDate,
            OutputDirectory = OutputDirectory,
            ReportName = ReportName,
            IncludeWorkItems = IncludeWorkItems,
            IncludeTechnicalAppendix = IncludeTechnicalAppendix,
        };

    private void EnsureContributorOption(string contributorName)
    {
        if (string.IsNullOrWhiteSpace(contributorName) ||
            string.Equals(contributorName, AllContributorsOption, StringComparison.OrdinalIgnoreCase) ||
            AvailableContributors.Contains(contributorName, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        AvailableContributors.Add(contributorName);
    }
}
