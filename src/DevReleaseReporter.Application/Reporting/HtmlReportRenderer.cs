using System.Net;
using System.Text;
using DevReleaseReporter.Application.Abstractions;
using DevReleaseReporter.Domain.Models;

namespace DevReleaseReporter.Application.Reporting;

public sealed class HtmlReportRenderer : IReportRenderer
{
    public string ContentType => "text/html";

    public string Render(ReleaseReportDocument report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html><head><meta charset=\"utf-8\"/><title>Release Report</title></head><body>");
        builder.AppendLine($"<h1>Release Report ({report.Query.From:yyyy-MM-dd} to {report.Query.To:yyyy-MM-dd})</h1>");
        builder.AppendLine($"<p>Generated: {report.GeneratedAt:O}</p>");

        builder.AppendLine("<h2>Commit classification summary</h2><ul>");
        foreach (var category in Enum.GetValues<ChangeCategory>())
        {
            report.CategorySummary.TryGetValue(category, out var count);
            builder.AppendLine($"<li>{category}: {count}</li>");
        }

        builder.AppendLine("</ul><h2>Commits</h2><ol>");
        foreach (var commit in report.Commits)
        {
            builder.AppendLine(
                $"<li>[{commit.Category}] {WebUtility.HtmlEncode(commit.Commit.Message)} " +
                $"<small>({WebUtility.HtmlEncode(commit.Commit.Author)}, {commit.Commit.Timestamp:yyyy-MM-dd})</small></li>");
        }

        builder.AppendLine("</ol><h2>Pull requests</h2><ul>");
        foreach (var pullRequest in report.PullRequests)
        {
            builder.AppendLine(
                $"<li>#{pullRequest.Id} {WebUtility.HtmlEncode(pullRequest.Title)} " +
                $"({WebUtility.HtmlEncode(pullRequest.Status)})</li>");
        }

        builder.AppendLine("</ul><h2>Work items</h2><ul>");
        foreach (var workItem in report.WorkItems)
        {
            builder.AppendLine(
                $"<li>#{workItem.Id} {WebUtility.HtmlEncode(workItem.Type)}: {WebUtility.HtmlEncode(workItem.Title)} " +
                $"({WebUtility.HtmlEncode(workItem.State)})</li>");
        }

        builder.AppendLine("</ul></body></html>");
        return builder.ToString();
    }
}
