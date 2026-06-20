using DevReleaseReporter.Domain.Models;

namespace DevReleaseReporter.Application.Abstractions;

public interface ICommitClassifier
{
    ChangeCategory Classify(CommitInfo commit);
}
