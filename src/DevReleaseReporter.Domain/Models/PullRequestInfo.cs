namespace DevReleaseReporter.Domain.Models;

public sealed record PullRequestInfo(
    int Id,
    string Title,
    string Status,
    string Author,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt);
