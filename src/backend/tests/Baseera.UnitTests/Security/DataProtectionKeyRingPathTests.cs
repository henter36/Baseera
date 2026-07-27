using Baseera.Infrastructure.DependencyInjection;
using Baseera.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Baseera.UnitTests.Security;

public sealed class DataProtectionKeyRingPathTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Restricted_environment_missing_path_throws(string environmentName)
    {
        var configuration = BuildConfiguration(keysPath: null);
        var environment = new StubHostEnvironment(environmentName);

        var ex = Assert.Throws<InvalidOperationException>(
            () => InfrastructureServiceCollectionExtensions.ResolveDataProtectionKeysPath(
                configuration,
                environment));

        Assert.Contains("DataProtection:KeysPath is required", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_relative_path_throws()
    {
        var configuration = BuildConfiguration("relative/data-protection-keys");
        var environment = new StubHostEnvironment(Environments.Production);

        var ex = Assert.Throws<InvalidOperationException>(
            () => InfrastructureServiceCollectionExtensions.ResolveDataProtectionKeysPath(
                configuration,
                environment));

        Assert.Contains("absolute path", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_absolute_path_is_accepted()
    {
        var absolute = Path.Combine(Path.GetTempPath(), "baseera-dp-unit", Guid.NewGuid().ToString("N"));
        var configuration = BuildConfiguration(absolute);
        var environment = new StubHostEnvironment(Environments.Production);

        var resolved = InfrastructureServiceCollectionExtensions.ResolveDataProtectionKeysPath(
            configuration,
            environment);

        Assert.Equal(absolute, resolved);
    }

    [Fact]
    public void Development_missing_path_uses_temp_fallback()
    {
        var configuration = BuildConfiguration(keysPath: null);
        var environment = new StubHostEnvironment(Environments.Development);

        var resolved = InfrastructureServiceCollectionExtensions.ResolveDataProtectionKeysPath(
            configuration,
            environment);

        Assert.StartsWith(Path.GetTempPath(), resolved, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("baseera-data-protection-keys", resolved, StringComparison.Ordinal);
        Assert.Contains(Environments.Development, resolved, StringComparison.Ordinal);
    }

    [Fact]
    public void Testing_explicit_path_is_accepted()
    {
        var absolute = Path.Combine(Path.GetTempPath(), "baseera-dp-unit", Guid.NewGuid().ToString("N"));
        var configuration = BuildConfiguration(absolute);
        var environment = new StubHostEnvironment("Testing");

        var resolved = InfrastructureServiceCollectionExtensions.ResolveDataProtectionKeysPath(
            configuration,
            environment);

        Assert.Equal(absolute, resolved);
    }

    [Fact]
    public void Shared_key_ring_allows_cross_provider_unprotect()
    {
        var keysPath = Path.Combine(Path.GetTempPath(), "baseera-dp-shared", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(keysPath);

        try
        {
            var providerA = DataProtectionProvider.Create(
                new DirectoryInfo(keysPath),
                configuration => configuration.SetApplicationName("Baseera"));
            var protectedValue = providerA
                .CreateProtector(DataProtectionSensitiveValueProtector.SerialNumberPurpose)
                .Protect("SN-SHARED-001");

            var providerB = DataProtectionProvider.Create(
                new DirectoryInfo(keysPath),
                configuration => configuration.SetApplicationName("Baseera"));
            var plaintext = providerB
                .CreateProtector(DataProtectionSensitiveValueProtector.SerialNumberPurpose)
                .Unprotect(protectedValue);

            Assert.Equal("SN-SHARED-001", plaintext);
            Assert.NotEmpty(Directory.GetFiles(keysPath, "key-*.xml"));
        }
        finally
        {
            if (Directory.Exists(keysPath))
            {
                Directory.Delete(keysPath, recursive: true);
            }
        }
    }

    private static IConfiguration BuildConfiguration(string? keysPath) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                keysPath is null
                    ? new Dictionary<string, string?>()
                    : new Dictionary<string, string?>
                    {
                        ["DataProtection:KeysPath"] = keysPath
                    })
            .Build();

    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Baseera.UnitTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
