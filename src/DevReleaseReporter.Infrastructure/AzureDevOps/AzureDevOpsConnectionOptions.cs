namespace DevReleaseReporter.Infrastructure.AzureDevOps;

public sealed record AzureDevOpsConnectionOptions
{
    public required Uri OrganizationUrl { get; init; }

    public required string ProjectName { get; init; }

    public required string RepositoryName { get; init; }

    public required string PersonalAccessToken { get; init; }
}

