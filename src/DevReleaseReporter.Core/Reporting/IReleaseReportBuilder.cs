using DevReleaseReporter.Core.Models;

namespace DevReleaseReporter.Core.Reporting;

public interface IReleaseReportBuilder
{
    ReleaseReport Build(ReleaseReportRequest request, ReleaseDataSet releaseData);
}

