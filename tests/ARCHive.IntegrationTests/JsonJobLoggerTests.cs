using System.Text.Json;
using ARCHive.Core;
using ARCHive.Infrastructure;

namespace ARCHive.IntegrationTests;

[TestClass]
public sealed class JsonJobLoggerTests
{
    [TestMethod]
    public async Task WriteAsync_RecordsPerformancePrivacyAndAppliesRetention()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"ARCHive-logs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var expired = Path.Combine(root, "expired.json");
            await File.WriteAllTextAsync(expired, "{}");
            File.SetLastWriteTimeUtc(expired, DateTime.UtcNow.AddDays(-31));

            var jobId = Guid.NewGuid();
            var result = new JobResult(
                jobId,
                JobStatus.Completed,
                @"C:\destination\output",
                2_000,
                2,
                TimeSpan.FromSeconds(2),
                0,
                "Completed");
            var logger = new JsonJobLogger(root);

            var path = await logger.WriteAsync(
                new { JobId = jobId, Action = "Copy" },
                result,
                DateTimeOffset.Now);

            Assert.IsFalse(File.Exists(expired));
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            var rootElement = document.RootElement;
            Assert.AreEqual(
                1000d,
                rootElement
                    .GetProperty("Performance")
                    .GetProperty("AverageBytesPerSecond")
                    .GetDouble());
            Assert.IsFalse(
                rootElement
                    .GetProperty("Privacy")
                    .GetProperty("AutomaticUpload")
                    .GetBoolean());
            Assert.AreEqual(
                30,
                rootElement
                    .GetProperty("Privacy")
                    .GetProperty("RetentionDays")
                    .GetInt32());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
