using Baseera.Application.Abstractions;
using Baseera.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Baseera.IntegrationTests;

public sealed class DataProtectionKeyRingIntegrationTests
{
    [IntegrationConnectionFact]
    public void Production_like_environment_without_keys_path_rejects_startup()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Baseera"] =
                    "Server=127.0.0.1,1433;Database=Baseera_Unused;User Id=sa;Password=unused;Encrypt=False;TrustServerCertificate=True"
            })
            .Build();
        var environment = new StubHostEnvironment(Environments.Production);

        var ex = Assert.Throws<InvalidOperationException>(
            () => services.AddBaseeraInfrastructure(configuration, environment));

        Assert.Contains("DataProtection:KeysPath is required", ex.Message, StringComparison.Ordinal);
    }

    [IntegrationConnectionFact]
    public async Task Testing_factory_writes_keys_and_cleans_directories_on_dispose()
    {
        string attachmentsPath;
        string keysPath;

        await using (var factory = BaseeraApiFactory.CreateIsolated())
        {
            _ = factory.Services;
            attachmentsPath = factory.AttachmentsPath;
            keysPath = factory.DataProtectionKeysPath;

            Assert.True(Directory.Exists(keysPath));
            Assert.NotEmpty(Directory.GetFiles(keysPath, "key-*.xml"));

            using var scope = factory.Services.CreateScope();
            var protector = scope.ServiceProvider.GetRequiredService<ISensitiveValueProtector>();
            var protectedValue = protector.Protect("SN-CLEANUP-001");
            Assert.Equal("SN-CLEANUP-001", protector.Unprotect(protectedValue));
        }

        Assert.False(Directory.Exists(attachmentsPath));
        Assert.False(Directory.Exists(keysPath));
        Assert.Empty(Directory.Exists(keysPath) ? Directory.GetFiles(keysPath, "key-*.xml") : []);
    }

    [IntegrationConnectionFact]
    public async Task Dispose_of_one_factory_does_not_remove_another_factory_key_ring()
    {
        await using var surviving = BaseeraApiFactory.CreateIsolated();
        _ = surviving.Services;
        var survivingKeys = surviving.DataProtectionKeysPath;
        Assert.True(Directory.Exists(survivingKeys));

        await using (var disposable = BaseeraApiFactory.CreateIsolated())
        {
            _ = disposable.Services;
            Assert.NotEqual(survivingKeys, disposable.DataProtectionKeysPath);
        }

        Assert.True(Directory.Exists(survivingKeys));
        Assert.NotEmpty(Directory.GetFiles(survivingKeys, "key-*.xml"));
    }

    [IntegrationConnectionFact]
    public async Task Host_restart_with_same_key_ring_can_unprotect_prior_value()
    {
        var sharedKeys = Path.Combine(
            Path.GetTempPath(),
            "baseera-shared-dp-keys",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sharedKeys);

        try
        {
            string protectedValue;
            await using (var first = BaseeraApiFactory.CreateIsolated(
                             dataProtectionKeysPath: sharedKeys,
                             ownsTemporaryDirectories: false))
            {
                using var scope = first.Services.CreateScope();
                var protector = scope.ServiceProvider.GetRequiredService<ISensitiveValueProtector>();
                protectedValue = protector.Protect("SN-RESTART-001");
            }

            await using var second = BaseeraApiFactory.CreateIsolated(
                dataProtectionKeysPath: sharedKeys,
                ownsTemporaryDirectories: false);
            using (var scope = second.Services.CreateScope())
            {
                var protector = scope.ServiceProvider.GetRequiredService<ISensitiveValueProtector>();
                Assert.Equal("SN-RESTART-001", protector.Unprotect(protectedValue));
            }
        }
        finally
        {
            if (Directory.Exists(sharedKeys))
            {
                Directory.Delete(sharedKeys, recursive: true);
            }
        }
    }

    [IntegrationConnectionFact]
    public async Task Two_hosts_sharing_key_ring_can_unprotect_each_others_values()
    {
        var sharedKeys = Path.Combine(
            Path.GetTempPath(),
            "baseera-shared-dp-keys",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sharedKeys);

        try
        {
            await using var hostA = BaseeraApiFactory.CreateIsolated(
                dataProtectionKeysPath: sharedKeys,
                ownsTemporaryDirectories: false);
            await using var hostB = BaseeraApiFactory.CreateIsolated(
                dataProtectionKeysPath: sharedKeys,
                ownsTemporaryDirectories: false);

            using var scopeA = hostA.Services.CreateScope();
            using var scopeB = hostB.Services.CreateScope();
            var protectorA = scopeA.ServiceProvider.GetRequiredService<ISensitiveValueProtector>();
            var protectorB = scopeB.ServiceProvider.GetRequiredService<ISensitiveValueProtector>();

            var fromA = protectorA.Protect("SN-HOST-A");
            var fromB = protectorB.Protect("SN-HOST-B");

            Assert.Equal("SN-HOST-A", protectorB.Unprotect(fromA));
            Assert.Equal("SN-HOST-B", protectorA.Unprotect(fromB));
        }
        finally
        {
            if (Directory.Exists(sharedKeys))
            {
                Directory.Delete(sharedKeys, recursive: true);
            }
        }
    }

    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Baseera.IntegrationTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
