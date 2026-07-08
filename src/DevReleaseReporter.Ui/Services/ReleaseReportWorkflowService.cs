using System.Text;
using DevReleaseReporter.Core.Classification;
using DevReleaseReporter.Core.Models;
using DevReleaseReporter.Core.Reporting;
using DevReleaseReporter.Infrastructure.AzureDevOps;

namespace DevReleaseReporter.Ui.Services;

public sealed class ReleaseReportWorkflowService
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
    private readonly HttpClient httpClient;
    private readonly AzureDevOpsReleaseSource releaseSource;

    public ReleaseReportWorkflowService()
    {
        httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5),
        };
        releaseSource = new AzureDevOpsReleaseSource(httpClient);
    }

    public async Task<ReleaseDataSet> FetchReleaseDataAsync(
        ReleaseReportWorkflowRequest request,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await releaseSource.GetReleaseDataAsync(
            new AzureDevOpsConnectionOptions
            {
                OrganizationUrl = request.OrganizationUrl,
                ProjectName = request.ProjectName,
                RepositoryName = request.RepositoryName,
                PersonalAccessToken = request.PersonalAccessToken,
            },
            new AzureDevOpsReleaseQuery
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                BranchName = request.BranchName,
                Progress = progress,
                IncludeWorkItems = request.IncludeWorkItems,
                PageSize = request.PageSize,
            },
            cancellationToken);
    }

    public ReleaseReport BuildReport(
        ReleaseReportWorkflowRequest request,
        ReleaseDataSet releaseData)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(releaseData);

        var filteredReleaseData = ReleaseDataSetFilter.FilterByContributor(releaseData, request.ContributorName);
        var builder = new ReleaseReportBuilder(new ReleaseClassifier());
        return builder.Build(
            new ReleaseReportRequest
            {
                OrganizationName = DeriveOrganizationName(request.OrganizationUrl),
                ProjectName = request.ProjectName,
                RepositoryName = request.RepositoryName,
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                BranchName = request.BranchName,
                ContributorName = request.ContributorName,
                IncludeTechnicalAppendix = request.IncludeTechnicalAppendix,
                HighlightLimit = request.HighlightLimit,
            },
            filteredReleaseData);
    }

    public IReadOnlyList<string> GetAvailableContributors(ReleaseDataSet releaseData)
    {
        ArgumentNullException.ThrowIfNull(releaseData);
        return ReleaseDataSetFilter.GetAvailableContributors(releaseData);
    }

    public string RenderHtml(ReleaseReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return new HtmlReleaseReportRenderer().Render(report);
    }

    public async Task<string> ExportHtmlAsync(
        ReleaseReportWorkflowRequest request,
        string html,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(html);

        var outputPath = ResolveOutputPath(request.OutputPath, ".html");
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(outputPath, html, Utf8WithoutBom, cancellationToken);
        return outputPath;
    }

    public async Task<string> ExportExcelAsync(
        ReleaseReportWorkflowRequest request,
        ReleaseReport report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(report);
        cancellationToken.ThrowIfCancellationRequested();

        var outputPath = ResolveOutputPath(request.OutputPath, ".xlsx");
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            new ExcelReleaseReportRenderer().Save(report, outputPath);
        }, cancellationToken);

        return outputPath;
    }

    private static string DeriveOrganizationName(Uri organizationUrl)
    {
        if (string.Equals(organizationUrl.Host, "dev.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            var firstSegment = organizationUrl.AbsolutePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(firstSegment))
            {
                return firstSegment;
            }
        }

        const string visualStudioSuffix = ".visualstudio.com";
        if (organizationUrl.Host.EndsWith(visualStudioSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return organizationUrl.Host[..^visualStudioSuffix.Length];
        }

        return organizationUrl.Host;
    }

    private static string ResolveOutputPath(string configuredPath, string requiredExtension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredExtension);

        var normalizedExtension = requiredExtension.StartsWith('.') ? requiredExtension : "." + requiredExtension;
        var fullPath = Path.GetFullPath(configuredPath.Trim());
        return Path.ChangeExtension(fullPath, normalizedExtension);
    }
}
