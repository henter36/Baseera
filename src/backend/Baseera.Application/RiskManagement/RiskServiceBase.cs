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
