using ClosedXML.Excel;
using DevReleaseReporter.Core.Models;

namespace DevReleaseReporter.Core.Reporting;

public sealed class ExcelReleaseReportRenderer
{
    private static readonly XLColor AccentColor = XLColor.FromHtml("#2563EB");
    private static readonly XLColor AccentTextColor = XLColor.White;
    private static readonly XLColor HeaderFillColor = XLColor.FromHtml("#DBEAFE");
    private static readonly XLColor MutedFillColor = XLColor.FromHtml("#F8FAFC");
    private static readonly XLColor BorderColor = XLColor.FromHtml("#CBD5E1");

    public void Save(ReleaseReport report, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        using var workbook = new XLWorkbook();
        workbook.Style.Font.FontName = "Segoe UI";
        workbook.Style.Font.FontSize = 11;

        BuildOverviewWorksheet(workbook, report);
        BuildReleaseItemsWorksheet(workbook, report);

        if (report.TechnicalAppendix.Count > 0)
        {
            BuildAppendixWorksheet(workbook, report);
        }

        workbook.SaveAs(outputPath);
    }

    private static void BuildOverviewWorksheet(XLWorkbook workbook, ReleaseReport report)
    {
        var worksheet = workbook.Worksheets.Add("Overview");
        var row = 1;

        row = WriteWorksheetTitle(
            worksheet,
            row,
            $"{report.Metadata.ProjectName} release report",
            $"{report.Metadata.RepositoryName} activity translated into a stakeholder-friendly release summary.");
        row++;

        row = WriteSectionHeading(worksheet, row, "Report details");
        row = WriteKeyValue(worksheet, row, "Organization", report.Metadata.OrganizationName);
        row = WriteKeyValue(worksheet, row, "Project", report.Metadata.ProjectName);
        row = WriteKeyValue(worksheet, row, "Repository", report.Metadata.RepositoryName);
        row = WriteKeyValue(worksheet, row, "Reporting period", $"{FormatDate(report.Metadata.FromDate)} to {FormatDate(report.Metadata.ToDate)}");
        row = WriteKeyValue(worksheet, row, "Branch", report.Metadata.BranchName ?? "All branches");
        row = WriteKeyValue(worksheet, row, "Contributor", report.Metadata.ContributorName ?? "All contributors");
        row = WriteKeyValue(worksheet, row, "Generated", FormatTimestamp(report.Metadata.GeneratedAt));
        row++;

        row = WriteSectionHeading(worksheet, row, "Summary");
        var metricsStartRow = row;
        row = WriteSummaryMetric(worksheet, row, "Total items delivered", report.Summary.TotalItems);
        row = WriteSummaryMetric(worksheet, row, "Features", report.Summary.FeatureCount);
        row = WriteSummaryMetric(worksheet, row, "Bug fixes", report.Summary.BugFixCount);
        row = WriteSummaryMetric(worksheet, row, "Improvements", report.Summary.ImprovementsCount);
        row = WriteSummaryMetric(worksheet, row, "Pull requests", report.Summary.PullRequestCount);
        row = WriteSummaryMetric(worksheet, row, "Commits reviewed", report.Summary.CommitCount);
        row = WriteSummaryMetric(worksheet, row, "Standalone work items", report.Summary.WorkItemCount);
        StyleSummaryMetrics(worksheet, metricsStartRow, row - 1);
        row++;

        row = WriteSectionHeading(worksheet, row, "Highlights");
        if (report.Highlights.Count == 0)
        {
            worksheet.Cell(row, 1).Value = "No standout highlights were identified for the selected scope.";
            worksheet.Range(row, 1, row, 4).Merge();
            StyleMutedNote(worksheet.Cell(row, 1));
            row++;
        }
        else
        {
            var headerRow = row;
            WriteHeaders(worksheet, headerRow, "Category", "Title", "Summary", "Source");
            row++;

            foreach (var highlight in report.Highlights)
            {
                worksheet.Cell(row, 1).Value = FormatCategory(highlight.Category);
                worksheet.Cell(row, 2).Value = highlight.Title;
                worksheet.Cell(row, 3).Value = highlight.Summary ?? string.Empty;
                worksheet.Cell(row, 4).Value = FormatSourceReference(highlight.Source, highlight.SourceIdentifier);
                row++;
            }

            StyleDataRange(worksheet, headerRow, row - 1, 4, "TableStyleMedium2");
        }

        ApplyOverviewLayout(worksheet);
    }

