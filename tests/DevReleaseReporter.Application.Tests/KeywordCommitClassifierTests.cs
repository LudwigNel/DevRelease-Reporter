using DevReleaseReporter.Application.Classification;
using DevReleaseReporter.Domain.Models;

namespace DevReleaseReporter.Application.Tests;

public sealed class KeywordCommitClassifierTests
{
    private readonly KeywordCommitClassifier _classifier = new();

    [Theory]
    [InlineData("feat: add release report", ChangeCategory.Feature)]
    [InlineData("fix: null reference", ChangeCategory.BugFix)]
    [InlineData("refactor parser", ChangeCategory.Improvement)]
    [InlineData("docs: update readme", ChangeCategory.Documentation)]
    [InlineData("chore: bump dependencies", ChangeCategory.Maintenance)]
    [InlineData("misc update", ChangeCategory.Unknown)]
    public void Classify_MapsCommonCommitKeywords(string message, ChangeCategory expected)
    {
        var commit = new CommitInfo("1", message, "author", DateTimeOffset.UtcNow, []);

        var category = _classifier.Classify(commit);

        Assert.Equal(expected, category);
    }
}
