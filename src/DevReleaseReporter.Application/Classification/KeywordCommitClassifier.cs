using DevReleaseReporter.Application.Abstractions;
using DevReleaseReporter.Domain.Models;

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

    public ChangeCategory Classify(CommitInfo commit)
    {
        if (string.IsNullOrWhiteSpace(commit.Message))
        {
            return ChangeCategory.Unknown;
        }

        foreach (var (category, words) in Keywords)
        {
            if (words.Any(word => commit.Message.Contains(word, StringComparison.OrdinalIgnoreCase)))
            {
                return category;
            }
        }

        return ChangeCategory.Unknown;
    }
}
