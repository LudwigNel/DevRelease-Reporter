namespace DevReleaseReporter.Infrastructure.AzureDevOps;

public sealed record AzureDevOpsReleaseQuery
{
    public required DateTimeOffset FromDate { get; init; }

    public required DateTimeOffset ToDate { get; init; }

    public string? BranchName { get; init; }

    public IProgress<string>? Progress { get; init; }

    public bool IncludeWorkItems { get; init; } = true;

    public int PageSize { get; init; } = 100;
}
