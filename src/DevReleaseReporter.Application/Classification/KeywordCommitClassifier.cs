using DevReleaseReporter.Application.Abstractions;
using DevReleaseReporter.Domain.Models;
using System.Text.RegularExpressions;

namespace DevReleaseReporter.Application.Classification;

public sealed class KeywordCommitClassifier : ICommitClassifier
{
    private static readonly IReadOnlyDictionary<ChangeCategory, string[]> Keywords =
        new Dictionary<ChangeCategory, string[]>
        {
            [ChangeCategory.Feature] = ["feat", "feature", "add", "implement"],
            [ChangeCategory.BugFix] = ["fix", "bug", "patch", "hotfix"],
            [ChangeCategory.Improvement] = ["improve", "enhance", "refactor", "optimize"],
            [ChangeCategory.Documentation] = ["docs", "doc", "readme"],
            [ChangeCategory.Maintenance] = ["chore", "build", "deps", "dependency", "ci"]
        };

    private static readonly Regex TokenRegex = new("[a-z0-9]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public ChangeCategory Classify(CommitInfo commit)
    {
        if (string.IsNullOrWhiteSpace(commit.Message))
        {
            return ChangeCategory.Unknown;
        }

        var tokens = TokenRegex
            .Matches(commit.Message)
            .Select(match => match.Value.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (category, words) in Keywords)
        {
            if (words.Any(tokens.Contains))
            {
                return category;
            }
        }

        return ChangeCategory.Unknown;
    }
}
