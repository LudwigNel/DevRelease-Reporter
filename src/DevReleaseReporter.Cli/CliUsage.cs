namespace DevReleaseReporter.Cli;

internal static class CliUsage
{
    public static string GetText() =>
        """
        DevRelease Reporter CLI

        Usage:
          DevReleaseReporter.Cli generate-report --organization-url <url> --project <name> --repository <name> --from <date> --to <date> --output <file> [options]

        Required options:
          --organization-url <url>   Azure DevOps organization URL, e.g. https://dev.azure.com/your-org
          --project <name>           Azure DevOps project name
          --repository <name>        Azure DevOps repository name
          --from <date>              Start date (yyyy-MM-dd or ISO 8601)
          --to <date>                End date (yyyy-MM-dd or ISO 8601)
          --output <file>            Output HTML file path
          --pat <token>              Personal access token

        Authentication alternatives:
          --pat-env <name>           Read the PAT from an environment variable instead of --pat

        Optional:
          --branch <name>                    Filter to a branch name
          --user <name>                      Filter the report to a contributor name
          --highlights <count>              Number of highlights to include (default: 5)
          --page-size <count>               Azure DevOps page size (default: 100)
          --exclude-work-items              Skip optional work item enrichment
          --exclude-technical-appendix      Omit the technical appendix from the HTML report
          --help                            Show this help text

        Notes:
          - Date-only inputs are treated as full-day local dates.
          - The PAT is only used in memory for the current run.
        """;
}
