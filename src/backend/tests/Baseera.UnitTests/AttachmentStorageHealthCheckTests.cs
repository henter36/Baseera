using Baseera.Api.Health;
using Baseera.Infrastructure.Attachments;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Baseera.UnitTests;

public sealed class AttachmentStorageHealthCheckTests
{
    [Fact]
    public async Task Healthy_when_root_is_writable()
    {
        var root = Path.Combine(Path.GetTempPath(), "baseera-health-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var check = new AttachmentStorageHealthCheck(Options.Create(new AttachmentStorageOptions { RootPath = root }));
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Empty(Directory.GetFiles(root, ".health-*"));
    }

    [Fact]
    public async Task Unhealthy_when_root_path_is_invalid_file()
    {
        var filePath = Path.Combine(Path.GetTempPath(), "baseera-health-file-" + Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(filePath, "not-a-directory");
        try
        {
            var check = new AttachmentStorageHealthCheck(Options.Create(new AttachmentStorageOptions { RootPath = filePath }));
            var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);
            Assert.Equal(HealthStatus.Unhealthy, result.Status);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task Propagates_cancellation()
    {
        var root = Path.Combine(Path.GetTempPath(), "baseera-health-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var check = new AttachmentStorageHealthCheck(Options.Create(new AttachmentStorageOptions { RootPath = root }));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            check.CheckHealthAsync(new HealthCheckContext(), cts.Token));
    }
}
