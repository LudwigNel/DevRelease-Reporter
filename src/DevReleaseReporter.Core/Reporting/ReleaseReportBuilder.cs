using DevReleaseReporter.Core.Classification;
using DevReleaseReporter.Core.Models;

namespace DevReleaseReporter.Core.Reporting;

public sealed class ReleaseReportBuilder(IReleaseClassifier releaseClassifier) : IReleaseReportBuilder
{
    public ReleaseReport Build(ReleaseReportRequest request, ReleaseDataSet releaseData)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(releaseData);

        ValidateRequest(request);

        var classification = releaseClassifier.Classify(releaseData);

        return new ReleaseReport
        {
            Metadata = new ReleaseReportMetadata
            {
                OrganizationName = request.OrganizationName.Trim(),
                ProjectName = request.ProjectName.Trim(),
                RepositoryName = request.RepositoryName.Trim(),
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                BranchName = NormalizeText(request.BranchName),
                ContributorName = NormalizeText(request.ContributorName),
                GeneratedAt = request.GeneratedAt,
            },
            Summary = classification.Summary,
            Highlights = BuildHighlights(classification.Items, request.HighlightLimit),
            Sections = classification.Sections,
            TechnicalAppendix = request.IncludeTechnicalAppendix
                ? BuildTechnicalAppendix(classification.Items)
                : Array.Empty<TechnicalAppendixItem>(),
        };
    }

    private static IReadOnlyList<ReleaseHighlight> BuildHighlights(
        IReadOnlyList<ReportItem> items,
        int highlightLimit) =>
        items.Where(static item => item.Category == WorkCategory.Feature || item.Category == WorkCategory.BugFix)
            .Take(highlightLimit)
            .Select(static item => new ReleaseHighlight
            {
                Title = item.Title,
                Summary = item.Summary,
                Category = item.Category,
                Source = item.Source,
                SourceIdentifier = item.SourceIdentifier,
            })
            .ToArray();

    private static IReadOnlyList<TechnicalAppendixItem> BuildTechnicalAppendix(IReadOnlyList<ReportItem> items) =>
        items.Select(static item => new TechnicalAppendixItem
            {
                Title = item.Title,
                Source = item.Source,
                SourceIdentifier = item.SourceIdentifier,
                SourceUrl = item.SourceUrl,
                Category = item.Category,
                RelatedCommitIds = item.RelatedCommitIds,
                RelatedWorkItems = item.RelatedWorkItems,
            })
            .ToArray();

    private static string? NormalizeText(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? null
            : text.Trim();

    private static void ValidateRequest(ReleaseReportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OrganizationName))
        {
            throw new ArgumentException("OrganizationName is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ProjectName))
        {
            throw new ArgumentException("ProjectName is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.RepositoryName))
        {
            throw new ArgumentException("RepositoryName is required.", nameof(request));
        }

        if (request.ToDate < request.FromDate)
        {
            throw new ArgumentException("ToDate must be greater than or equal to FromDate.", nameof(request));
        }

        if (request.HighlightLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "HighlightLimit must be greater than zero.");
        }
    }
}
