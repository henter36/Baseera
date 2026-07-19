using Baseera.Application.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Baseera.UnitTests;

public sealed class StartupGuardHostTests
{
    [Theory]
    [InlineData("Production", true, false)]
    [InlineData("Staging", true, false)]
    [InlineData("Production", false, true)]
    public void Host_builder_fails_fast_for_restricted_env(string environment, bool testAuth, bool seed)
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            EnvironmentSecurityGuard.EnsureSafeConfiguration(environment, testAuth, seed);
        });
    }

    [Fact]
    public void Boolean_alone_insufficient_outside_allowlist()
    {
        Assert.False(EnvironmentSecurityGuard.CanEnableTestAuth("Production", true));
        Assert.False(EnvironmentSecurityGuard.CanEnableDemoSeed("Staging", true));
        Assert.True(EnvironmentSecurityGuard.CanEnableTestAuth("Testing", true));
    }
}
