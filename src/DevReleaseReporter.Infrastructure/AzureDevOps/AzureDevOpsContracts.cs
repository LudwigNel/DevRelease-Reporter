using System.Text.Json.Serialization;

namespace DevReleaseReporter.Infrastructure.AzureDevOps;

internal sealed record AzureDevOpsCollectionResponse<T>
{
    [JsonPropertyName("value")]
    public IReadOnlyList<T> Value { get; init; } = Array.Empty<T>();
}

internal sealed record GitCommitDto
{
    [JsonPropertyName("commitId")]
    public required string CommitId { get; init; }

    [JsonPropertyName("comment")]
    public string? Comment { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("remoteUrl")]
    public string? RemoteUrl { get; init; }

    [JsonPropertyName("author")]
    public IdentityDateDto? Author { get; init; }

    [JsonPropertyName("workItems")]
    public IReadOnlyList<WorkItemReferenceDto>? WorkItems { get; init; }
}

internal sealed record GitPullRequestDto
{
    [JsonPropertyName("pullRequestId")]
    public required int PullRequestId { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("remoteUrl")]
    public string? RemoteUrl { get; init; }

    [JsonPropertyName("closedDate")]
    public DateTimeOffset? ClosedDate { get; init; }

    [JsonPropertyName("sourceRefName")]
    public string? SourceRefName { get; init; }

    [JsonPropertyName("targetRefName")]
    public string? TargetRefName { get; init; }

    [JsonPropertyName("createdBy")]
    public IdentityDto? CreatedBy { get; init; }
}

internal sealed record WorkItemReferenceDto
{
    [JsonPropertyName("id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public required int Id { get; init; }
}

internal sealed record WorkItemBatchRequestDto
{
    [JsonPropertyName("ids")]
    public required IReadOnlyList<int> Ids { get; init; }

    [JsonPropertyName("fields")]
    public required IReadOnlyList<string> Fields { get; init; }
}

internal sealed record WorkItemBatchResponseDto
{
    [JsonPropertyName("value")]
    public IReadOnlyList<WorkItemDto> Value { get; init; } = Array.Empty<WorkItemDto>();
}

internal sealed record WorkItemDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("fields")]
    public IReadOnlyDictionary<string, object?> Fields { get; init; } = new Dictionary<string, object?>();
}

internal sealed record IdentityDto
{
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("uniqueName")]
    public string? UniqueName { get; init; }
}

internal sealed record IdentityDateDto
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("date")]
    public DateTimeOffset? Date { get; init; }
}
