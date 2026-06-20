using DevReleaseReporter.Application.Abstractions;
using DevReleaseReporter.Application.Reporting;
using DevReleaseReporter.Domain.Models;

namespace DevReleaseReporter.Application.Services;

public sealed class ReleaseReportService(
    IReleaseDataSource dataSource,
    ICommitClassifier classifier,
    IReportRenderer renderer)
{
    public async Task<RenderedReport> GenerateAsync(ReleaseQuery query, CancellationToken cancellationToken = default)
    {
        var snapshot = await dataSource.GetSnapshotAsync(query, cancellationToken).ConfigureAwait(false);

        var classifiedCommits = snapshot.Commits
            .Take(query.MaxCommits)
            .Select(commit => new ClassifiedCommit(commit, classifier.Classify(commit)))
            .ToList();

        var document = new ReleaseReportDocument(
            DateTimeOffset.UtcNow,
            query,
            classifiedCommits,
            snapshot.PullRequests.Take(query.MaxPullRequests).ToList(),
            snapshot.WorkItems);

        return new RenderedReport(renderer.Render(document), renderer.ContentType, document);
    }
}
