using DevReleaseReporter.Core.Models;

namespace DevReleaseReporter.Core.Classification;

public interface IReleaseClassifier
{
    ReleaseClassificationResult Classify(ReleaseDataSet releaseData);

    WorkCategory ClassifyCategory(string? primaryText, string? secondaryText = null);
}