    private static void BuildReleaseItemsWorksheet(XLWorkbook workbook, ReleaseReport report)
    {
        var worksheet = workbook.Worksheets.Add("Release items");
        WriteWorksheetTitle(
            worksheet,
            1,
            "Release items",
            "All report items in one sheet so stakeholders can scan or filter without switching tabs.");

        const int headerRow = 4;
        WriteHeaders(
            worksheet,
            headerRow,
            "Section",
            "Category",
            "Title",
            "Summary",
            "Source",
            "Related commits",
            "Related work items");

        var row = headerRow + 1;
        foreach (var section in report.Sections)
        {
            foreach (var item in section.Items)
            {
                worksheet.Cell(row, 1).Value = section.Title;
                worksheet.Cell(row, 2).Value = FormatCategory(item.Category);
                worksheet.Cell(row, 3).Value = item.Title;
                worksheet.Cell(row, 4).Value = item.Summary ?? string.Empty;
                WriteSourceCell(worksheet.Cell(row, 5), item.Source, item.SourceIdentifier, item.SourceUrl);
                worksheet.Cell(row, 6).Value = string.Join(", ", item.RelatedCommitIds);
                worksheet.Cell(row, 7).Value = FormatWorkItems(item.RelatedWorkItems);
                row++;
            }
        }

        if (row == headerRow + 1)
        {
            worksheet.Cell(row, 1).Value = "No release items are available for the selected scope.";
            worksheet.Range(row, 1, row, 7).Merge();
            StyleMutedNote(worksheet.Cell(row, 1));
            row++;
        }
        else
        {
            StyleDataRange(worksheet, headerRow, row - 1, 7, "TableStyleMedium9");
        }

        ApplyItemsLayout(worksheet, 7);
    }

    private static void BuildAppendixWorksheet(XLWorkbook workbook, ReleaseReport report)
    {
        var worksheet = workbook.Worksheets.Add("Appendix");
        WriteWorksheetTitle(
            worksheet,
            1,
            "Technical appendix",
            "Supporting technical items and traceability details for engineering audiences.");

        const int headerRow = 4;
        WriteHeaders(
            worksheet,
            headerRow,
            "Title",
            "Category",
            "Source",
            "Related commits",
            "Related work items");

        var row = headerRow + 1;
        foreach (var item in report.TechnicalAppendix)
        {
            worksheet.Cell(row, 1).Value = item.Title;
            worksheet.Cell(row, 2).Value = FormatCategory(item.Category);
            WriteSourceCell(worksheet.Cell(row, 3), item.Source, item.SourceIdentifier, item.SourceUrl);
            worksheet.Cell(row, 4).Value = string.Join(", ", item.RelatedCommitIds);
            worksheet.Cell(row, 5).Value = FormatWorkItems(item.RelatedWorkItems);
            row++;
        }

        StyleDataRange(worksheet, headerRow, row - 1, 5, "TableStyleMedium4");
        ApplyItemsLayout(worksheet, 5);
    }

    private static int WriteKeyValue(IXLWorksheet worksheet, int row, string key, string value)
    {
        worksheet.Cell(row, 1).Value = key;
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        worksheet.Cell(row, 2).Value = value;
        worksheet.Range(row, 1, row, 2).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        worksheet.Range(row, 1, row, 2).Style.Border.BottomBorderColor = BorderColor;
        return row + 1;
    }

