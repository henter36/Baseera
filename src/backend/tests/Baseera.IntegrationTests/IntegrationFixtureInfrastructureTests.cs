using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Baseera.IntegrationTests;

[Collection(CoreIntegrationCollection.Name)]
public sealed class IntegrationFixtureInfrastructureTests(CoreIntegrationFixture fixture)
    : IntegrationTestBase<CoreIntegrationFixture>(fixture)
{
    [IntegrationConnectionFact]
    public void Collection_fixture_uses_named_collection_database()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();

        Assert.Equal(Fixture.DatabaseName, db.Database.GetDbConnection().Database);
        Assert.Contains("Baseera_Test_Core_", Fixture.DatabaseName, StringComparison.Ordinal);
    }

    [IntegrationConnectionFact]
    public async Task Reset_removes_test_rows_and_keeps_reference_seed()
    {
        const string subject = "fixture-reset-marker";

        await Factory.SeedUserAsync(
            subject,
            "مستخدم reset",
            [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));

        await Fixture.ResetAsync();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();

        Assert.False(await db.Users.IgnoreQueryFilters().AnyAsync(user => user.ExternalSubject == subject));
        Assert.True(await db.Permissions.AnyAsync(permission => permission.Code == PermissionCodes.UsersView));
        Assert.True(await db.Roles.AnyAsync(role => role.Code == RoleCodes.SystemAdministrator));
    }

    [IntegrationConnectionFact]
    public async Task Interceptor_factory_keeps_independent_database()
    {
        await using var countedFactory = BaseeraApiFactory.WithInterceptor(new NoopCommandInterceptor());
        using var scope = countedFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();

        Assert.NotEqual(Fixture.DatabaseName, db.Database.GetDbConnection().Database);
    }

    private sealed class NoopCommandInterceptor : DbCommandInterceptor;
}
