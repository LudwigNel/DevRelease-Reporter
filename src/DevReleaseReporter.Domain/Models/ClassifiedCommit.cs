namespace DevReleaseReporter.Domain.Models;

public sealed record ClassifiedCommit(CommitInfo Commit, ChangeCategory Category);