    private static int WriteWorksheetTitle(IXLWorksheet worksheet, int row, string title, string subtitle)
    {
        worksheet.Cell(row, 1).Value = title;
        worksheet.Range(row, 1, row, 6).Merge();
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        worksheet.Cell(row, 1).Style.Font.FontSize = 18;
        worksheet.Cell(row, 1).Style.Font.FontColor = AccentTextColor;
        worksheet.Cell(row, 1).Style.Fill.BackgroundColor = AccentColor;
        worksheet.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        worksheet.Cell(row, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.Row(row).Height = 28;
        row++;

        worksheet.Cell(row, 1).Value = subtitle;
        worksheet.Range(row, 1, row, 6).Merge();
        worksheet.Cell(row, 1).Style.Fill.BackgroundColor = HeaderFillColor;
        worksheet.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#334155");
        worksheet.Cell(row, 1).Style.Alignment.WrapText = true;
        worksheet.Row(row).Height = 22;
        return row + 1;
    }

    private static int WriteSectionHeading(IXLWorksheet worksheet, int row, string heading)
    {
        worksheet.Cell(row, 1).Value = heading;
        worksheet.Range(row, 1, row, 4).Merge();
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        worksheet.Cell(row, 1).Style.Font.FontSize = 13;
        worksheet.Cell(row, 1).Style.Fill.BackgroundColor = MutedFillColor;
        worksheet.Cell(row, 1).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        worksheet.Cell(row, 1).Style.Border.BottomBorderColor = BorderColor;
        return row + 1;
    }

    private static int WriteSummaryMetric(IXLWorksheet worksheet, int row, string key, int value)
    {
        worksheet.Cell(row, 1).Value = key;
        worksheet.Cell(row, 2).Value = value;
        return row + 1;
    }

    private static void StyleSummaryMetrics(IXLWorksheet worksheet, int startRow, int endRow)
    {
        for (var row = startRow; row <= endRow; row++)
        {
            var isAlternate = (row - startRow) % 2 == 0;
            var range = worksheet.Range(row, 1, row, 2);
            range.Style.Fill.BackgroundColor = isAlternate ? XLColor.White : MutedFillColor;
            range.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            range.Style.Border.BottomBorderColor = BorderColor;
        }

        worksheet.Range(startRow, 1, endRow, 1).Style.Font.Bold = true;
        worksheet.Range(startRow, 2, endRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static void WriteHeaders(IXLWorksheet worksheet, int row, params string[] headers)
    {
        for (var column = 0; column < headers.Length; column++)
        {
            worksheet.Cell(row, column + 1).Value = headers[column];
        }

        StyleHeaderRow(worksheet, row, headers.Length);
    }

    private static void StyleHeaderRow(IXLWorksheet worksheet, int row, int columnCount)
    {
        var range = worksheet.Range(row, 1, row, columnCount);
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.FromHtml("#1E3A8A");
        range.Style.Fill.BackgroundColor = HeaderFillColor;
        range.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        range.Style.Border.BottomBorderColor = BorderColor;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void StyleDataRange(IXLWorksheet worksheet, int headerRow, int endRow, int columnCount, string themeName)
    {
        if (endRow <= headerRow)
        {
            return;
        }

        var range = worksheet.Range(headerRow, 1, endRow, columnCount);
        var table = range.CreateTable();
        table.Theme = XLTableTheme.FromName(themeName);
        table.ShowAutoFilter = true;
        table.ShowRowStripes = true;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        range.Style.Alignment.WrapText = true;
    }

    private static void WriteSourceCell(IXLCell cell, ReportItemSource source, string identifier, string? sourceUrl)
    {
        cell.Value = FormatSourceReference(source, identifier);
        if (!string.IsNullOrWhiteSpace(sourceUrl))
        {
            cell.SetHyperlink(new XLHyperlink(sourceUrl));
            cell.Style.Font.FontColor = AccentColor;
            cell.Style.Font.Underline = XLFontUnderlineValues.Single;
        }
    }

    private static void StyleMutedNote(IXLCell cell)
    {
        cell.Style.Font.Italic = true;
        cell.Style.Font.FontColor = XLColor.FromHtml("#64748B");
        cell.Style.Fill.BackgroundColor = MutedFillColor;
        cell.Style.Alignment.WrapText = true;
    }

    private static void ApplyOverviewLayout(IXLWorksheet worksheet)
    {
        worksheet.SheetView.FreezeRows(2);
        worksheet.Column(1).Width = 24;
        worksheet.Column(2).Width = 20;
        worksheet.Column(3).Width = 18;
        worksheet.Column(4).Width = 42;
        worksheet.Columns().Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
    }

    private static void ApplyItemsLayout(IXLWorksheet worksheet, int columnCount)
    {
        worksheet.SheetView.FreezeRows(4);
        worksheet.Columns().Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        worksheet.Columns().Style.Alignment.WrapText = true;

        if (columnCount >= 1) worksheet.Column(1).Width = 24;
        if (columnCount >= 2) worksheet.Column(2).Width = 18;
        if (columnCount >= 3) worksheet.Column(3).Width = 40;
        if (columnCount >= 4) worksheet.Column(4).Width = 55;
        if (columnCount >= 5) worksheet.Column(5).Width = 18;
        if (columnCount >= 6) worksheet.Column(6).Width = 24;
        if (columnCount >= 7) worksheet.Column(7).Width = 32;
    }

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

    private static string FormatSourceType(ReportItemSource source) =>
        source switch
        {
            ReportItemSource.PullRequest => "Pull request",
            ReportItemSource.Commit => "Commit",
            ReportItemSource.WorkItem => "Work item",
            _ => source.ToString(),
        };

    private static string FormatSourceReference(ReportItemSource source, string identifier) =>
        source switch
        {
            ReportItemSource.PullRequest => $"Pull request #{identifier}",
            ReportItemSource.Commit => $"Commit {identifier}",
            ReportItemSource.WorkItem => $"Work item #{identifier}",
            _ => identifier,
        };

    private static string FormatWorkItems(IReadOnlyList<WorkItem> workItems) =>
        string.Join(", ", workItems.Select(static workItem => $"#{workItem.Id} {workItem.Title}"));

    private static string FormatDate(DateTimeOffset value) =>
        value.ToString("dd MMM yyyy");

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToString("dd MMM yyyy HH:mm 'UTC'zzz");
}
