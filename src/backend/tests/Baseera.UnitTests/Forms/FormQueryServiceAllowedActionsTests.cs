using Baseera.Application.Abstractions;
using Baseera.Application.Forms;
using Baseera.Application.Security;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Baseera.Infrastructure.Persistence;

namespace Baseera.UnitTests.Forms;

public sealed class FormQueryServiceAllowedActionsTests : IDisposable
{
    private readonly BaseeraDbContext _db = FormTestFixtures.CreateDb();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Draft_with_design_permissions_allows_update_and_submit()
    {
        FormTestFixtures.SeedDefaultPolicy(_db);
        var user = FormTestFixtures.AddUser(_db, "مصمم");
        var form = FormTestFixtures.NewForm(user.Id);
        _db.FormDefinitions.Add(form);
        await _db.SaveChangesAsync();

        var detail = await CreateService(
            user.Id,
            [
                PermissionCodes.FormsView,
                PermissionCodes.FormsUpdateDraft,
                PermissionCodes.FormsSubmitForReview
            ]).GetDetailAsync(form.Id);

        Assert.NotNull(detail);
        Assert.Equal(
            ["UpdateDraft", "SubmitForReview"],
            detail!.AllowedActions);
    }

    [Fact]
    public async Task In_review_reviewer_sees_request_changes_and_reject()
    {
        FormTestFixtures.SeedDefaultPolicy(_db);
        var user = FormTestFixtures.AddUser(_db, "مراجع");
        var form = FormTestFixtures.NewForm(user.Id, status: FormDefinitionStatus.InReview);
        _db.FormDefinitions.Add(form);
        await _db.SaveChangesAsync();

        var detail = await CreateService(
            user.Id,
            [
                PermissionCodes.FormsView,
                PermissionCodes.FormsRequestChanges,
                PermissionCodes.FormsReject,
                PermissionCodes.FormsApprove
            ]).GetDetailAsync(form.Id);

        Assert.NotNull(detail);
        Assert.Equal(
            ["RequestChanges", "Approve", "Reject"],
            detail!.AllowedActions);
    }

    [Fact]
    public async Task Approved_form_allows_archive_only()
    {
        FormTestFixtures.SeedDefaultPolicy(_db);
        var user = FormTestFixtures.AddUser(_db, "أرشفة");
        var form = FormTestFixtures.NewForm(user.Id, status: FormDefinitionStatus.Approved);
        _db.FormDefinitions.Add(form);
        await _db.SaveChangesAsync();

        var detail = await CreateService(
            user.Id,
            [
                PermissionCodes.FormsView,
                PermissionCodes.FormsArchive,
                PermissionCodes.FormsRestore,
                PermissionCodes.FormsUpdateDraft
            ]).GetDetailAsync(form.Id);

        Assert.NotNull(detail);
        Assert.Equal(["Archive"], detail!.AllowedActions);
    }

    [Fact]
    public async Task Archived_form_allows_restore_only()
    {
        FormTestFixtures.SeedDefaultPolicy(_db);
        var user = FormTestFixtures.AddUser(_db, "استعادة");
        var form = FormTestFixtures.NewForm(user.Id, status: FormDefinitionStatus.Archived);
        _db.FormDefinitions.Add(form);
        await _db.SaveChangesAsync();

        var detail = await CreateService(
            user.Id,
            [
                PermissionCodes.FormsView,
                PermissionCodes.FormsArchive,
                PermissionCodes.FormsRestore
            ]).GetDetailAsync(form.Id);

        Assert.NotNull(detail);
        Assert.Equal(["Restore"], detail!.AllowedActions);
    }

    [Fact]
    public async Task Design_deny_hides_update_and_submit_actions()
    {
        FormTestFixtures.SeedDefaultPolicy(_db);
        var user = FormTestFixtures.AddUser(_db, "مصمم");
        var form = FormTestFixtures.NewForm(user.Id);
        _db.FormDefinitions.Add(form);
        _db.FormAccessGrants.Add(FormTestFixtures.NewGrant(
            form.Id,
            user.Id,
            FormAccessCapability.Design,
            FormAccessGrantEffect.Deny));
        await _db.SaveChangesAsync();

        var detail = await CreateService(
            user.Id,
            [
                PermissionCodes.FormsView,
                PermissionCodes.FormsUpdateDraft,
                PermissionCodes.FormsSubmitForReview
            ]).GetDetailAsync(form.Id);

        Assert.NotNull(detail);
        Assert.Empty(detail!.AllowedActions);
    }

    private FormQueryService CreateService(Guid userId, string[] permissions)
    {
        var current = FormTestFixtures.CurrentUser(
            userId,
            permissions,
            new UserScopeSnapshot(ScopeType.Global, null, null, null));
        var org = new OrganizationalScopeService(current, _db);
        var scope = new FormScopeService(org, current, _db);
        var effective = new FormEffectiveAccessService(_db, current);
        var retention = new FormRetentionPolicyService(_db, scope);
        return new FormQueryService(_db, current, scope, retention, effective, new FormTestFixtures.NoOpAudit());
    }
}
