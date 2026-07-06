using DevReleaseReporter.Core.Models;

namespace DevReleaseReporter.Core.Reporting;

public interface IReleaseReportRenderer
{
    string Render(ReleaseReport report);
}

