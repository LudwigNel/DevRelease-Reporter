using DevReleaseReporter.Domain.Models;

namespace DevReleaseReporter.Application.Abstractions;

public interface IReportRenderer
{
    string ContentType { get; }

    string Render(ReleaseReportDocument report);
}
