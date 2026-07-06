using System.Globalization;

namespace DevReleaseReporter.Cli;

internal static class CliOptionsParser
{
    public static bool WantsHelp(string[] args) =>
        args.Length == 0 ||
        args.Any(static arg => string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase)) ||
        args.Any(static arg => string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase)) ||
        args.Any(static arg => string.Equals(arg, "help", StringComparison.OrdinalIgnoreCase));

    public static CliGenerateReportOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var remainingArgs = StripCommandName(args);
        var optionMap = ParseOptions(remainingArgs);

        var pat = GetOptionalValue(optionMap, "--pat");
        var patEnv = GetOptionalValue(optionMap, "--pat-env");

        if (string.IsNullOrWhiteSpace(pat) && string.IsNullOrWhiteSpace(patEnv))
        {
            throw new CliUsageException("Either --pat or --pat-env is required.");
        }

        if (!string.IsNullOrWhiteSpace(pat) && !string.IsNullOrWhiteSpace(patEnv))
        {
            throw new CliUsageException("Use either --pat or --pat-env, not both.");
        }

        return new CliGenerateReportOptions
        {
            OrganizationUrl = ParseRequiredUri(optionMap, "--organization-url"),
            ProjectName = GetRequiredValue(optionMap, "--project"),
            RepositoryName = GetRequiredValue(optionMap, "--repository"),
            PersonalAccessToken = ResolvePersonalAccessToken(pat, patEnv),
            FromDate = ParseDate(optionMap, "--from", endOfDay: false),
            ToDate = ParseDate(optionMap, "--to", endOfDay: true),
            OutputPath = GetRequiredValue(optionMap, "--output"),
            BranchName = GetOptionalValue(optionMap, "--branch"),
            ContributorName = GetOptionalValue(optionMap, "--user"),
            IncludeWorkItems = !optionMap.ContainsKey("--exclude-work-items"),
            IncludeTechnicalAppendix = !optionMap.ContainsKey("--exclude-technical-appendix"),
            HighlightLimit = ParsePositiveInt(optionMap, "--highlights", defaultValue: 5),
            PageSize = ParsePositiveInt(optionMap, "--page-size", defaultValue: 100),
        };
    }

    private static string[] StripCommandName(string[] args)
    {
        if (args.Length == 0)
        {
            return args;
        }

        return string.Equals(args[0], "generate-report", StringComparison.OrdinalIgnoreCase)
            ? args[1..]
            : args;
    }

    private static Dictionary<string, string?> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length; index++)
        {
            var current = args[index];
            if (!current.StartsWith("--", StringComparison.Ordinal))
            {
                throw new CliUsageException($"Unexpected argument '{current}'.");
            }

            if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                options[current] = args[index + 1];
                index++;
                continue;
            }

            options[current] = null;
        }

        return options;
    }

    private static string GetRequiredValue(IReadOnlyDictionary<string, string?> options, string optionName)
    {
        var value = GetOptionalValue(options, optionName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CliUsageException($"Missing required option {optionName}.");
        }

        return value.Trim();
    }

    private static string? GetOptionalValue(IReadOnlyDictionary<string, string?> options, string optionName) =>
        options.TryGetValue(optionName, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static Uri ParseRequiredUri(IReadOnlyDictionary<string, string?> options, string optionName)
    {
        var rawValue = GetRequiredValue(options, optionName);
        if (!Uri.TryCreate(rawValue, UriKind.Absolute, out var uri))
        {
            throw new CliUsageException($"Option {optionName} must be a valid absolute URL.");
        }

        return uri;
    }

    private static int ParsePositiveInt(
        IReadOnlyDictionary<string, string?> options,
        string optionName,
        int defaultValue)
    {
        var rawValue = GetOptionalValue(options, optionName);
        if (rawValue is null)
        {
            return defaultValue;
        }

        if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value <= 0)
        {
            throw new CliUsageException($"Option {optionName} must be a positive integer.");
        }

        return value;
    }

    private static DateTimeOffset ParseDate(
        IReadOnlyDictionary<string, string?> options,
        string optionName,
        bool endOfDay)
    {
        var rawValue = GetRequiredValue(options, optionName);

        if (DateOnly.TryParseExact(rawValue, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
        {
            var timeOnly = endOfDay
                ? new TimeOnly(23, 59, 59, 999).Add(TimeSpan.FromTicks(9999))
                : TimeOnly.MinValue;

            var localDateTime = dateOnly.ToDateTime(timeOnly, DateTimeKind.Local);
            return new DateTimeOffset(localDateTime);
        }

        if (DateTimeOffset.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dateTimeOffset))
        {
            return dateTimeOffset;
        }

        throw new CliUsageException($"Option {optionName} must be a valid yyyy-MM-dd or ISO 8601 date.");
    }

    private static string ResolvePersonalAccessToken(string? pat, string? patEnv)
    {
        if (!string.IsNullOrWhiteSpace(pat))
        {
            return pat;
        }

        var variableName = patEnv!.Trim();
        var value = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CliUsageException($"Environment variable '{variableName}' does not contain a PAT value.");
        }

        return value.Trim();
    }
}
