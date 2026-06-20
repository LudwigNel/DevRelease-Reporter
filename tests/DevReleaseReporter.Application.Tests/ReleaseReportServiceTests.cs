using DevReleaseReporter.Application.Abstractions;
using DevReleaseReporter.Application.Classification;
using DevReleaseReporter.Application.Reporting;
using DevReleaseReporter.Application.Services;
using DevReleaseReporter.Domain.Models;

namespace DevReleaseReporter.Application.Tests;

public sealed class ReleaseReportServiceTests
{
    [Fact]
    public async Task GenerateAsync_BuildsCategorizedHtmlReport()
    {
        var source = new StubReleaseDataSource(new ReleaseDataSnapshot(
            [
                new CommitInfo("1", "feat: add dashboard", "A", DateTimeOffset.UtcNow, []),
                new CommitInfo("2", "fix: issue", "B", DateTimeOffset.UtcNow, [])
            ],
            [new PullRequestInfo(42, "Dashboard feature", "completed", "A", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)],
            []));

        var service = new ReleaseReportService(source, new KeywordCommitClassifier(), new HtmlReportRenderer());

        var rendered = await service.GenerateAsync(new ReleaseQuery(DateTimeOffset.UtcNow.AddDays(-7), DateTimeOffset.UtcNow));

        Assert.Equal("text/html", rendered.ContentType);
        Assert.Contains("Feature: 1", rendered.Content);
        Assert.Contains("BugFix: 1", rendered.Content);
        Assert.Contains("Dashboard feature", rendered.Content);
    }

    private sealed class StubReleaseDataSource(ReleaseDataSnapshot snapshot) : IReleaseDataSource
    {
        public Task<ReleaseDataSnapshot> GetSnapshotAsync(ReleaseQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(snapshot);
    }
}
