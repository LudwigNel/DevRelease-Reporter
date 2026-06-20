using DevReleaseReporter.Domain.Models;

namespace DevReleaseReporter.Application.Abstractions;

public interface IReleaseDataSource
{
    Task<ReleaseDataSnapshot> GetSnapshotAsync(ReleaseQuery query, CancellationToken cancellationToken = default);
}
