using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using UniGetUI.Core.Data;
using UniGetUI.PackageEngine.Operations.History;

namespace UniGetUI.Avalonia.Infrastructure;

public static class DebugLogCommand
{
    private const int MaxUpdateRecords = 12;

    private static readonly Regex AuthorizationValue = new(
        @"(?i)\bauthorization\s*[:=]\s*(?:bearer\s+)?[^\s,;]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SecretAssignment = new(
        @"(?i)\b(password|passwd|token|secret|api[_-]?key|access[_-]?token|refresh[_-]?token)\b\s*[""']?\s*[:=]\s*[""']?[^""'\s,;]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex UrlCredentials = new(
        @"(?i)(https?://)[^/\s:@]+:[^/\s@]+@",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsCommand(string[] args) =>
        args is { Length: 1 }
        && (args[0].Equals("-debug", StringComparison.OrdinalIgnoreCase)
            || args[0].Equals("--debug", StringComparison.OrdinalIgnoreCase));

    public static int Run(TextWriter output, TextWriter error)
    {
        try
        {
            string report = CreateReport(OperationHistoryStore.GetAll());
            string path = WriteReport(report);
            output.WriteLine($"Upkeep debug log saved to: {path}");
            return 0;
        }
        catch (Exception ex)
        {
            error.WriteLine($"Could not create the Upkeep debug log: {ex.Message}");
            return 1;
        }
    }

    public static string CreateReport(IEnumerable<OperationHistoryRecord> records)
    {
        OperationHistoryRecord[] updates = records
            .Where(record => string.Equals(record.Kind, "update-package", StringComparison.OrdinalIgnoreCase))
            .Take(MaxUpdateRecords)
            .ToArray();

        var report = new StringBuilder();
        report.AppendLine("Upkeep update diagnostic log");
        report.AppendLine($"Generated (UTC): {DateTimeOffset.UtcNow:O}");
        report.AppendLine($"Upkeep version: {CoreData.VersionName}");
        report.AppendLine($"Operating system: {RuntimeInformation.OSDescription}");
        report.AppendLine($"Process architecture: {RuntimeInformation.ProcessArchitecture}");
        report.AppendLine($"Update operations included: {updates.Length}");
        report.AppendLine("Review this file before sharing; it contains package metadata and installer output.");
        report.AppendLine("User profile paths and obvious credential values are redacted.");

        if (updates.Length == 0)
        {
            report.AppendLine();
            report.AppendLine("No package update records were found in Operation History.");
            return report.ToString();
        }

        for (int index = 0; index < updates.Length; index++)
        {
            OperationHistoryRecord update = updates[index];
            report.AppendLine();
            report.AppendLine($"===== Update {index + 1} =====");
            report.AppendLine($"Package: {Sanitize(update.PackageName)} ({Sanitize(update.PackageId)})");
            report.AppendLine($"Status: {Sanitize(update.Status)}");
            report.AppendLine($"Manager/source: {Sanitize(update.ManagerName)} / {Sanitize(update.SourceName)}");
            report.AppendLine($"Version: {Sanitize(update.VersionBefore)} -> {Sanitize(update.VersionAfter)}");
            report.AppendLine($"Timestamp (UTC): {Sanitize(update.TimestampUtc)}");
            report.AppendLine($"Exit code: {update.ExitCode?.ToString() ?? "unknown"}");
            report.AppendLine($"Ran elevated: {update.RanElevated?.ToString() ?? "unknown"}");

            if (!string.IsNullOrWhiteSpace(update.FailureSummary))
                report.AppendLine($"Failure summary: {Sanitize(update.FailureSummary)}");

            report.AppendLine("--- Operation output ---");
            foreach (OperationHistoryOutputLine line in update.Output ?? [])
                report.AppendLine($"{Sanitize(line.Type)}: {Sanitize(line.Text)}");
        }

        return report.ToString();
    }

    private static string WriteReport(string report)
    {
        string fileName = $"Upkeep-debug-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.txt";
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string[] directories = [desktop, Path.GetTempPath()];
        Exception? lastError = null;

        foreach (string directory in directories.Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, fileName);
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                writer.Write(report);
                return path;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                lastError = ex;
            }
        }

        throw new IOException("Could not save the debug log to the Desktop or temporary folder.", lastError);
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        string sanitized = value;
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile))
            sanitized = sanitized.Replace(profile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);

        sanitized = AuthorizationValue.Replace(sanitized, "Authorization: [REDACTED]");
        sanitized = SecretAssignment.Replace(sanitized, "$1=[REDACTED]");
        return UrlCredentials.Replace(sanitized, "$1[REDACTED]@");
    }
}
