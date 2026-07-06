namespace DevReleaseReporter.Infrastructure.AzureDevOps;

public sealed class AzureDevOpsClientException : Exception
{
    public AzureDevOpsClientException(string message)
        : base(message)
    {
    }

    public AzureDevOpsClientException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

