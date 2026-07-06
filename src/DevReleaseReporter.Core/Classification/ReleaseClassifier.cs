using System.Globalization;
using System.Text.RegularExpressions;
using DevReleaseReporter.Core.Models;

namespace DevReleaseReporter.Core.Classification;

public sealed partial class ReleaseClassifier : IReleaseClassifier
{
    private static readonly Regex ConventionalPrefixRegex = new(
        @"^\s*(?<type>feat|fix|refactor|perf)(\([^)]+\))?\s*:\s*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public ReleaseClassificationResult Classify(ReleaseDataSet releaseData)
    {
        ArgumentNullException.ThrowIfNull(releaseData);

        var items = new List<ReportItem>();

        foreach (var pullRequest in releaseData.PullRequests)
        {
            items.Add(CreatePullRequestItem(pullRequest));
        }

        var pullRequestCommitIds = releaseData.PullRequests
            .SelectMany(static pullRequest => pullRequest.CommitIds)
            .Where(static commitId => !string.IsNullOrWhiteSpace(commitId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var commit in releaseData.Commits)
        {
            if (!pullRequestCommitIds.Contains(commit.Id))
            {
                items.Add(CreateCommitItem(commit));
            }
        }

        var linkedWorkItemIds = releaseData.PullRequests
            .SelectMany(static pullRequest => pullRequest.WorkItems)
            .Select(static workItem => workItem.Id)
            .ToHashSet();

        foreach (var workItem in releaseData.WorkItems)
        {
            if (!linkedWorkItemIds.Contains(workItem.Id))
            {
                items.Add(CreateWorkItemItem(workItem));
            }
        }

        var orderedItems = OrderItems(items);
        var sections = BuildSections(orderedItems);
        var summary = BuildSummary(orderedItems, releaseData);

        return new ReleaseClassificationResult
        {
            Items = orderedItems,
            Sections = sections,
            Summary = summary,
        };
    }

    public WorkCategory ClassifyCategory(string? primaryText, string? secondaryText = null)
    {
        var category = TryClassify(primaryText);
        if (category.HasValue)
        {
            return category.Value;
        }

        category = TryClassify(secondaryText);
        return category ?? WorkCategory.Other;
    }

    private static IReadOnlyList<ReportItem> OrderItems(IEnumerable<ReportItem> items) =>
        items.OrderBy(static item => GetSectionOrder(item.Section))
            .ThenBy(static item => GetCategoryOrder(item.Category))
            .ThenBy(static item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<ReportSection> BuildSections(IReadOnlyList<ReportItem> items) =>
        Enum.GetValues<ReportSectionKind>()
            .Select(section => new ReportSection
            {
                Kind = section,
                Title = GetSectionTitle(section),
                Items = items.Where(item => item.Section == section).ToArray(),
            })
            .ToArray();

    private static ReportSummary BuildSummary(IReadOnlyList<ReportItem> items, ReleaseDataSet releaseData) =>
        new()
        {
            TotalItems = items.Count,
            FeatureCount = items.Count(item => item.Category == WorkCategory.Feature),
            BugFixCount = items.Count(item => item.Category == WorkCategory.BugFix),
            TechnicalCount = items.Count(item => item.Category == WorkCategory.Technical),
            PerformanceCount = items.Count(item => item.Category == WorkCategory.Performance),
            OtherCount = items.Count(item => item.Category == WorkCategory.Other),
            FeaturesDeliveredCount = items.Count(item => item.Section == ReportSectionKind.FeaturesDelivered),
            BugsFixedCount = items.Count(item => item.Section == ReportSectionKind.BugsFixed),
            ImprovementsCount = items.Count(item => item.Section == ReportSectionKind.Improvements),
            PullRequestCount = releaseData.PullRequests.Count,
            CommitCount = releaseData.Commits.Count,
            WorkItemCount = releaseData.WorkItems.Count,
        };

    private ReportItem CreatePullRequestItem(PullRequest pullRequest)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);

        var category = ClassifyCategory(pullRequest.Title, pullRequest.Description);
        var title = BuildBusinessTitle(
            pullRequest.Title,
            fallbackTitle: $"Pull request #{pullRequest.Id}");

        return new ReportItem
        {
            Title = title,
            Summary = NormalizeSummary(pullRequest.Description),
            Category = category,
            Section = MapToSection(category),
            Source = ReportItemSource.PullRequest,
            SourceIdentifier = pullRequest.Id.ToString(CultureInfo.InvariantCulture),
            SourceUrl = NormalizeText(pullRequest.Url),
            RelatedCommitIds = pullRequest.CommitIds
                .Where(static commitId => !string.IsNullOrWhiteSpace(commitId))
                .ToArray(),
            RelatedWorkItems = pullRequest.WorkItems,
        };
    }

    private ReportItem CreateCommitItem(Commit commit)
    {
        ArgumentNullException.ThrowIfNull(commit);

        var messageParts = SplitMessage(commit.Message);
        var category = ClassifyCategory(messageParts.Title, commit.Description ?? messageParts.Body);
        var title = BuildBusinessTitle(
            messageParts.Title,
            fallbackTitle: $"Commit {ShortenCommitId(commit.Id)}");

        return new ReportItem
        {
            Title = title,
            Summary = NormalizeSummary(commit.Description ?? messageParts.Body),
            Category = category,
            Section = MapToSection(category),
            Source = ReportItemSource.Commit,
            SourceIdentifier = commit.Id,
            SourceUrl = NormalizeText(commit.Url),
            RelatedCommitIds = new[] { commit.Id },
        };
    }

    private ReportItem CreateWorkItemItem(WorkItem workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);

        var category = ClassifyCategory(workItem.Title, workItem.Description);
        var title = BuildBusinessTitle(
            workItem.Title,
            fallbackTitle: $"Work item #{workItem.Id}");

        return new ReportItem
        {
            Title = title,
            Summary = NormalizeSummary(workItem.Description),
            Category = category,
            Section = MapToSection(category),
            Source = ReportItemSource.WorkItem,
            SourceIdentifier = workItem.Id.ToString(CultureInfo.InvariantCulture),
            SourceUrl = NormalizeText(workItem.Url),
        };
    }

