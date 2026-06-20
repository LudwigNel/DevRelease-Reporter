using DevReleaseReporter.Domain.Models;

namespace DevReleaseReporter.Application.Reporting;

public sealed record RenderedReport(
    string Content,
    string ContentType,
    ReleaseReportDocument Document);
