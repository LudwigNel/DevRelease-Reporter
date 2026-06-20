using DevReleaseReporter.Application.Abstractions;
using DevReleaseReporter.Application.Services;
using DevReleaseReporter.Domain.Models;

namespace DevReleaseReporter.Desktop;

public sealed class DesktopShell(ReleaseReportService reportService)
{
    public Task<DevReleaseReporter.Application.Reporting.RenderedReport> GenerateAsync(
        ReleaseQuery query,
        CancellationToken cancellationToken = default) => reportService.GenerateAsync(query, cancellationToken);

    public static DesktopShell CreateDefault(IReleaseDataSource dataSource, ICommitClassifier classifier, IReportRenderer renderer)
        => new(new ReleaseReportService(dataSource, classifier, renderer));
}
