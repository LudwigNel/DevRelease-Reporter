namespace DevReleaseReporter.Infrastructure.AzureDevOps;

public sealed record AzureDevOpsOptions(
    string Organization,
    string Project,
    string Repository,
    string PersonalAccessToken);