    private static WorkCategory? TryClassify(string? text)
    {
        var normalized = NormalizeText(text);
        if (normalized is null)
        {
            return null;
        }

        var match = ConventionalPrefixRegex.Match(normalized);
        if (!match.Success)
        {
            return null;
        }

        var type = match.Groups["type"].Value;
        return type.ToLowerInvariant() switch
        {
            "feat" => WorkCategory.Feature,
            "fix" => WorkCategory.BugFix,
            "refactor" => WorkCategory.Technical,
            "perf" => WorkCategory.Performance,
            _ => null,
        };
    }

    private static ReportSectionKind MapToSection(WorkCategory category) =>
        category switch
        {
            WorkCategory.Feature => ReportSectionKind.FeaturesDelivered,
            WorkCategory.BugFix => ReportSectionKind.BugsFixed,
            _ => ReportSectionKind.Improvements,
        };

    private static string GetSectionTitle(ReportSectionKind section) =>
        section switch
        {
            ReportSectionKind.FeaturesDelivered => "Features delivered",
            ReportSectionKind.BugsFixed => "Bugs fixed",
            ReportSectionKind.Improvements => "Improvements",
            _ => throw new ArgumentOutOfRangeException(nameof(section), section, null),
        };

    private static int GetSectionOrder(ReportSectionKind section) =>
        section switch
        {
            ReportSectionKind.FeaturesDelivered => 0,
            ReportSectionKind.BugsFixed => 1,
            ReportSectionKind.Improvements => 2,
            _ => int.MaxValue,
        };

    private static int GetCategoryOrder(WorkCategory category) =>
        category switch
        {
            WorkCategory.Feature => 0,
            WorkCategory.BugFix => 1,
            WorkCategory.Performance => 2,
            WorkCategory.Technical => 3,
            WorkCategory.Other => 4,
            _ => int.MaxValue,
        };

    private static string BuildBusinessTitle(string? text, string fallbackTitle)
    {
        var normalized = NormalizeText(text);
        if (normalized is null)
        {
            return fallbackTitle;
        }

        var cleaned = ConventionalPrefixRegex.Replace(normalized, string.Empty).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? fallbackTitle : cleaned;
    }

    private static string? NormalizeSummary(string? text)
    {
        var normalized = NormalizeText(text);
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static (string Title, string? Body) SplitMessage(string message)
    {
        var normalized = NormalizeText(message);
        if (normalized is null)
        {
            return ("", null);
        }

        var lines = normalized
            .Split(["\r\n", "\n"], StringSplitOptions.None | StringSplitOptions.TrimEntries)
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (lines.Length == 0)
        {
            return ("", null);
        }

        if (lines.Length == 1)
        {
            return (lines[0], null);
        }

        return (lines[0], string.Join(Environment.NewLine, lines.Skip(1)));
    }

    private static string ShortenCommitId(string commitId)
    {
        var normalized = NormalizeText(commitId);
        if (normalized is null)
        {
            return "unknown";
        }

        return normalized.Length <= 8 ? normalized : normalized[..8];
    }

    private static string? NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return text.Trim();
    }
}

