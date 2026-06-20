namespace DevReleaseReporter.Domain.Models;

public sealed record CommitInfo(
    string Id,
    string Message,
    string Author,
    DateTimeOffset Timestamp,
    IReadOnlyList<int> LinkedWorkItemIds);
