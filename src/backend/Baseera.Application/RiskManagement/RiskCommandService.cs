namespace Baseera.Application.RiskManagement;

using Baseera.Application.Abstractions;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.RiskManagement;
using Microsoft.EntityFrameworkCore;

public sealed class RiskCommandService(IBaseeraDbContext db, ICurrentUser currentUser, IOrganizationalScopeService scope, IAuditService audit)
    : RiskServiceBase(db, currentUser, scope, audit), IRiskCommandService
{
    public async Task<Guid> CreateCategoryAsync(Guid organizationId, RiskCategoryUpsertRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksManageCategories);

        if (request.ParentCategoryId is Guid parentId)
        {
            var parentExists = await Db.RiskCategories.AnyAsync(c => c.Id == parentId && c.OrganizationId == organizationId, cancellationToken);
            if (!parentExists)
            {
                throw new InvalidOperationException("التصنيف الأصل غير موجود.");
            }
        }

        var code = request.Code.Trim();
        var codeTaken = await Db.RiskCategories.AnyAsync(c => c.OrganizationId == organizationId && c.Code == code, cancellationToken);
        if (codeTaken)
        {
            throw new InvalidOperationException("رمز التصنيف مستخدم بالفعل.");
        }

        var category = new Domain.RiskManagement.RiskCategory
        {
            OrganizationId = organizationId,
            Code = code,
            NameAr = request.NameAr.Trim(),
            NameEn = request.NameEn,
            ParentCategoryId = request.ParentCategoryId,
            Description = request.Description,
            DisplayOrder = request.DisplayOrder,
            CreatedBy = ActorReference()
        };

        Db.Add(category);
        await Db.SaveChangesAsync(cancellationToken);
        await AuditAsync(RiskAuditActions.RiskCategoryCreated, nameof(Domain.RiskManagement.RiskCategory), category.Id, new { category.Code }, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
        return category.Id;
    }

    public async Task<Guid> CreateAsync(Guid facilityId, RiskCreateRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksCreate);
        var facility = await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var organizationId = facility.Region.OrganizationId;

        var category = await Db.RiskCategories
            .FirstOrDefaultAsync(c => c.Id == request.RiskCategoryId && c.OrganizationId == organizationId, cancellationToken)
            ?? throw new InvalidOperationException("تصنيف الخطر غير موجود ضمن نطاق المنشأة.");

        if (request.FacilityUnitId is Guid unitId)
        {
            var unitExists = await Db.FacilityUnits.AnyAsync(u => u.Id == unitId && u.FacilityId == facilityId, cancellationToken);
            if (!unitExists)
            {
                throw new InvalidOperationException("الوحدة المحددة لا تتبع هذا السجن.");
            }
        }

        await EnsureOwnerAssignableAsync(organizationId, request.OwnerWorkforceMemberId, null, cancellationToken);

        var sequence = await Db.NextRiskRecordSequenceValueAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var risk = new RiskRecord
        {
            OrganizationId = organizationId,
            RiskCode = $"RSK-{sequence:00000000}",
            Title = request.Title.Trim(),
            Description = request.Description,
            RiskCategoryId = request.RiskCategoryId,
            RiskType = request.RiskType,
            ScopeLevel = ScopeType.Facility,
            FacilityId = facilityId,
            FacilityUnitId = request.FacilityUnitId,
            OwnerWorkforceMemberId = request.OwnerWorkforceMemberId,
            Status = RiskStatus.Draft,
            ConfidentialityLevel = request.ConfidentialityLevel,
            SourceType = request.SourceType,
            SourceReference = request.SourceReference,
            FirstIdentifiedAtUtc = now,
            DataFreshAsOfUtc = now,
            RecurrenceKey = RiskRecurrenceKeyBuilder.Build(category.Code, facilityId, request.Title),
            CreatedBy = ActorReference()
        };

        Db.Add(risk);
        await Db.SaveChangesAsync(cancellationToken);
        await AuditAsync(RiskAuditActions.RiskCreated, nameof(RiskRecord), risk.Id, new { risk.RiskCode, RiskType = risk.RiskType.ToString() }, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
        return risk.Id;
    }

    public async Task UpdateAsync(Guid facilityId, Guid riskId, RiskUpdateRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksUpdate);
        var risk = await EnsureRiskVisibleAsync(facilityId, riskId, cancellationToken);
        EnsureCurrentRowVersion(risk, request.RowVersion);

        var category = await Db.RiskCategories
            .FirstOrDefaultAsync(c => c.Id == request.RiskCategoryId && c.OrganizationId == risk.OrganizationId, cancellationToken)
            ?? throw new InvalidOperationException("تصنيف الخطر غير موجود ضمن نطاق المنشأة.");

        if (request.FacilityUnitId is Guid unitId)
        {
            var unitExists = await Db.FacilityUnits.AnyAsync(u => u.Id == unitId && u.FacilityId == facilityId, cancellationToken);
            if (!unitExists)
            {
                throw new InvalidOperationException("الوحدة المحددة لا تتبع هذا السجن.");
            }
        }

        risk.Title = request.Title.Trim();
        risk.Description = request.Description;
        risk.RiskCategoryId = request.RiskCategoryId;
        risk.RiskType = request.RiskType;
        risk.ConfidentialityLevel = request.ConfidentialityLevel;
        risk.FacilityUnitId = request.FacilityUnitId;
        risk.RecurrenceKey = RiskRecurrenceKeyBuilder.Build(category.Code, facilityId, request.Title);
        risk.UpdatedBy = ActorReference();
        risk.UpdatedAtUtc = DateTimeOffset.UtcNow;

        Db.Update(risk);
        await Db.SaveChangesAsync(cancellationToken);
        await AuditAsync(RiskAuditActions.RiskUpdated, nameof(RiskRecord), risk.Id, new { risk.RiskCode }, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
    }

    public async Task ExecuteCommandAsync(Guid facilityId, Guid riskId, RiskCommandRequest request, CancellationToken cancellationToken = default)
    {
        var risk = await EnsureRiskVisibleAsync(facilityId, riskId, cancellationToken);
        EnsureCurrentRowVersion(risk, request.RowVersion);

        switch (request.Command)
        {
            case RiskCommandTypes.AssignOwner:
                await AssignOwnerAsync(risk, request, cancellationToken);
                break;
            case RiskCommandTypes.StartMonitoring:
                await StartMonitoringAsync(risk, cancellationToken);
                break;
            case RiskCommandTypes.Escalate:
                await EscalateAsync(risk, request, cancellationToken);
                break;
            case RiskCommandTypes.Reopen:
                await ReopenAsync(risk, request, cancellationToken);
                break;
            case RiskCommandTypes.Archive:
                await ArchiveAsync(risk, cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"أمر غير معروف: {request.Command}.");
        }
    }

    private async Task AssignOwnerAsync(RiskRecord risk, RiskCommandRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.RisksAssignOwner);

        if (request.OwnerWorkforceMemberId is null && request.OwnerUserId is null)
        {
            throw new InvalidOperationException("يجب تحديد مالك (عضو قوى بشرية أو مستخدم).");
        }

        await EnsureOwnerAssignableAsync(risk.OrganizationId, request.OwnerWorkforceMemberId, request.OwnerUserId, cancellationToken);

        risk.OwnerWorkforceMemberId = request.OwnerWorkforceMemberId;
        risk.OwnerUserId = request.OwnerUserId;
        risk.UpdatedBy = ActorReference();
        risk.UpdatedAtUtc = DateTimeOffset.UtcNow;

        Db.Update(risk);
        await Db.SaveChangesAsync(cancellationToken);
        await AuditAsync(RiskAuditActions.RiskOwnerAssigned, nameof(RiskRecord), risk.Id, new { risk.OwnerWorkforceMemberId, risk.OwnerUserId }, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
    }

    private async Task StartMonitoringAsync(RiskRecord risk, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.RisksUpdate);
        await TransitionAsync(risk, RiskStatus.Monitoring, "بدء المتابعة.", cancellationToken);
    }

    private async Task EscalateAsync(RiskRecord risk, RiskCommandRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.RisksEscalate);
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new InvalidOperationException("مبرر التصعيد مطلوب.");
        }

        await AuditAsync(RiskAuditActions.RiskEscalated, nameof(RiskRecord), risk.Id, new { Reason = request.Reason }, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
    }

    private async Task ReopenAsync(RiskRecord risk, RiskCommandRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.RisksReopen);
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new InvalidOperationException("يلزم توثيق سبب/دليل إعادة الفتح.");
        }

        RiskLifecycleStateMachine.EnsureAllowed(risk.Status, RiskStatus.Reopened);
        var fromStatus = risk.Status;
        risk.Status = RiskStatus.Reopened;
        risk.ReopenedCount += 1;
        risk.LastReopenedAtUtc = DateTimeOffset.UtcNow;
        risk.LastReopenReason = request.Reason;
        risk.ClosedAtUtc = null;
        risk.ClosedBy = null;
        Db.Add(new RiskStatusHistory
        {
            RiskRecordId = risk.Id,
            FromStatus = fromStatus,
            ToStatus = RiskStatus.Reopened,
            ChangedBy = ActorReference(),
            Reason = request.Reason
        });

        // A reopened risk must immediately re-enter assessment before it can be considered active again.
        RiskLifecycleStateMachine.EnsureAllowed(RiskStatus.Reopened, RiskStatus.UnderAssessment);
        risk.Status = RiskStatus.UnderAssessment;
        Db.Add(new RiskStatusHistory
        {
            RiskRecordId = risk.Id,
            FromStatus = RiskStatus.Reopened,
            ToStatus = RiskStatus.UnderAssessment,
            ChangedBy = ActorReference(),
            Reason = "إعادة الفتح تستوجب تقييمًا جديدًا."
        });

        Db.Update(risk);
        await Db.SaveChangesAsync(cancellationToken);
        await AuditAsync(RiskAuditActions.RiskReopened, nameof(RiskRecord), risk.Id, new { Reason = request.Reason }, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
    }

    private async Task ArchiveAsync(RiskRecord risk, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.RisksUpdate);
        await TransitionAsync(risk, RiskStatus.Archived, "أرشفة الخطر.", cancellationToken);
    }

    private async Task TransitionAsync(RiskRecord risk, RiskStatus to, string reason, CancellationToken cancellationToken)
    {
        var from = risk.Status;
        RiskLifecycleStateMachine.EnsureAllowed(from, to);
        risk.Status = to;
        risk.UpdatedBy = ActorReference();
        risk.UpdatedAtUtc = DateTimeOffset.UtcNow;
        Db.Add(new RiskStatusHistory
        {
            RiskRecordId = risk.Id,
            FromStatus = from,
            ToStatus = to,
            ChangedBy = ActorReference(),
            Reason = reason
        });
        Db.Update(risk);
        await Db.SaveChangesAsync(cancellationToken);
        await AuditAsync(RiskAuditActions.RiskStatusChanged, nameof(RiskRecord), risk.Id, new { From = from.ToString(), To = to.ToString() }, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
    }
}
