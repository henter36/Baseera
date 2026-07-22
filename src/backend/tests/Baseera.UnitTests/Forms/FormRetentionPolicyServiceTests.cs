using Baseera.Application.Abstractions;
using Baseera.Application.Forms;
using Baseera.Application.Security;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Baseera.Infrastructure.Persistence;

namespace Baseera.UnitTests.Forms;

public sealed class FormRetentionPolicyServiceTests : IDisposable
{
    private readonly BaseeraDbContext _db = FormTestFixtures.CreateDb();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void RejectHardDelete_throws()
    {
        var service = CreateService(FormTestFixtures.CurrentUser(Guid.NewGuid(), [], new UserScopeSnapshot(ScopeType.Global, null, null, null)));
        var ex = Assert.Throws<InvalidOperationException>(() => service.RejectHardDelete());
        Assert.Contains("الحذف النهائي", ex.Message);
    }

    [Fact]
    public async Task Draft_form_retention_is_not_applicable()
    {
        FormTestFixtures.SeedDefaultPolicy(_db);
        var creator = FormTestFixtures.AddUser(_db);
        var form = FormTestFixtures.NewForm(creator.Id, status: FormDefinitionStatus.Draft);
        _db.FormDefinitions.Add(form);
        await _db.SaveChangesAsync();

        var status = await CreateService(GlobalUser()).GetRetentionStatusAsync(form.Id);

        Assert.False(status.IsRetentionApplicable);
        Assert.Null(status.RetentionAnchorUtc);
        Assert.False(status.IsEligibleForArchive);
    }

    [Fact]
    public async Task Approved_form_uses_approved_timestamp_and_default_retention()
    {
        FormTestFixtures.SeedDefaultPolicy(_db);
        var creator = FormTestFixtures.AddUser(_db);
        var approvedAt = DateTimeOffset.UtcNow.AddDays(-10);
        var form = FormTestFixtures.NewForm(creator.Id, status: FormDefinitionStatus.Approved);
        form.ApprovedAtUtc = approvedAt;
        _db.FormDefinitions.Add(form);
        await _db.SaveChangesAsync();

        var status = await CreateService(GlobalUser()).GetRetentionStatusAsync(form.Id);

        Assert.True(status.IsRetentionApplicable);
        Assert.Equal(approvedAt, status.RetentionAnchorUtc);
        Assert.Equal(365, status.RetentionDays);
        Assert.Equal(approvedAt.AddDays(365), status.ExpiresAtUtc);
        Assert.False(status.IsExpired);
        Assert.False(status.IsEligibleForArchive);
    }

    [Fact]
    public async Task Sensitive_classification_uses_sensitive_retention_days()
    {
        FormTestFixtures.SeedDefaultPolicy(_db);
        var creator = FormTestFixtures.AddUser(_db);
        var approvedAt = DateTimeOffset.UtcNow.AddDays(-5);
        var form = FormTestFixtures.NewForm(
            creator.Id,
            status: FormDefinitionStatus.Approved,
            classification: ClassificationLevel.Confidential);
        form.ApprovedAtUtc = approvedAt;
        _db.FormDefinitions.Add(form);
        await _db.SaveChangesAsync();

        var status = await CreateService(GlobalUser()).GetRetentionStatusAsync(form.Id);

        Assert.Equal(730, status.RetentionDays);
        Assert.Equal(approvedAt.AddDays(730), status.ExpiresAtUtc);
    }

    [Fact]
    public async Task Minimum_retention_days_are_enforced()
    {
        var policy = FormTestFixtures.SeedDefaultPolicy(_db);
        policy.DefaultRetentionDays = 10;
        policy.MinimumRetentionDays = 30;
        _db.Update(policy);
        await _db.SaveChangesAsync();

        var creator = FormTestFixtures.AddUser(_db);
        var form = FormTestFixtures.NewForm(creator.Id, status: FormDefinitionStatus.Approved);
        form.ApprovedAtUtc = DateTimeOffset.UtcNow;
        _db.FormDefinitions.Add(form);
        await _db.SaveChangesAsync();

        var status = await CreateService(GlobalUser()).GetRetentionStatusAsync(form.Id);
        Assert.Equal(30, status.RetentionDays);
    }

    [Fact]
    public async Task Approved_form_becomes_eligible_for_archive_after_retention_expires()
    {
        FormTestFixtures.SeedDefaultPolicy(_db);
        var creator = FormTestFixtures.AddUser(_db);
        var form = FormTestFixtures.NewForm(creator.Id, status: FormDefinitionStatus.Approved);
        form.ApprovedAtUtc = DateTimeOffset.UtcNow.AddDays(-400);
        _db.FormDefinitions.Add(form);
        await _db.SaveChangesAsync();

        var service = CreateService(GlobalUser());
        var status = await service.GetRetentionStatusAsync(form.Id);

        Assert.True(status.IsExpired);
        Assert.True(status.IsEligibleForArchive);
        Assert.True(await service.IsEligibleForArchiveAsync(form));
    }

    [Fact]
    public async Task Archived_form_anchors_on_archived_timestamp()
    {
        FormTestFixtures.SeedDefaultPolicy(_db);
        var creator = FormTestFixtures.AddUser(_db);
        var archivedAt = DateTimeOffset.UtcNow.AddDays(-3);
        var form = FormTestFixtures.NewForm(creator.Id, status: FormDefinitionStatus.Archived);
        form.ApprovedAtUtc = DateTimeOffset.UtcNow.AddDays(-400);
        form.ArchivedAtUtc = archivedAt;
        _db.FormDefinitions.Add(form);
        await _db.SaveChangesAsync();

        var status = await CreateService(GlobalUser()).GetRetentionStatusAsync(form.Id);

        Assert.True(status.IsRetentionApplicable);
        Assert.Equal(archivedAt, status.RetentionAnchorUtc);
        Assert.False(status.IsEligibleForArchive);
    }

    [Fact]
    public async Task Out_of_scope_form_is_not_found()
    {
        FormTestFixtures.SeedDefaultPolicy(_db);
        FormTestFixtures.SeedOrgGraph(_db);
        var creator = FormTestFixtures.AddUser(_db);
        var form = FormTestFixtures.NewForm(
            creator.Id,
            scopeType: ScopeType.Facility,
            regionId: SeedIds.RegionB,
            facilityId: SeedIds.FacilityB1);
        _db.FormDefinitions.Add(form);
        await _db.SaveChangesAsync();

        var regionAUser = FormTestFixtures.CurrentUser(
            creator.Id,
            [PermissionCodes.FormsView],
            new UserScopeSnapshot(ScopeType.Region, SeedIds.RegionA, null, null));
        var service = CreateService(regionAUser);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetRetentionStatusAsync(form.Id));
    }

    private static FakeCurrentUser GlobalUser() =>
        FormTestFixtures.CurrentUser(
            Guid.NewGuid(),
            [PermissionCodes.FormsView],
            new UserScopeSnapshot(ScopeType.Global, null, null, null));

    private FormRetentionPolicyService CreateService(FakeCurrentUser current)
    {
        var org = new OrganizationalScopeService(current, _db);
        var scope = new FormScopeService(org, current, _db);
        return new FormRetentionPolicyService(_db, scope);
    }
}
