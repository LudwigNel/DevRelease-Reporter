namespace DevReleaseReporter.Domain.Models;

public sealed record WorkItemInfo(
    int Id,
    string Type,
    string Title,
    string State);
