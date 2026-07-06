using System.Text;
using DevReleaseReporter.Core.Classification;
using DevReleaseReporter.Core.Models;
using DevReleaseReporter.Core.Reporting;
using DevReleaseReporter.Infrastructure.AzureDevOps;

namespace DevReleaseReporter.Cli;

internal static class ReleaseReportCliApplication
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    public static async Task<int> RunAsync(string[] args, TextWriter standardOutput, TextWriter standardError)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        if (CliOptionsParser.WantsHelp(args))
        {
            await standardOutput.WriteLineAsync(CliUsage.GetText());
            return 0;
        }

        try
        {
            var options = CliOptionsParser.Parse(args);
            using var cancellationTokenSource = CreateCancellationTokenSource();

            await standardOutput.WriteLineAsync("Fetching Azure DevOps release data...");

            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5),
            };

            var releaseSource = new AzureDevOpsReleaseSource(httpClient);
            var releaseData = await releaseSource.GetReleaseDataAsync(
                new AzureDevOpsConnectionOptions
                {
                    OrganizationUrl = options.OrganizationUrl,
                    ProjectName = options.ProjectName,
                    RepositoryName = options.RepositoryName,
                    PersonalAccessToken = options.PersonalAccessToken,
                },
                new AzureDevOpsReleaseQuery
                {
                    FromDate = options.FromDate,
                    ToDate = options.ToDate,
                    BranchName = options.BranchName,
                    IncludeWorkItems = options.IncludeWorkItems,
                    PageSize = options.PageSize,
                },
                cancellationTokenSource.Token);
            var filteredReleaseData = ReleaseDataSetFilter.FilterByContributor(releaseData, options.ContributorName);

            await standardOutput.WriteLineAsync("Building release report...");

            var reportBuilder = new ReleaseReportBuilder(new ReleaseClassifier());
            var report = reportBuilder.Build(
                new ReleaseReportRequest
                {
                    OrganizationName = DeriveOrganizationName(options.OrganizationUrl),
                    ProjectName = options.ProjectName,
                    RepositoryName = options.RepositoryName,
                    FromDate = options.FromDate,
                    ToDate = options.ToDate,
                    BranchName = options.BranchName,
                    ContributorName = options.ContributorName,
                    IncludeTechnicalAppendix = options.IncludeTechnicalAppendix,
                    HighlightLimit = options.HighlightLimit,
                },
                filteredReleaseData);

            var renderer = new HtmlReleaseReportRenderer();
            var html = renderer.Render(report);
            var outputPath = Path.GetFullPath(options.OutputPath);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(outputPath, html, Utf8WithoutBom, cancellationTokenSource.Token);

            await standardOutput.WriteLineAsync($"HTML report written to {outputPath}");
            await standardOutput.WriteLineAsync(
                $"Delivered {report.Summary.TotalItems} items from {report.Summary.PullRequestCount} pull requests and {report.Summary.CommitCount} commits.");

            return 0;
        }
        catch (CliUsageException exception)
        {
            await standardError.WriteLineAsync(exception.Message);
            await standardError.WriteLineAsync();
            await standardError.WriteLineAsync(CliUsage.GetText());
            return 1;
        }
        catch (AzureDevOpsClientException exception)
        {
            await standardError.WriteLineAsync($"Azure DevOps error: {exception.Message}");
            return 2;
        }
        catch (OperationCanceledException)
        {
            await standardError.WriteLineAsync("Operation cancelled.");
            return 3;
        }
    }

    private static CancellationTokenSource CreateCancellationTokenSource()
    {
        var cancellationTokenSource = new CancellationTokenSource();

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        return cancellationTokenSource;
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
}
