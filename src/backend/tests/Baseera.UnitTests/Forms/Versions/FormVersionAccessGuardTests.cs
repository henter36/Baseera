using Baseera.Application.Abstractions;
using Baseera.Application.Forms;
using Baseera.Application.Security;
using Baseera.Domain.Common;
using Baseera.Domain.Forms;
using Baseera.Infrastructure.Persistence;

namespace Baseera.UnitTests.Forms.Versions;

public sealed class FormVersionAccessGuardTests : IDisposable
{
    private readonly BaseeraDbContext _db = FormTestFixtures.CreateDb();

    public void Dispose() => _db.Dispose();

    private IFormVersionAccessGuard CreateGuard(Guid userId, IReadOnlyCollection<string> permissions)
    {
        var currentUser = FormTestFixtures.CurrentUser(userId, permissions, new UserScopeSnapshot(ScopeType.Global, null, null, null));
        var scope = new FormScopeService(new OrganizationalScopeService(currentUser, _db), currentUser, _db);
        var effectiveAccess = new FormEffectiveAccessService(_db, currentUser);
        return new FormVersionAccessGuard(_db, scope, effectiveAccess);
    }

    [Fact]
    public async Task LoadInScopeAsync_returns_form_when_caller_is_in_scope()
    {
        var userId = FormTestFixtures.AddUser(_db).Id;
        var form = FormTestFixtures.NewForm(userId);
        _db.FormDefinitions.Add(form);
        await _db.SaveChangesAsync();

        var guard = CreateGuard(userId, ["Forms.View"]);
        var loaded = await guard.LoadInScopeAsync(form.Id);

        Assert.Equal(form.Id, loaded.Id);
    }

    [Fact]
    public async Task LoadInScopeAsync_throws_not_found_for_a_form_that_does_not_exist()
    {
        var userId = FormTestFixtures.AddUser(_db).Id;
        var guard = CreateGuard(userId, ["Forms.View"]);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => guard.LoadInScopeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task LoadViewableAsync_returns_form_when_in_scope_with_default_view_access()
    {
        var userId = FormTestFixtures.AddUser(_db).Id;
        var form = FormTestFixtures.NewForm(userId);
        _db.FormDefinitions.Add(form);
        await _db.SaveChangesAsync();

        var guard = CreateGuard(userId, ["Forms.View"]);
        var loaded = await guard.LoadViewableAsync(form.Id);

        Assert.Equal(form.Id, loaded.Id);
    }

    [Fact]
    public async Task LoadViewableAsync_throws_not_found_when_an_explicit_deny_grant_blocks_view_even_though_the_form_is_in_scope()
    {
        var userId = FormTestFixtures.AddUser(_db).Id;
        var form = FormTestFixtures.NewForm(userId);
        _db.FormDefinitions.Add(form);
        await _db.SaveChangesAsync();
        _db.FormAccessGrants.Add(FormTestFixtures.NewGrant(form.Id, userId, FormAccessCapability.View, FormAccessGrantEffect.Deny));
        await _db.SaveChangesAsync();

        var guard = CreateGuard(userId, ["Forms.View"]);

        // The combined scope+capability check must reject access the same way a missing form
        // would (404, not 403) — this is the exact behavior LoadViewableAsync exists to preserve
        // from the two-call pattern it replaces.
        await Assert.ThrowsAsync<KeyNotFoundException>(() => guard.LoadViewableAsync(form.Id));
    }

    [Fact]
    public async Task EnsureCapabilityAsync_throws_unauthorized_when_denied()
    {
        var userId = FormTestFixtures.AddUser(_db).Id;
        var form = FormTestFixtures.NewForm(userId);
        _db.FormDefinitions.Add(form);
        await _db.SaveChangesAsync();
        _db.FormAccessGrants.Add(FormTestFixtures.NewGrant(form.Id, userId, FormAccessCapability.Design, FormAccessGrantEffect.Deny));
        await _db.SaveChangesAsync();

        var guard = CreateGuard(userId, ["Forms.UpdateDraft"]);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => guard.EnsureCapabilityAsync(form, FormAccessCapability.Design));
    }

    [Fact]
    public async Task HasCapabilityAsync_reflects_the_underlying_effective_access_decision()
    {
        var userId = FormTestFixtures.AddUser(_db).Id;
        var form = FormTestFixtures.NewForm(userId);
        _db.FormDefinitions.Add(form);
        await _db.SaveChangesAsync();

        var guard = CreateGuard(userId, ["Forms.UpdateDraft"]);

        Assert.True(await guard.HasCapabilityAsync(form, FormAccessCapability.Design));
    }
}
