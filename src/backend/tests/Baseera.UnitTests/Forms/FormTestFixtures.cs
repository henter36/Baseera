using Baseera.Application.Abstractions;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Baseera.Domain.Organization;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Baseera.UnitTests.Forms;

/// <summary>
/// Shared scaffolding for Forms unit tests (InMemory EF provider).
/// </summary>
internal static class FormTestFixtures
{
    public static BaseeraDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<BaseeraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    public static User AddUser(BaseeraDbContext db, string name = "user")
    {
        var user = new User
        {
            ExternalSubject = Guid.NewGuid().ToString(),
            UserName = Guid.NewGuid().ToString("N"),
            DisplayNameAr = name,
            IsActive = true,
            ProvisioningStatus = UserProvisioningStatus.Active
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    public static FormGovernancePolicy SeedDefaultPolicy(BaseeraDbContext db)
    {
        if (db.FormGovernancePolicies.Any())
        {
            return db.FormGovernancePolicies.First();
        }

        var policy = new FormGovernancePolicy
        {
            Id = SeedIds.FormGovernancePolicy,
            RequireReviewBeforeApproval = true,
            RequireSeparationOfDuties = true,
            AllowDesignerToReviewOwnForm = false,
            AllowReviewerToApproveOwnReview = false,
            AllowApproverToPublish = true,
            DefaultRetentionDays = 365,
            SensitiveRetentionDays = 730,
            MinimumRetentionDays = 30,
            AuditSensitiveViews = true,
            AuditExports = true,
            RequireReasonForArchive = true
        };
        db.FormGovernancePolicies.Add(policy);
        db.SaveChanges();
        return policy;
    }

    public static void SeedOrgGraph(BaseeraDbContext db)
    {
        if (!db.Organizations.Any(o => o.Id == SeedIds.Organization))
        {
            db.Organizations.Add(new Organization
            {
                Id = SeedIds.Organization,
                Code = "HQ",
                NameAr = "رئيسي",
                IsActive = true
            });
        }

        if (!db.Regions.Any(r => r.Id == SeedIds.RegionA))
        {
            db.Regions.AddRange(
                new Region { Id = SeedIds.RegionA, OrganizationId = SeedIds.Organization, Code = "A", NameAr = "أ", IsActive = true },
                new Region { Id = SeedIds.RegionB, OrganizationId = SeedIds.Organization, Code = "B", NameAr = "ب", IsActive = true });
        }

        if (!db.Facilities.Any(f => f.Id == SeedIds.FacilityA1))
        {
            db.Facilities.AddRange(
                new Facility { Id = SeedIds.FacilityA1, RegionId = SeedIds.RegionA, Code = "A1", NameAr = "أ1", IsActive = true },
                new Facility { Id = SeedIds.FacilityA2, RegionId = SeedIds.RegionA, Code = "A2", NameAr = "أ2", IsActive = true },
                new Facility { Id = SeedIds.FacilityB1, RegionId = SeedIds.RegionB, Code = "B1", NameAr = "ب1", IsActive = true });
        }

        db.SaveChanges();
    }

    public static FormDefinition NewForm(
        Guid createdBy,
        ScopeType scopeType = ScopeType.Global,
        Guid? regionId = null,
        Guid? facilityId = null,
        Guid? facilityUnitId = null,
        FormDefinitionStatus status = FormDefinitionStatus.Draft,
        ClassificationLevel classification = ClassificationLevel.Internal,
        string code = "FORM-001") => new()
    {
        Code = code,
        NameAr = "نموذج تجريبي",
        Description = "وصف تجريبي",
        Classification = classification,
        ScopeType = scopeType,
        RegionId = regionId,
        FacilityId = facilityId,
        FacilityUnitId = facilityUnitId,
        Status = status,
        CreatedByUserId = createdBy,
        LastModifiedByUserId = createdBy
    };

    public static FormAccessGrant NewGrant(
        Guid formId,
        Guid principalId,
        FormAccessCapability capability,
        FormAccessGrantEffect effect = FormAccessGrantEffect.Allow,
        FormAccessGrantPrincipalType principalType = FormAccessGrantPrincipalType.User,
        DateTimeOffset? validFromUtc = null,
        DateTimeOffset? validToUtc = null) => new()
    {
        FormDefinitionId = formId,
        PrincipalType = principalType,
        PrincipalId = principalId,
        Capability = capability,
        Effect = effect,
        ValidFromUtc = validFromUtc,
        ValidToUtc = validToUtc,
        Reason = "اختبار",
        CreatedByUserId = principalId
    };

    public static FakeCurrentUser CurrentUser(
        Guid userId,
        IReadOnlyCollection<string> permissions,
        params UserScopeSnapshot[] scopes) =>
        new(true, userId, userId.ToString(), "actor", permissions, scopes);

    public sealed class NoOpAudit : IAuditService
    {
        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
