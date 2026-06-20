using DevReleaseReporter.Application.Classification;
using DevReleaseReporter.Application.Reporting;
using DevReleaseReporter.Application.Services;
using DevReleaseReporter.Domain.Models;
using DevReleaseReporter.Infrastructure.AzureDevOps;

if (args.Contains("--help", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine("Usage: DevReleaseReporter.Cli [--sample]");
    Console.WriteLine("Set AZDO_ORGANIZATION, AZDO_PROJECT, AZDO_REPOSITORY, AZDO_PAT for Azure DevOps mode.");
    return;
}

var query = new ReleaseQuery(
    DateTimeOffset.UtcNow.AddDays(-30),
    DateTimeOffset.UtcNow,
    MaxCommits: 1000,
    MaxPullRequests: 500,
    IncludeWorkItems: true);

var classifier = new KeywordCommitClassifier();
var renderer = new HtmlReportRenderer();

if (args.Contains("--sample", StringComparer.OrdinalIgnoreCase))
{
    var sampleCommits = new List<CommitInfo>
    {
        new("1", "feat: add release trend chart", "Dev A", DateTimeOffset.UtcNow.AddDays(-5), []),
        new("2", "fix: correct date formatting in report", "Dev B", DateTimeOffset.UtcNow.AddDays(-4), [])
    };

    var sampleSnapshot = new ReleaseDataSnapshot(
        sampleCommits,
        [new PullRequestInfo(10, "Release report improvements", "completed", "Dev B", DateTimeOffset.UtcNow.AddDays(-3), DateTimeOffset.UtcNow.AddDays(-2))],
        [new WorkItemInfo(1001, "User Story", "As a PM, I need a clean report", "Done")]);

    var sampleSource = new InMemoryReleaseDataSource(sampleSnapshot);
    var sampleService = new ReleaseReportService(sampleSource, classifier, renderer);
    var report = await sampleService.GenerateAsync(query).ConfigureAwait(false);
    await File.WriteAllTextAsync("release-report.html", report.Content).ConfigureAwait(false);
    Console.WriteLine("Sample report written to release-report.html");
    return;
}

var organization = Environment.GetEnvironmentVariable("AZDO_ORGANIZATION");
var project = Environment.GetEnvironmentVariable("AZDO_PROJECT");
var repository = Environment.GetEnvironmentVariable("AZDO_REPOSITORY");
var pat = Environment.GetEnvironmentVariable("AZDO_PAT");

if (string.IsNullOrWhiteSpace(organization) ||
    string.IsNullOrWhiteSpace(project) ||
    string.IsNullOrWhiteSpace(repository) ||
    string.IsNullOrWhiteSpace(pat))
{
    Console.WriteLine("Missing Azure DevOps settings. Use --sample or set AZDO_ORGANIZATION, AZDO_PROJECT, AZDO_REPOSITORY, AZDO_PAT.");
    return;
}

using var httpClient = new HttpClient();
var options = new AzureDevOpsOptions(organization, project, repository, pat);
var dataSource = new AzureDevOpsDataSource(httpClient, options);
var service = new ReleaseReportService(dataSource, classifier, renderer);
var result = await service.GenerateAsync(query).ConfigureAwait(false);
await File.WriteAllTextAsync("release-report.html", result.Content).ConfigureAwait(false);

Console.WriteLine("Report written to release-report.html");

internal sealed class InMemoryReleaseDataSource(ReleaseDataSnapshot snapshot) : DevReleaseReporter.Application.Abstractions.IReleaseDataSource
{
    public Task<ReleaseDataSnapshot> GetSnapshotAsync(ReleaseQuery query, CancellationToken cancellationToken = default)
        => Task.FromResult(snapshot);
}
