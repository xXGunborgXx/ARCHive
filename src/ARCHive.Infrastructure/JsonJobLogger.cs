using System.Runtime.InteropServices;
using System.Text.Json;
using ARCHive.Core;

namespace ARCHive.Infrastructure;

public sealed class JsonJobLogger(string? logDirectory = null) : IJobLogger
{
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);
    private readonly string _logDirectory = logDirectory ??
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ARCHive",
            "Logs");

    public async Task<string> WriteAsync<TJob>(
        TJob job,
        JobResult result,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_logDirectory);
        DeleteExpiredLogs();

        var durationSeconds = result.Duration.TotalSeconds;
        var averageBytesPerSecond = durationSeconds > 0 &&
            result.BytesProcessed > 0
                ? result.BytesProcessed / durationSeconds
                : (double?)null;

        var entry = new
        {
            SchemaVersion = 1,
            Application = new
            {
                Name = "ARCHive",
                Version = typeof(JsonJobLogger).Assembly.GetName().Version?.ToString()
            },
            Environment = new
            {
                Windows = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.OSArchitecture.ToString(),
                DotNet = RuntimeInformation.FrameworkDescription
            },
            Job = job,
            Result = result,
            Performance = new
            {
                DurationSeconds = Math.Round(durationSeconds, 3),
                AverageBytesPerSecond = averageBytesPerSecond.HasValue
                    ? Math.Round(averageBytesPerSecond.Value, 2)
                    : (double?)null,
                result.BytesProcessed,
                result.FilesProcessed
            },
            Privacy = new
            {
                Storage = "Local only",
                AutomaticUpload = false,
                RetentionDays = (int)RetentionPeriod.TotalDays
            }
        };

        var fileName =
            $"{createdAt.ToLocalTime():yyyyMMdd-HHmmss}-{result.JobId:N}.json";
        var path = Path.Combine(_logDirectory, fileName);
        var options = new JsonSerializerOptions { WriteIndented = true };

        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read,
            16 * 1024,
            FileOptions.Asynchronous);

        await JsonSerializer.SerializeAsync(
            stream,
            entry,
            options,
            cancellationToken);

        return path;
    }

    private void DeleteExpiredLogs()
    {
        var cutoff = DateTime.UtcNow - RetentionPeriod;

        try
        {
            foreach (var path in Directory.EnumerateFiles(
                _logDirectory,
                "*.json",
                SearchOption.TopDirectoryOnly))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(path) < cutoff)
                    {
                        File.Delete(path);
                    }
                }
                catch (Exception ex) when (
                    ex is IOException or UnauthorizedAccessException)
                {
                    // A locked log is retained and can be reconsidered later.
                }
            }
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            // Logging the current operation remains more important than
            // enforcing retention during a transient filesystem problem.
        }
    }
}
