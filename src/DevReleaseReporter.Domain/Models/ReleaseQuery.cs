namespace DevReleaseReporter.Domain.Models;

public sealed record ReleaseQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    int MaxCommits = 1000,
    int MaxPullRequests = 1000,
    bool IncludeWorkItems = true);
