using Baseera.Application.Abstractions;
using Baseera.Application.Forms;
using Baseera.Application.Security;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Baseera.Infrastructure.Persistence;

namespace Baseera.UnitTests.Forms;

public sealed class FormSeparationOfDutiesServiceTests : IDisposable
{
    private readonly BaseeraDbContext _db = FormTestFixtures.CreateDb();
    private readonly FormTestFixtures.NoOpAudit _audit = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Creator_cannot_review_when_designer_review_is_disallowed()
    {
        FormTestFixtures.SeedDefaultPolicy(_db);
        var creator = FormTestFixtures.AddUser(_db, "منشئ");
        var form = FormTestFixtures.NewForm(creator.Id);
        _db.FormDefinitions.Add(form);
        await _db.SaveChangesAsync();

        var service = CreateService(creator.Id, []);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.EnforceReviewAsync(form, creator.Id));
    }

    [Fact]
    public async Task Creator_can_review_when_policy_allows_designer_review()
    {
        var policy = FormTestFixtures.SeedDefaultPolicy(_db);
        policy.AllowDesignerToReviewOwnForm = true;
        _db.Update(policy);
        await _db.SaveChangesAsync();

        var creator = FormTestFixtures.AddUser(_db, "منشئ");
        var form = FormTestFixtures.NewForm(creator.Id);
        _db.FormDefinitions.Add(form);
        await _db.SaveChangesAsync();

        var service = CreateService(creator.Id, []);
        var exception = await Record.ExceptionAsync(() =>
            service.EnforceReviewAsync(form, creator.Id));

        Assert.Null(exception);
    }

    [Fact]
    public async Task Last_modifier_cannot_approve()
    {
        FormTestFixtures.SeedDefaultPolicy(_db);
        var creator = FormTestFixtures.AddUser(_db, "منشئ");
        var form = FormTestFixtures.NewForm(creator.Id, status: FormDefinitionStatus.InReview);
        form.LastModifiedByUserId = creator.Id;
        _db.FormDefinitions.Add(form);
        await _db.SaveChangesAsync();

        var service = CreateService(creator.Id, []);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.EnforceApproveAsync(form, creator.Id));
    }

    [Fact]
    public async Task Reviewer_who_requested_changes_cannot_approve()
    {
        FormTestFixtures.SeedDefaultPolicy(_db);
        var creator = FormTestFixtures.AddUser(_db, "منشئ");
        var reviewer = FormTestFixtures.AddUser(_db, "مراجع");
        var form = FormTestFixtures.NewForm(creator.Id, status: FormDefinitionStatus.InReview);
        form.LastModifiedByUserId = creator.Id;
        _db.FormDefinitions.Add(form);
        _db.FormReviewDecisions.Add(new FormReviewDecision
        {
            FormDefinitionId = form.Id,
            Decision = FormReviewDecisionType.RequestChanges,
            ReviewedByUserId = reviewer.Id,
            FromStatus = FormDefinitionStatus.InReview,
            ToStatus = FormDefinitionStatus.ChangesRequested
        });
        await _db.SaveChangesAsync();

        var service = CreateService(reviewer.Id, []);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.EnforceApproveAsync(form, reviewer.Id));
        Assert.Contains("طلب التعديلات", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reviewer_can_approve_after_request_changes_when_policy_allows()
    {
        var policy = FormTestFixtures.SeedDefaultPolicy(_db);
        policy.AllowReviewerToApproveOwnReview = true;
        _db.Update(policy);
        await _db.SaveChangesAsync();

        var creator = FormTestFixtures.AddUser(_db, "منشئ");
        var reviewer = FormTestFixtures.AddUser(_db, "مراجع");
        var form = FormTestFixtures.NewForm(creator.Id, status: FormDefinitionStatus.InReview);
        form.LastModifiedByUserId = creator.Id;
        _db.FormDefinitions.Add(form);
        _db.FormReviewDecisions.Add(new FormReviewDecision
        {
            FormDefinitionId = form.Id,
            Decision = FormReviewDecisionType.RequestChanges,
            ReviewedByUserId = reviewer.Id,
            FromStatus = FormDefinitionStatus.InReview,
            ToStatus = FormDefinitionStatus.ChangesRequested
        });
        await _db.SaveChangesAsync();

        var service = CreateService(reviewer.Id, []);
        var exception = await Record.ExceptionAsync(() =>
            service.EnforceApproveAsync(form, reviewer.Id));

        Assert.Null(exception);
    }

    [Fact]
    public async Task Creator_cannot_grant_access_on_own_form()
    {
        FormTestFixtures.SeedDefaultPolicy(_db);
        var creator = FormTestFixtures.AddUser(_db, "منشئ");
        var form = FormTestFixtures.NewForm(creator.Id);
        _db.FormDefinitions.Add(form);
        await _db.SaveChangesAsync();

        var service = CreateService(creator.Id, []);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.EnforceGrantAsync(form, creator.Id));
    }

    [Fact]
    public async Task Submit_for_review_has_no_additional_sod_checks()
    {
        FormTestFixtures.SeedDefaultPolicy(_db);
        var creator = FormTestFixtures.AddUser(_db, "منشئ");
        var form = FormTestFixtures.NewForm(creator.Id);
        _db.FormDefinitions.Add(form);
        await _db.SaveChangesAsync();

        var service = CreateService(creator.Id, []);
        var exception = await Record.ExceptionAsync(() =>
            service.EnforceSubmitForReviewAsync(form, creator.Id));

        Assert.Null(exception);
    }

    [Fact]
    public async Task Administrative_override_is_allowed_with_governance_permission()
    {
        FormTestFixtures.SeedDefaultPolicy(_db);
        var creator = FormTestFixtures.AddUser(_db, "منشئ");
        var form = FormTestFixtures.NewForm(creator.Id, status: FormDefinitionStatus.InReview);
        _db.FormDefinitions.Add(form);
        await _db.SaveChangesAsync();

        var service = CreateService(
            creator.Id,
            [PermissionCodes.FormsManageGovernance]);
        var exception = await Record.ExceptionAsync(() =>
            service.EnforceReviewAsync(form, creator.Id, "تجاوز إداري لاختبار فصل الواجبات"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task SoD_checks_are_skipped_when_policy_disables_separation()
    {
        var policy = FormTestFixtures.SeedDefaultPolicy(_db);
        policy.RequireSeparationOfDuties = false;
        _db.Update(policy);
        await _db.SaveChangesAsync();

        var creator = FormTestFixtures.AddUser(_db, "منشئ");
        var form = FormTestFixtures.NewForm(creator.Id, status: FormDefinitionStatus.InReview);
        form.LastModifiedByUserId = creator.Id;
        _db.FormDefinitions.Add(form);
        await _db.SaveChangesAsync();

        var service = CreateService(creator.Id, []);
        var exception = await Record.ExceptionAsync(async () =>
        {
            await service.EnforceReviewAsync(form, creator.Id);
            await service.EnforceApproveAsync(form, creator.Id);
            await service.EnforceGrantAsync(form, creator.Id);
        });

        Assert.Null(exception);
    }

    private FormSeparationOfDutiesService CreateService(Guid userId, string[] permissions)
    {
        var current = FormTestFixtures.CurrentUser(
            userId,
            permissions,
            new UserScopeSnapshot(ScopeType.Global, null, null, null));
        return new FormSeparationOfDutiesService(_db, current, _audit);
    }
}
