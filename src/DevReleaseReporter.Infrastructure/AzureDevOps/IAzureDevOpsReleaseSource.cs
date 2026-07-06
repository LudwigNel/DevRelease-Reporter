using DevReleaseReporter.Core.Models;

namespace DevReleaseReporter.Infrastructure.AzureDevOps;

public interface IAzureDevOpsReleaseSource
{
    Task<ReleaseDataSet> GetReleaseDataAsync(
        AzureDevOpsConnectionOptions connectionOptions,
        AzureDevOpsReleaseQuery query,
        CancellationToken cancellationToken = default);
}

