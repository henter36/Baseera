namespace Baseera.Application.RiskManagement;

using Baseera.Application.Abstractions;
using Baseera.Domain.Common;
using Baseera.Domain.Organization;
using Baseera.Domain.RiskManagement;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Shared scope/permission/audit/concurrency plumbing for the twelve Risk Management services, mirroring
/// the private helpers in SensitiveCustodyServices — kept as a base class (not a "God service") so every
/// concrete service enforces facility scope, four-eyes, and audit logging identically.
/// </summary>
public abstract class RiskServiceBase(IBaseeraDbContext db, ICurrentUser currentUser, IOrganizationalScopeService scope, IAuditService audit)
{
    protected const string Module = "RiskManagement";
    protected const string FacilityNotFoundMessage = "السجن غير موجود أو خارج نطاق صلاحياتك.";
    protected const string RiskNotFoundMessage = "الخطر غير موجود أو خارج نطاق صلاحياتك.";
    protected const string OrganizationNotFoundMessage = "المنظمة غير موجودة أو خارج نطاق صلاحياتك.";
    protected const string MissingPermissionMessage = "لا تملك الصلاحية اللازمة لتنفيذ هذا الإجراء.";

    protected IBaseeraDbContext Db => db;
    protected ICurrentUser User => currentUser;
    protected IOrganizationalScopeService Scope => scope;

    protected void Require(string permission)
    {
        if (!currentUser.HasPermission(permission))
        {
            throw new UnauthorizedAccessException(MissingPermissionMessage);
        }
    }

    protected async Task<Facility> EnsureFacilityVisibleAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        var facility = await db.Facilities.AsNoTracking()
            .Include(f => f.Region)
            .FirstOrDefaultAsync(f => f.Id == facilityId, cancellationToken)
            ?? throw new KeyNotFoundException(FacilityNotFoundMessage);

        if (!scope.CanAccessFacility(facilityId))
        {
            throw new KeyNotFoundException(FacilityNotFoundMessage);
        }

