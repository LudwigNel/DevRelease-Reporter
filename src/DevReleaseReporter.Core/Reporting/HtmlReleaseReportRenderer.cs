using System.Net;
using System.Text;
using DevReleaseReporter.Core.Models;

namespace DevReleaseReporter.Core.Reporting;

public sealed class HtmlReleaseReportRenderer : IReleaseReportRenderer
{
    public string Render(ReleaseReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var html = new StringBuilder();

        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("  <meta charset=\"utf-8\">");
        html.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.AppendLine($"  <title>{Encode(report.Metadata.ProjectName)} release report</title>");
        html.AppendLine("  <style>");
        html.AppendLine("    body { font-family: Segoe UI, Arial, sans-serif; margin: 0; background: #f5f7fb; color: #1f2937; }");
        html.AppendLine("    main { max-width: 1100px; margin: 0 auto; padding: 32px 24px 64px; }");
        html.AppendLine("    .hero, section, .summary-card { background: #ffffff; border: 1px solid #dbe3f0; border-radius: 16px; box-shadow: 0 10px 30px rgba(15, 23, 42, 0.05); }");
        html.AppendLine("    .hero { padding: 28px; margin-bottom: 24px; }");
        html.AppendLine("    .hero h1, section h2, section h3 { margin-top: 0; }");
        html.AppendLine("    .meta { color: #475569; margin-top: 12px; line-height: 1.6; }");
        html.AppendLine("    .summary-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(170px, 1fr)); gap: 16px; margin: 0 0 24px; }");
        html.AppendLine("    .summary-card { padding: 18px; }");
        html.AppendLine("    .summary-card .label { display: block; color: #475569; font-size: 0.95rem; }");
        html.AppendLine("    .summary-card .value { display: block; font-size: 2rem; font-weight: 700; margin-top: 8px; }");
        html.AppendLine("    section { padding: 24px; margin-top: 24px; }");
        html.AppendLine("    ul { margin: 0; padding-left: 20px; }");
        html.AppendLine("    li + li { margin-top: 12px; }");
        html.AppendLine("    .muted { color: #64748b; }");
        html.AppendLine("    .pill { display: inline-block; padding: 4px 10px; border-radius: 999px; font-size: 0.85rem; font-weight: 600; background: #e2e8f0; color: #334155; margin-right: 8px; }");
        html.AppendLine("    .source { color: #475569; font-size: 0.95rem; }");
        html.AppendLine("    .appendix-item { padding: 16px 0; border-top: 1px solid #e2e8f0; }");
        html.AppendLine("    .appendix-item:first-of-type { border-top: 0; padding-top: 0; }");
        html.AppendLine("    a { color: #2563eb; text-decoration: none; }");
        html.AppendLine("    a:hover { text-decoration: underline; }");
        html.AppendLine("  </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("<main>");

        RenderHero(report, html);
        RenderExecutiveSummary(report, html);
        RenderHighlights(report, html);
        RenderDetailedChanges(report, html);

        if (report.TechnicalAppendix.Count > 0)
        {
            RenderTechnicalAppendix(report, html);
        }

        html.AppendLine("</main>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }

    private static void RenderHero(ReleaseReport report, StringBuilder html)
    {
        html.AppendLine("  <section class=\"hero\">");
        html.AppendLine($"    <h1>{Encode(report.Metadata.ProjectName)} release report</h1>");
        html.AppendLine($"    <p>{Encode(report.Metadata.RepositoryName)} activity translated into a business-friendly summary for stakeholders.</p>");
        html.AppendLine("    <div class=\"meta\">");
        html.AppendLine($"      <div><strong>Organization:</strong> {Encode(report.Metadata.OrganizationName)}</div>");
        html.AppendLine($"      <div><strong>Repository:</strong> {Encode(report.Metadata.RepositoryName)}</div>");
        html.AppendLine($"      <div><strong>Reporting period:</strong> {Encode(FormatDate(report.Metadata.FromDate))} to {Encode(FormatDate(report.Metadata.ToDate))}</div>");
        if (!string.IsNullOrWhiteSpace(report.Metadata.BranchName))
        {
            html.AppendLine($"      <div><strong>Branch:</strong> {Encode(report.Metadata.BranchName)}</div>");
        }

        if (!string.IsNullOrWhiteSpace(report.Metadata.ContributorName))
        {
            html.AppendLine($"      <div><strong>Contributor:</strong> {Encode(report.Metadata.ContributorName)}</div>");
        }

        html.AppendLine($"      <div><strong>Generated:</strong> {Encode(FormatTimestamp(report.Metadata.GeneratedAt))}</div>");
        html.AppendLine("    </div>");
        html.AppendLine("  </section>");
    }

    private static void RenderExecutiveSummary(ReleaseReport report, StringBuilder html)
    {
        html.AppendLine("  <section>");
        html.AppendLine("    <h2>Executive summary</h2>");
        html.AppendLine("    <div class=\"summary-grid\">");
        RenderSummaryCard("Total items delivered", report.Summary.TotalItems, html);
        RenderSummaryCard("Features", report.Summary.FeatureCount, html);
        RenderSummaryCard("Bug fixes", report.Summary.BugFixCount, html);
        RenderSummaryCard("Improvements", report.Summary.ImprovementsCount, html);
        RenderSummaryCard("Pull requests", report.Summary.PullRequestCount, html);
        RenderSummaryCard("Commits reviewed", report.Summary.CommitCount, html);
        html.AppendLine("    </div>");
        html.AppendLine("    <p class=\"muted\">This report groups engineering activity into stakeholder-readable outcomes so business teams can quickly understand what changed without reading commit history.</p>");
        html.AppendLine("  </section>");
    }

    private static void RenderHighlights(ReleaseReport report, StringBuilder html)
    {
        html.AppendLine("  <section>");
        html.AppendLine("    <h2>Highlights</h2>");

        if (report.Highlights.Count == 0)
        {
            html.AppendLine("    <p class=\"muted\">No standout features or bug fixes were identified in the selected scope.</p>");
            html.AppendLine("  </section>");
            return;
        }

        html.AppendLine("    <ul>");
        foreach (var highlight in report.Highlights)
        {
            html.AppendLine("      <li>");
            html.AppendLine($"        <span class=\"pill\">{Encode(FormatCategory(highlight.Category))}</span><strong>{Encode(highlight.Title)}</strong>");
            if (!string.IsNullOrWhiteSpace(highlight.Summary))
            {
                html.AppendLine($"        <div class=\"muted\">{Encode(highlight.Summary)}</div>");
            }

            html.AppendLine($"        <div class=\"source\">Source: {Encode(FormatSource(highlight.Source, highlight.SourceIdentifier))}</div>");
            html.AppendLine("      </li>");
        }

        html.AppendLine("    </ul>");
        html.AppendLine("  </section>");
    }

    private static void RenderDetailedChanges(ReleaseReport report, StringBuilder html)
    {
        html.AppendLine("  <section>");
        html.AppendLine("    <h2>Detailed changes</h2>");

        foreach (var section in report.Sections)
        {
            html.AppendLine($"    <h3>{Encode(section.Title)}</h3>");

            if (section.Items.Count == 0)
            {
                html.AppendLine("    <p class=\"muted\">No items in this category for the selected scope.</p>");
                continue;
            }

            html.AppendLine("    <ul>");
            foreach (var item in section.Items)
            {
                html.AppendLine("      <li>");
                html.AppendLine($"        <strong>{Encode(item.Title)}</strong>");
                if (!string.IsNullOrWhiteSpace(item.Summary))
                {
                    html.AppendLine($"        <div class=\"muted\">{Encode(item.Summary)}</div>");
                }

                html.AppendLine($"        <div class=\"source\">{Encode(FormatCategory(item.Category))} · {Encode(FormatSource(item.Source, item.SourceIdentifier))}</div>");
                html.AppendLine("      </li>");
            }

            html.AppendLine("    </ul>");
        }

        html.AppendLine("  </section>");
    }

    private static void RenderTechnicalAppendix(ReleaseReport report, StringBuilder html)
    {
        html.AppendLine("  <section>");
        html.AppendLine("    <h2>Technical appendix</h2>");

        foreach (var item in report.TechnicalAppendix)
        {
            html.AppendLine("    <div class=\"appendix-item\">");
            html.AppendLine($"      <strong>{Encode(item.Title)}</strong>");
            html.AppendLine($"      <div class=\"source\">{Encode(FormatCategory(item.Category))} · {Encode(FormatSource(item.Source, item.SourceIdentifier))}</div>");

            if (!string.IsNullOrWhiteSpace(item.SourceUrl))
            {
                html.AppendLine($"      <div><a href=\"{EncodeAttribute(item.SourceUrl)}\">Open source item</a></div>");
            }

            if (item.RelatedCommitIds.Count > 0)
            {
                html.AppendLine($"      <div class=\"muted\"><strong>Commit IDs:</strong> {Encode(string.Join(", ", item.RelatedCommitIds))}</div>");
            }

            if (item.RelatedWorkItems.Count > 0)
            {
                var workItems = item.RelatedWorkItems
                    .Select(static workItem => $"#{workItem.Id} {workItem.Title}");
                html.AppendLine($"      <div class=\"muted\"><strong>Work items:</strong> {Encode(string.Join(", ", workItems))}</div>");
            }

            html.AppendLine("    </div>");
        }

        html.AppendLine("  </section>");
    }

    private static void RenderSummaryCard(string label, int value, StringBuilder html)
    {
        html.AppendLine("      <div class=\"summary-card\">");
        html.AppendLine($"        <span class=\"label\">{Encode(label)}</span>");
        html.AppendLine($"        <span class=\"value\">{value}</span>");
        html.AppendLine("      </div>");
    }

    private static string FormatDate(DateTimeOffset value) =>
        value.ToString("dd MMM yyyy");

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToString("dd MMM yyyy HH:mm 'UTC'zzz");

    private static string FormatCategory(WorkCategory category) =>
        category switch
        {
            WorkCategory.Feature => "Feature",
            WorkCategory.BugFix => "Bug fix",
            WorkCategory.Technical => "Technical improvement",
            WorkCategory.Performance => "Performance improvement",
            WorkCategory.Other => "Other",
            _ => category.ToString(),
        };

    private static string FormatSource(ReportItemSource source, string identifier) =>
        source switch
        {
            ReportItemSource.PullRequest => $"Pull request #{identifier}",
            ReportItemSource.Commit => $"Commit {identifier}",
            ReportItemSource.WorkItem => $"Work item #{identifier}",
            _ => identifier,
        };

    private static string Encode(string? value) =>
        WebUtility.HtmlEncode(value ?? string.Empty);

    private static string EncodeAttribute(string? value) =>
        WebUtility.HtmlEncode(value ?? string.Empty);
}