        return facility;
    }

    /// <summary>
    /// Organization-level visibility check for org-wide resources (risk matrices) that carry no facility
    /// anchor of their own. Composes the already-existing FilterRegions/FilterFacilities scope filters
    /// instead of re-deriving scope logic here, so it stays in lock-step with every other scope decision
    /// made by IOrganizationalScopeService. Existence and scope are both folded into one KeyNotFoundException
    /// (never 403) so an out-of-scope organization is indistinguishable from one that does not exist.
    /// </summary>
    protected async Task EnsureOrganizationVisibleAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var organizationExists = await db.Organizations.AsNoTracking()
            .AnyAsync(organization => organization.Id == organizationId && !organization.IsDeleted, cancellationToken);
        if (!organizationExists)
        {
            throw new KeyNotFoundException(OrganizationNotFoundMessage);
        }

        if (scope.HasNationalAccess || scope.HasHeadquartersAccess)
        {
            return;
        }

        var canAccessRegion = await scope.FilterRegions(db.Regions)
            .AnyAsync(region => region.OrganizationId == organizationId, cancellationToken);
        if (canAccessRegion)
        {
            return;
        }

        var canAccessFacility = await scope.FilterFacilities(db.Facilities)
            .AnyAsync(facility => facility.Region.OrganizationId == organizationId, cancellationToken);
        if (!canAccessFacility)
        {
            throw new KeyNotFoundException(OrganizationNotFoundMessage);
        }
    }

    protected async Task<RiskRecord> EnsureRiskVisibleAsync(Guid facilityId, Guid riskId, CancellationToken cancellationToken)
    {
        var risk = await db.RiskRecords
            .Include(r => r.Facility)
            .FirstOrDefaultAsync(r => r.Id == riskId, cancellationToken)
            ?? throw new KeyNotFoundException(RiskNotFoundMessage);

        if (risk.FacilityId != facilityId || !scope.CanAccessFacility(facilityId))
        {
            throw new KeyNotFoundException(RiskNotFoundMessage);
        }

        return risk;
    }

    /// <summary>
    /// Validates that a workforce member proposed as a treatment plan owner or action assignee is actually
    /// stationed at (or operating out of) the given facility — not merely somewhere in the organization —
    /// so a cross-facility member's name/details never leak back through RiskTreatmentPlanDto/RiskTreatmentActionDto
    /// to a caller scoped to a different facility.
    /// </summary>
    protected async Task EnsureWorkforceMemberInFacilityAsync(Guid facilityId, Guid workforceMemberId, string errorMessage, CancellationToken cancellationToken)
    {
        var inFacility = await db.WorkforceMembers.AsNoTracking()
            .AnyAsync(w => w.Id == workforceMemberId && (w.CurrentOperationalFacilityId == facilityId || w.HomeFacilityId == facilityId), cancellationToken);
        if (!inFacility)
        {
            throw new InvalidOperationException(errorMessage);
        }
    }

    /// <summary>
    /// Validates that an owner (workforce member and/or user) proposed for a risk or control belongs to the
    /// risk's own organization, rather than merely existing anywhere in the system. Centralized here so
    /// RiskCommandService.CreateAsync/AssignOwnerAsync and RiskControlService.CreateAsync enforce the same
    /// rule instead of re-deriving it at each call site.
    /// </summary>
    protected async Task EnsureOwnerAssignableAsync(Guid organizationId, Guid? workforceMemberId, Guid? userId, CancellationToken cancellationToken)
    {
        if (workforceMemberId is Guid memberId)
        {
            var memberInOrganization = await db.WorkforceMembers.AsNoTracking()
                .AnyAsync(w => w.Id == memberId && w.OrganizationId == organizationId, cancellationToken);
            if (!memberInOrganization)
            {
                throw new InvalidOperationException("عضو القوى البشرية المحدد كمالك غير موجود ضمن نطاق المنظمة.");
            }
        }

        if (userId is Guid ownerUserId)
        {
            var userInOrganization = await db.Users.AsNoTracking()
                .AnyAsync(u => u.Id == ownerUserId &&
                    (u.UserScopes.Any(s => s.ScopeType == ScopeType.Global) ||
                     u.UserScopes.Any(s => s.RegionId.HasValue && db.Regions.Any(r => r.Id == s.RegionId && r.OrganizationId == organizationId)) ||
                     u.UserScopes.Any(s => s.FacilityId.HasValue && db.Facilities.Any(f => f.Id == s.FacilityId && f.Region.OrganizationId == organizationId))),
                    cancellationToken);
            if (!userInOrganization)
            {
                throw new InvalidOperationException("المستخدم المحدد كمالك غير موجود ضمن نطاق المنظمة.");
            }
        }
    }

    protected string ActorReference() =>
        currentUser.ExternalSubject ?? currentUser.DisplayName ?? currentUser.UserId?.ToString() ?? "unknown";

    /// <summary>Four-eyes: an actor may never approve/decide their own submission.</summary>
    protected void EnforceFourEyes(string submittedBy)
    {
        if (string.Equals(submittedBy, ActorReference(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("لا يمكن لمنشئ العملية اعتمادها أو مراجعتها (مبدأ الفصل بين المهام).");
        }
    }

    protected static void EnsureCurrentRowVersion(EntityBase entity, string rowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersion))
        {
            throw new InvalidOperationException("RowVersion مطلوب.");
        }

        byte[] expected;
        try
        {
            expected = Convert.FromBase64String(rowVersion);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("RowVersion غير صالح.", ex);
        }

        if (!expected.SequenceEqual(entity.RowVersion))
        {
            throw new InvalidOperationException("تم تعديل السجل بواسطة مستخدم آخر. أعد تحميل البيانات والمحاولة مجددًا.");
        }
    }

    protected async Task AuditAsync(string action, string entityType, Guid entityId, object? newValues, CancellationToken cancellationToken)
    {
        await audit.WriteAsync(new AuditEntry
        {
            Action = action,
            Module = Module,
            EntityType = entityType,
            EntityId = entityId.ToString(),
            NewValues = newValues,
            IsSensitiveView = false
        },
            cancellationToken);
    }
}
