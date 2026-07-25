namespace Baseera.Application.Resources;

using Baseera.Application.Abstractions;
using Baseera.Domain.Identity;
using Baseera.Domain.Resources;
using Microsoft.EntityFrameworkCore;

public interface IResourceReadinessQueryService
{
    Task<ResourceWorkspacePayload> GetWorkspacePayloadAsync(Guid facilityId, CancellationToken cancellationToken);
    Task<ResourceSummaryDto> GetSummaryAsync(Guid facilityId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ResourceCategoryReadinessDto>> GetCategoriesAsync(Guid facilityId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ResourceExceptionDto>> GetExceptionsAsync(Guid facilityId, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<ResourceUnitDistributionDto>> GetUnitDistributionAsync(Guid facilityId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ResourceActivityDto>> GetTimelineAsync(Guid facilityId, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<ResourceAssetListItemDto>> ListAssetsAsync(Guid facilityId, ResourceType? resourceType, string? search, int pageSize, CancellationToken cancellationToken);
    Task<ResourceAssetDetailDto> GetAssetAsync(Guid facilityId, Guid assetId, CancellationToken cancellationToken);
}

public interface IResourceAssetCommandService
{
    Task<Guid> CreateAssetAsync(Guid facilityId, ResourceAssetCreateRequest request, CancellationToken cancellationToken);
    Task ChangeStatusAsync(Guid facilityId, Guid assetId, ResourceStatusChangeRequest request, CancellationToken cancellationToken);
    Task PlaceAssetAsync(Guid facilityId, Guid assetId, ResourcePlacementRequest request, CancellationToken cancellationToken);
}

public interface IMaintenanceWorkOrderService
{
    Task<Guid> CreateWorkOrderAsync(Guid facilityId, MaintenanceWorkOrderRequest request, CancellationToken cancellationToken);
}

public interface IResourceRequirementService
{
    Task<Guid> RecordRequirementAsync(Guid facilityId, ResourceRequirementRequest request, CancellationToken cancellationToken);
}

public interface IResourceImportService
{
    Task<ResourceImportResult> PreviewAsync(Guid facilityId, ResourceImportPreviewRequest request, CancellationToken cancellationToken);
    Task<ResourceImportResult> ConfirmAsync(Guid facilityId, ResourceImportPreviewRequest request, CancellationToken cancellationToken);
}

public sealed class ResourceReadinessOptions
{
    public int StaleVerificationDays { get; init; } = 30;
    public int ExceptionLimit { get; init; } = 20;
    public int TimelineLimit { get; init; } = 50;
    public int AssetPageSizeLimit { get; init; } = 100;
}

public sealed class ResourceReadinessService(
    IBaseeraDbContext db,
    IOrganizationalScopeService scope,
    ICurrentUser currentUser,
    IAuditService audit,
    TimeProvider timeProvider)
    : IResourceReadinessQueryService,
      IResourceAssetCommandService,
      IMaintenanceWorkOrderService,
      IResourceRequirementService,
      IResourceImportService
{
    private readonly ResourceReadinessOptions options = new();

    public async Task<ResourceWorkspacePayload> GetWorkspacePayloadAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.ResourcesViewSummary);
        var summary = await GetSummaryAsync(facilityId, cancellationToken);
        var categories = currentUser.HasPermission(PermissionCodes.ResourcesViewAssets)
            ? await GetCategoriesAsync(facilityId, cancellationToken)
            : [];
        var exceptions = currentUser.HasPermission(PermissionCodes.ResourcesViewAssets)
            ? await GetExceptionsAsync(facilityId, options.ExceptionLimit, cancellationToken)
            : [];
        var distribution = currentUser.HasPermission(PermissionCodes.ResourcesViewAssets)
            ? await GetUnitDistributionAsync(facilityId, cancellationToken)
            : [];
        var timeline = currentUser.HasPermission(PermissionCodes.ResourcesViewMaintenance)
            ? await GetTimelineAsync(facilityId, options.TimelineLimit, cancellationToken)
            : [];

        return new ResourceWorkspacePayload
        {
            Summary = summary,
            Categories = categories,
            Exceptions = exceptions,
            UnitDistribution = distribution,
            Timeline = timeline
        };
    }

    public async Task<ResourceSummaryDto> GetSummaryAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.ResourcesViewSummary);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var rows = await AssetsInFacility(facilityId)
            .GroupBy(_ => 1)
            .Select(group => new ResourceStatusCounts(
                group.Count(),
                group.Count(a => a.CurrentStatus == ResourceStatus.Available),
                group.Count(a => a.CurrentStatus == ResourceStatus.Standby),
                group.Count(a => a.CurrentStatus == ResourceStatus.InUse),
                group.Count(a => a.CurrentStatus == ResourceStatus.Reserved),
                group.Count(a => a.CurrentStatus == ResourceStatus.UnderMaintenance),
                group.Count(a => a.CurrentStatus == ResourceStatus.OutOfService),
                group.Count(a => a.CurrentStatus == ResourceStatus.AwaitingParts),
                group.Count(a => a.CurrentStatus == ResourceStatus.Unknown),
                group.Count(a => a.CurrentStatus == ResourceStatus.Retired),
                group.Count(a => a.CurrentStatus == ResourceStatus.Transferred),
                group.Count(a => a.Criticality == ResourceCriticality.MissionCritical
                    && (a.CurrentStatus == ResourceStatus.OutOfService
                        || a.CurrentStatus == ResourceStatus.UnderMaintenance
                        || a.CurrentStatus == ResourceStatus.AwaitingParts)),
                group.Count(a => a.LastVerifiedAtUtc == null || a.LastVerifiedAtUtc < now.AddDays(-options.StaleVerificationDays)),
                group.Count(a => a.AssetCode == "" || a.DisplayName == "" || a.OperationalFacilityId == null || a.CurrentStatus == ResourceStatus.Unknown)))
            .FirstOrDefaultAsync(cancellationToken);
        rows ??= ResourceStatusCounts.Empty;

        var required = await ActiveRequirements(facilityId, now)
            .SumAsync(requirement => (int?)requirement.RequiredQuantity, cancellationToken) ?? 0;

        var readiness = ResourceReadinessPolicy.Calculate(new ResourceReadinessInputs(
            rows.Total,
            rows.Available,
            rows.Standby,
            rows.InUse,
            rows.Reserved,
            rows.UnderMaintenance,
            rows.OutOfService,
            rows.AwaitingParts,
            rows.Unknown,
            rows.Retired,
            rows.Transferred,
            required,
            rows.MissingData));

        var warnings = BuildSummaryWarnings(rows, required);
        return new ResourceSummaryDto
        {
            FacilityId = facilityId,
            TotalRegistered = rows.Total,
            Operational = readiness.Operational,
            Available = rows.Available,
            Standby = rows.Standby,
            InUse = rows.InUse,
            UnderMaintenance = rows.UnderMaintenance,
            OutOfService = rows.OutOfService,
            AwaitingParts = rows.AwaitingParts,
            Unknown = rows.Unknown,
            Retired = rows.Retired,
            Required = required,
            Gap = readiness.Gap,
            Surplus = readiness.Surplus,
            ReadinessRate = readiness.ReadinessRate,
            AvailabilityRate = readiness.AvailabilityRate,
            DataCompletenessRate = readiness.DataCompletenessRate,
            MissionCriticalUnavailable = rows.MissionCriticalUnavailable,
            StaleRecords = rows.StaleRecords,
            MissingDataRecords = rows.MissingData,
            FreshnessStatus = rows.Total == 0 ? "missing" : rows.StaleRecords > 0 ? "partial" : "current",
            ConfidenceLevel = rows.Total == 0 ? "unknown" : warnings.Count > 0 ? "medium" : "high",
            IsPartial = warnings.Count > 0,
            Warnings = warnings,
            GeneratedAtUtc = now,
            DataEffectiveAtUtc = await AssetsInFacility(facilityId).MaxAsync(a => (DateTimeOffset?)a.LastVerifiedAtUtc, cancellationToken)
        };
    }

    public async Task<IReadOnlyList<ResourceCategoryReadinessDto>> GetCategoriesAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.ResourcesViewAssets);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var requirements = await ActiveRequirements(facilityId, now)
            .GroupBy(r => r.ResourceType)
            .Select(g => new { Type = g.Key, Required = g.Sum(r => r.RequiredQuantity) })
            .ToDictionaryAsync(x => x.Type, x => x.Required, cancellationToken);

        var rows = await AssetsInFacility(facilityId)
            .GroupBy(a => a.ResourceType)
            .Select(g => new ResourceCategoryCounts(
                g.Key,
                g.Count(),
                g.Count(a => a.CurrentStatus == ResourceStatus.Available),
                g.Count(a => a.CurrentStatus == ResourceStatus.Standby),
                g.Count(a => a.CurrentStatus == ResourceStatus.InUse),
                g.Count(a => a.CurrentStatus == ResourceStatus.Reserved),
                g.Count(a => a.CurrentStatus == ResourceStatus.UnderMaintenance),
                g.Count(a => a.CurrentStatus == ResourceStatus.OutOfService),
                g.Count(a => a.CurrentStatus == ResourceStatus.AwaitingParts),
                g.Count(a => a.CurrentStatus == ResourceStatus.Unknown),
                g.Count(a => a.CurrentStatus == ResourceStatus.Retired),
                g.Count(a => a.CurrentStatus == ResourceStatus.Transferred),
                g.Count(a => a.LastVerifiedAtUtc == null || a.LastVerifiedAtUtc < now.AddDays(-options.StaleVerificationDays))))
            .ToListAsync(cancellationToken);

        return Enum.GetValues<ResourceType>()
            .Select(type => BuildCategory(type, rows.FirstOrDefault(r => r.Type == type), requirements.GetValueOrDefault(type)))
            .ToList();
    }

    public async Task<IReadOnlyList<ResourceExceptionDto>> GetExceptionsAsync(Guid facilityId, int limit, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.ResourcesViewAssets);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var staleBefore = now.AddDays(-options.StaleVerificationDays);
        var boundedLimit = Math.Clamp(limit, 1, 50);

        var assetExceptions = await AssetsInFacility(facilityId)
            .Where(a => a.CurrentStatus == ResourceStatus.OutOfService
                || a.CurrentStatus == ResourceStatus.AwaitingParts
                || a.CurrentStatus == ResourceStatus.UnderMaintenance
                || a.CurrentStatus == ResourceStatus.Unknown
                || a.LastVerifiedAtUtc == null
                || a.LastVerifiedAtUtc < staleBefore)
            .OrderByDescending(a => a.Criticality)
            .ThenBy(a => a.LastVerifiedAtUtc)
            .Take(boundedLimit)
            .Select(a => new ResourceExceptionDto
            {
                Type = ExceptionType(a.CurrentStatus, a.LastVerifiedAtUtc, staleBefore),
                ResourceAssetId = a.Id,
                ResourceType = a.ResourceType,
                Reference = a.AssetCode,
                TitleAr = a.DisplayName,
                SeverityAr = a.Criticality == ResourceCriticality.MissionCritical ? "حرجة" : a.Criticality == ResourceCriticality.High ? "عالية" : "متوسطة",
                PriorityRank = a.Criticality == ResourceCriticality.MissionCritical ? 950 : a.CurrentStatus == ResourceStatus.OutOfService ? 850 : 650,
                ReasonAr = ExceptionReason(a.CurrentStatus, a.LastVerifiedAtUtc, staleBefore),
                OwnerAr = null,
                DueAtUtc = null,
                ActionLabelAr = "فتح المورد"
            })
            .ToListAsync(cancellationToken);

        return assetExceptions;
    }

    public async Task<IReadOnlyList<ResourceUnitDistributionDto>> GetUnitDistributionAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.ResourcesViewAssets);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var rows = await AssetsInFacility(facilityId)
            .GroupBy(a => new { a.OperationalFacilityUnitId, UnitName = a.OperationalFacilityUnit == null ? "غير محدد" : a.OperationalFacilityUnit.NameAr })
            .Select(g => new
            {
                g.Key.OperationalFacilityUnitId,
                g.Key.UnitName,
                Vehicles = g.Count(a => a.ResourceType == ResourceType.Vehicle),
                Communications = g.Count(a => a.ResourceType == ResourceType.CommunicationDevice),
                Equipment = g.Count(a => a.ResourceType == ResourceType.OperationalEquipment || a.ResourceType == ResourceType.SecurityEquipment),
                FacilityAssets = g.Count(a => a.ResourceType == ResourceType.FacilityAsset),
                Operational = g.Count(a => a.CurrentStatus == ResourceStatus.Available || a.CurrentStatus == ResourceStatus.InUse || a.CurrentStatus == ResourceStatus.Standby || a.CurrentStatus == ResourceStatus.Reserved),
                Total = g.Count(),
                Critical = g.Count(a => a.Criticality == ResourceCriticality.MissionCritical && (a.CurrentStatus == ResourceStatus.OutOfService || a.CurrentStatus == ResourceStatus.AwaitingParts || a.CurrentStatus == ResourceStatus.UnderMaintenance))
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new ResourceUnitDistributionDto
            {
                FacilityUnitId = row.OperationalFacilityUnitId,
                UnitNameAr = row.UnitName,
                Vehicles = row.Vehicles,
                CommunicationDevices = row.Communications,
                Equipment = row.Equipment,
                FacilityAssets = row.FacilityAssets,
                ReadinessRate = row.Total > 0 ? Math.Round((decimal)row.Operational / row.Total, 4, MidpointRounding.AwayFromZero) : null,
                Gap = 0,
                CriticalExceptions = row.Critical
            })
            .OrderByDescending(row => row.CriticalExceptions)
            .ThenBy(row => row.UnitNameAr, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<IReadOnlyList<ResourceActivityDto>> GetTimelineAsync(Guid facilityId, int limit, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.ResourcesViewMaintenance);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var boundedLimit = Math.Clamp(limit, 1, 50);
        var statusEvents = await db.ResourceStatusEvents
            .AsNoTracking()
            .Where(e => e.ResourceAsset.OperationalFacilityId == facilityId && !e.ResourceAsset.IsDeleted)
            .OrderByDescending(e => e.OccurredAtUtc)
            .Take(boundedLimit)
            .Select(e => new ResourceActivityDto
            {
                EventType = "resource-status-changed",
                TitleAr = "تغيرت حالة مورد",
                DescriptionAr = e.ResourceAsset.DisplayName + " إلى " + ResourceStatusStateMachine.StatusAr(e.NewStatus),
                OccurredAtUtc = e.OccurredAtUtc,
                EntityReference = e.ResourceAsset.AssetCode,
                Tone = e.NewStatus == ResourceStatus.OutOfService ? "danger" : "info",
                ResourceAssetId = e.ResourceAssetId
            })
            .ToListAsync(cancellationToken);

        return statusEvents;
    }

    public async Task<IReadOnlyList<ResourceAssetListItemDto>> ListAssetsAsync(Guid facilityId, ResourceType? resourceType, string? search, int pageSize, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.ResourcesViewAssets);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var query = AssetsInFacility(facilityId);
        if (resourceType.HasValue)
        {
            query = query.Where(asset => asset.ResourceType == resourceType.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(asset => asset.AssetCode.Contains(term) || asset.DisplayName.Contains(term));
        }

        var limit = Math.Clamp(pageSize, 1, options.AssetPageSizeLimit);
        var maintenanceAssetIds = await db.MaintenanceWorkOrders
            .AsNoTracking()
            .Where(order => !order.IsDeleted
                && order.Status != MaintenanceStatus.Completed
                && order.Status != MaintenanceStatus.Cancelled
                && order.Status != MaintenanceStatus.Rejected
                && order.ResourceAsset.OperationalFacilityId == facilityId)
            .Select(order => order.ResourceAssetId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var maintenanceSet = maintenanceAssetIds.ToHashSet();

        var rows = await query
            .OrderBy(asset => asset.ResourceType)
            .ThenBy(asset => asset.AssetCode)
            .Take(limit)
            .Select(asset => new AssetProjection(
                asset.Id,
                asset.ResourceType,
                asset.AssetCode,
                asset.DisplayName,
                asset.SerialNumber,
                asset.VehicleProfile == null ? null : asset.VehicleProfile.PlateNumber,
                asset.CurrentStatus,
                asset.Condition,
                asset.Criticality,
                asset.OperationalFacilityUnit == null ? null : asset.OperationalFacilityUnit.NameAr,
                asset.CustodianUser == null ? null : asset.CustodianUser.DisplayNameAr,
                asset.LastVerifiedAtUtc))
            .ToListAsync(cancellationToken);

        return rows.Select(row => ToListItem(row, maintenanceSet.Contains(row.Id))).ToList();
    }

    public async Task<ResourceAssetDetailDto> GetAssetAsync(Guid facilityId, Guid assetId, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.ResourcesViewAssets);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var asset = await AssetsInFacility(facilityId)
            .Where(a => a.Id == assetId)
            .Select(a => new AssetProjection(
                a.Id,
                a.ResourceType,
                a.AssetCode,
                a.DisplayName,
                a.SerialNumber,
                a.VehicleProfile == null ? null : a.VehicleProfile.PlateNumber,
                a.CurrentStatus,
                a.Condition,
                a.Criticality,
                a.OperationalFacilityUnit == null ? null : a.OperationalFacilityUnit.NameAr,
                a.CustodianUser == null ? null : a.CustodianUser.DisplayNameAr,
                a.LastVerifiedAtUtc))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("المورد غير موجود.");

        var maintenance = await db.MaintenanceWorkOrders
            .AsNoTracking()
            .Where(order => order.ResourceAssetId == assetId && !order.IsDeleted)
            .OrderByDescending(order => order.ReportedAtUtc)
            .Take(10)
            .Select(order => new ResourceMaintenanceDto
            {
                Id = order.Id,
                WorkOrderNumber = order.WorkOrderNumber,
                MaintenanceType = order.MaintenanceType,
                Priority = order.Priority,
                Status = order.Status,
                ReportedAtUtc = order.ReportedAtUtc,
                ExpectedCompletionAtUtc = order.ExpectedCompletionAtUtc,
                CompletedAtUtc = order.CompletedAtUtc,
                IsOverdue = order.ExpectedCompletionAtUtc.HasValue && order.ExpectedCompletionAtUtc < timeProvider.GetUtcNow() && order.CompletedAtUtc == null,
                ProblemDescription = order.ProblemDescription
            })
            .ToListAsync(cancellationToken);

        var timeline = await db.ResourceStatusEvents
            .AsNoTracking()
            .Where(e => e.ResourceAssetId == assetId)
            .OrderByDescending(e => e.OccurredAtUtc)
            .Take(30)
            .Select(e => new ResourceActivityDto
            {
                EventType = "resource-status-changed",
                TitleAr = "تغيرت حالة المورد",
                DescriptionAr = e.Reason,
                OccurredAtUtc = e.OccurredAtUtc,
                EntityReference = asset.AssetCode,
                Tone = e.NewStatus == ResourceStatus.OutOfService ? "danger" : "info",
                ResourceAssetId = e.ResourceAssetId
            })
            .ToListAsync(cancellationToken);

        return new ResourceAssetDetailDto
        {
            Asset = ToListItem(asset, maintenance.Any(order => order.Status is not MaintenanceStatus.Completed and not MaintenanceStatus.Cancelled and not MaintenanceStatus.Rejected)),
            Maintenance = maintenance,
            Timeline = timeline,
            AllowedActions = BuildAllowedActions()
        };
    }

    public async Task<Guid> CreateAssetAsync(Guid facilityId, ResourceAssetCreateRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.ResourcesManageAssets);
        var facility = await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        ValidateAssetRequest(request);
        if (request.OperationalFacilityUnitId.HasValue)
        {
            await EnsureUnitInFacilityAsync(facilityId, request.OperationalFacilityUnitId.Value, cancellationToken);
        }

        var exists = await db.ResourceAssets.AnyAsync(
            asset => asset.OrganizationId == facility.OrganizationId
                && asset.AssetCode == request.AssetCode.Trim()
                && !asset.IsDeleted,
            cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("كود المورد مستخدم مسبقًا داخل المنظمة.");
        }

        var now = timeProvider.GetUtcNow();
        var asset = new ResourceAsset
        {
            OrganizationId = facility.OrganizationId,
            ResourceType = request.ResourceType,
            AssetCode = request.AssetCode.Trim(),
            DisplayName = request.DisplayName.Trim(),
            SerialNumber = string.IsNullOrWhiteSpace(request.SerialNumber) ? null : request.SerialNumber.Trim(),
            Manufacturer = request.Manufacturer,
            Model = request.Model,
            ManufactureYear = request.ManufactureYear,
            OwnershipOrganizationId = request.OwnershipOrganizationId,
            OperationalFacilityId = facilityId,
            OperationalFacilityUnitId = request.OperationalFacilityUnitId,
            CustodianUserId = request.CustodianUserId,
            CurrentStatus = request.CurrentStatus,
            Condition = request.Condition,
            Criticality = request.Criticality,
            SourceType = request.SourceType,
            SourceReference = request.SourceReference,
            LastVerifiedAtUtc = now,
            LastVerifiedBy = currentUser.DisplayName
        };
        db.Add(asset);
        db.Add(new ResourceStatusEvent
        {
            ResourceAssetId = asset.Id,
            PreviousStatus = null,
            NewStatus = asset.CurrentStatus,
            OccurredAtUtc = now,
            Reason = "إنشاء مورد",
            SourceType = asset.SourceType,
            SourceReference = asset.SourceReference,
            RecordedByUserId = currentUser.UserId,
            RecordedAtUtc = now
        });
        db.Add(new ResourcePlacement
        {
            ResourceAssetId = asset.Id,
            OwnershipOrganizationId = request.OwnershipOrganizationId,
            OperationalFacilityId = facilityId,
            OperationalFacilityUnitId = request.OperationalFacilityUnitId,
            AssignedToUserId = request.CustodianUserId,
            EffectiveFromUtc = now,
            AssignmentType = ResourceAssignmentType.Permanent,
            SourceReference = request.SourceReference,
            Reason = "موضع افتتاحي"
        });
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("ResourceCreated", "ResourceAsset", asset.Id, cancellationToken);
        return asset.Id;
    }

    public async Task ChangeStatusAsync(Guid facilityId, Guid assetId, ResourceStatusChangeRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.ResourcesManageStatus);
        var asset = await LoadAssetForUpdateAsync(facilityId, assetId, cancellationToken);
        if (!ResourceStatusStateMachine.CanTransition(asset.CurrentStatus, request.NewStatus, !string.IsNullOrWhiteSpace(request.Reason)))
        {
            throw new InvalidOperationException("انتقال حالة المورد غير مسموح دون سبب أو أمر صيانة.");
        }

        var previous = asset.CurrentStatus;
        asset.CurrentStatus = request.NewStatus;
        asset.UpdatedAtUtc = timeProvider.GetUtcNow();
        db.Add(new ResourceStatusEvent
        {
            ResourceAssetId = asset.Id,
            PreviousStatus = previous,
            NewStatus = request.NewStatus,
            OccurredAtUtc = request.OccurredAtUtc,
            Reason = request.Reason.Trim(),
            ReasonCode = request.ReasonCode,
            SourceType = request.SourceType,
            SourceReference = request.SourceReference,
            RecordedByUserId = currentUser.UserId,
            RecordedAtUtc = timeProvider.GetUtcNow()
        });
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("ResourceStatusChanged", "ResourceAsset", asset.Id, cancellationToken);
    }

    public async Task PlaceAssetAsync(Guid facilityId, Guid assetId, ResourcePlacementRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.ResourcesManagePlacements);
        var asset = await LoadAssetForUpdateAsync(facilityId, assetId, cancellationToken);
        if (request.OperationalFacilityUnitId.HasValue)
        {
            await EnsureUnitInFacilityAsync(facilityId, request.OperationalFacilityUnitId.Value, cancellationToken);
        }

        var activePlacements = await db.ResourcePlacements
            .Where(p => p.ResourceAssetId == assetId && p.EffectiveToUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var placement in activePlacements)
        {
            placement.EffectiveToUtc = request.EffectiveFromUtc;
        }

        asset.OwnershipOrganizationId = request.OwnershipOrganizationId;
        asset.OperationalFacilityId = facilityId;
        asset.OperationalFacilityUnitId = request.OperationalFacilityUnitId;
        db.Add(new ResourcePlacement
        {
            ResourceAssetId = assetId,
            OwnershipOrganizationId = request.OwnershipOrganizationId,
            OperationalFacilityId = facilityId,
            OperationalFacilityUnitId = request.OperationalFacilityUnitId,
            EffectiveFromUtc = request.EffectiveFromUtc,
            EffectiveToUtc = request.EffectiveToUtc,
            AssignmentType = request.AssignmentType,
            SourceReference = request.SourceReference,
            Reason = request.Reason
        });
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("ResourcePlacementChanged", "ResourceAsset", asset.Id, cancellationToken);
    }

    public async Task<Guid> CreateWorkOrderAsync(Guid facilityId, MaintenanceWorkOrderRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.ResourcesManageMaintenance);
        var asset = await LoadAssetForUpdateAsync(facilityId, request.ResourceAssetId, cancellationToken);
        var sequence = await db.MaintenanceWorkOrders.CountAsync(cancellationToken) + 1;
        var order = new MaintenanceWorkOrder
        {
            OrganizationId = asset.OrganizationId,
            ResourceAssetId = asset.Id,
            WorkOrderNumber = $"MWO-{sequence:000000}",
            MaintenanceType = request.MaintenanceType,
            Priority = request.Priority,
            Status = request.PartsRequired ? MaintenanceStatus.AwaitingParts : MaintenanceStatus.Open,
            ReportedAtUtc = request.ReportedAtUtc,
            ReportedByUserId = currentUser.UserId,
            ProblemDescription = request.ProblemDescription.Trim(),
            AssignedToUserId = request.AssignedToUserId,
            ExpectedCompletionAtUtc = request.ExpectedCompletionAtUtc,
            PartsRequired = request.PartsRequired,
            WaitingForPartsSinceUtc = request.PartsRequired ? request.ReportedAtUtc : null
        };
        db.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("MaintenanceWorkOrderCreated", "MaintenanceWorkOrder", order.Id, cancellationToken);
        return order.Id;
    }

    public async Task<Guid> RecordRequirementAsync(Guid facilityId, ResourceRequirementRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.ResourcesManageRequirements);
        var facility = await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        if (request.RequiredQuantity < 0 || request.MinimumOperationalQuantity < 0 || request.MinimumOperationalQuantity > request.RequiredQuantity)
        {
            throw new ArgumentException("قيم الاحتياج غير صحيحة.");
        }

        if (request.FacilityUnitId.HasValue)
        {
            await EnsureUnitInFacilityAsync(facilityId, request.FacilityUnitId.Value, cancellationToken);
        }

        var requirement = new ResourceRequirement
        {
            OrganizationId = facility.OrganizationId,
            FacilityId = facilityId,
            FacilityUnitId = request.FacilityUnitId,
            ResourceType = request.ResourceType,
            ResourceCategory = request.ResourceCategory.Trim(),
            RequiredQuantity = request.RequiredQuantity,
            MinimumOperationalQuantity = request.MinimumOperationalQuantity,
            EffectiveFromUtc = request.EffectiveFromUtc,
            EffectiveToUtc = request.EffectiveToUtc,
            SourceReference = request.SourceReference.Trim(),
            ApprovalReference = request.ApprovalReference,
            Notes = request.Notes
        };
        db.Add(requirement);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("ResourceRequirementChanged", "ResourceRequirement", requirement.Id, cancellationToken);
        return requirement.Id;
    }

    public async Task<ResourceImportResult> PreviewAsync(Guid facilityId, ResourceImportPreviewRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.ResourcesImport);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        return await ValidateImportAsync(facilityId, request, apply: false, cancellationToken);
    }

    public async Task<ResourceImportResult> ConfirmAsync(Guid facilityId, ResourceImportPreviewRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.ResourcesImport);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        return await ValidateImportAsync(facilityId, request, apply: true, cancellationToken);
    }

    private IQueryable<ResourceAsset> AssetsInFacility(Guid facilityId) =>
        db.ResourceAssets
            .AsNoTracking()
            .Where(asset => !asset.IsDeleted && asset.OperationalFacilityId == facilityId);

    private IQueryable<ResourceRequirement> ActiveRequirements(Guid facilityId, DateTimeOffset asOfUtc) =>
        db.ResourceRequirements
            .AsNoTracking()
            .Where(requirement => !requirement.IsDeleted
                && requirement.FacilityId == facilityId
                && requirement.EffectiveFromUtc <= asOfUtc
                && (requirement.EffectiveToUtc == null || requirement.EffectiveToUtc > asOfUtc));

    private async Task<FacilityScopeInfo> EnsureFacilityVisibleAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        if (!scope.CanAccessFacility(facilityId))
        {
            throw new KeyNotFoundException("السجن غير موجود.");
        }

        return await db.Facilities
            .AsNoTracking()
            .Where(f => f.Id == facilityId && f.IsActive)
            .Select(f => new FacilityScopeInfo(f.Id, f.Region.OrganizationId, f.RegionId))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("السجن غير موجود.");
    }

    private async Task EnsureUnitInFacilityAsync(Guid facilityId, Guid unitId, CancellationToken cancellationToken)
    {
        var exists = await db.FacilityUnits
            .AsNoTracking()
            .AnyAsync(unit => unit.Id == unitId && unit.FacilityId == facilityId && unit.IsActive, cancellationToken);
        if (!exists)
        {
            throw new KeyNotFoundException("الوحدة غير موجودة.");
        }
    }

    private async Task<ResourceAsset> LoadAssetForUpdateAsync(Guid facilityId, Guid assetId, CancellationToken cancellationToken)
    {
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var asset = await db.ResourceAssets
            .Where(a => a.Id == assetId && a.OperationalFacilityId == facilityId && !a.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("المورد غير موجود.");
        return asset;
    }

    private static ResourceCategoryReadinessDto BuildCategory(ResourceType type, ResourceCategoryCounts? row, int required)
    {
        row ??= ResourceCategoryCounts.Empty(type);
        var readiness = ResourceReadinessPolicy.Calculate(new ResourceReadinessInputs(
            row.Total,
            row.Available,
            row.Standby,
            row.InUse,
            row.Reserved,
            row.UnderMaintenance,
            row.OutOfService,
            row.AwaitingParts,
            row.Unknown,
            row.Retired,
            row.Transferred,
            required,
            row.StaleRecords));
        return new ResourceCategoryReadinessDto
        {
            ResourceType = type,
            ResourceTypeCode = type.ToString(),
            LabelAr = ResourceStatusStateMachine.TypeAr(type),
            Total = row.Total,
            Operational = readiness.Operational,
            Available = row.Available,
            UnderMaintenance = row.UnderMaintenance,
            OutOfService = row.OutOfService,
            AwaitingParts = row.AwaitingParts,
            Required = required,
            Gap = readiness.Gap,
            ReadinessRate = readiness.ReadinessRate,
            FreshnessStatus = row.Total == 0 ? "missing" : row.StaleRecords > 0 ? "partial" : "current",
            ConfidenceLevel = row.Total == 0 ? "unknown" : row.StaleRecords > 0 ? "medium" : "high"
        };
    }

    private static IReadOnlyList<string> BuildSummaryWarnings(ResourceStatusCounts rows, int required)
    {
        var warnings = new List<string>();
        if (rows.Total == 0)
        {
            warnings.Add("لا توجد سجلات موارد فعلية لهذا السجن.");
        }

        if (required == 0)
        {
            warnings.Add("لا يوجد baseline احتياج معتمد للموارد.");
        }

        if (rows.StaleRecords > 0)
        {
            warnings.Add("توجد سجلات موارد تحتاج تحققًا حديثًا.");
        }

        if (rows.MissingData > 0)
        {
            warnings.Add("توجد سجلات موارد ناقصة البيانات.");
        }

        if (rows.MissionCriticalUnavailable > 0)
        {
            warnings.Add("توجد موارد حرجة غير جاهزة.");
        }

        return warnings;
    }

    private static ResourceAssetListItemDto ToListItem(AssetProjection row, bool hasOpenMaintenance) =>
        new()
        {
            Id = row.Id,
            ResourceType = row.ResourceType,
            AssetCode = row.AssetCode,
            DisplayName = row.DisplayName,
            SerialNumber = row.SerialNumber,
            PlateNumber = row.PlateNumber,
            CurrentStatus = row.CurrentStatus,
            Condition = row.Condition,
            Criticality = row.Criticality,
            OperationalFacilityUnitNameAr = row.UnitNameAr,
            CustodianNameAr = row.CustodianNameAr,
            LastVerifiedAtUtc = row.LastVerifiedAtUtc,
            HasOpenMaintenance = hasOpenMaintenance,
            DataQualityIssues = DataQualityIssues(row)
        };

    private static IReadOnlyList<string> DataQualityIssues(AssetProjection row)
    {
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(row.AssetCode)) issues.Add("كود المورد مفقود.");
        if (string.IsNullOrWhiteSpace(row.DisplayName)) issues.Add("اسم المورد مفقود.");
        if (row.CurrentStatus == ResourceStatus.Unknown) issues.Add("حالة المورد غير معروفة.");
        if (row.LastVerifiedAtUtc is null) issues.Add("لا يوجد تحقق حديث.");
        return issues;
    }

    private IReadOnlyList<ResourceAllowedActionDto> BuildAllowedActions() =>
    [
        new("CHANGE_STATUS", "تحديث الحالة", currentUser.HasPermission(PermissionCodes.ResourcesManageStatus), "تحتاج صلاحية تحديث حالة الموارد."),
        new("PLACE", "تغيير الموقع التشغيلي", currentUser.HasPermission(PermissionCodes.ResourcesManagePlacements), "تحتاج صلاحية إدارة مواضع الموارد."),
        new("OPEN_MAINTENANCE", "فتح أمر صيانة", currentUser.HasPermission(PermissionCodes.ResourcesManageMaintenance), "تحتاج صلاحية إدارة الصيانة.")
    ];

    private async Task<ResourceImportResult> ValidateImportAsync(Guid facilityId, ResourceImportPreviewRequest request, bool apply, CancellationToken cancellationToken)
    {
        if (request.Rows.Count > 500)
        {
            throw new ArgumentException("الحد الأقصى للاستيراد 500 صف.");
        }

        var facility = await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var existingCodes = await AssetsInFacility(facilityId)
            .Select(asset => asset.AssetCode)
            .ToListAsync(cancellationToken);
        var codeSet = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requestCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        var valid = 0;
        var duplicate = 0;

        foreach (var row in request.Rows)
        {
            var code = row.AssetCode.Trim();
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(row.DisplayName))
            {
                errors.Add($"صف {row.AssetCode}: كود المورد والاسم مطلوبان.");
                continue;
            }

            if (!requestCodes.Add(code) || codeSet.Contains(code))
            {
                duplicate++;
                continue;
            }

            valid++;
            if (!apply)
            {
                continue;
            }

            db.Add(new ResourceAsset
            {
                OrganizationId = facility.OrganizationId,
                ResourceType = row.ResourceType,
                AssetCode = code,
                DisplayName = row.DisplayName.Trim(),
                SerialNumber = row.SerialNumber,
                OwnershipOrganizationId = facility.OrganizationId,
                OperationalFacilityId = facilityId,
                CurrentStatus = row.CurrentStatus,
                Condition = row.Condition,
                Criticality = row.Criticality,
                SourceType = ResourceSourceType.Import,
                SourceReference = request.SourceReference,
                LastVerifiedAtUtc = timeProvider.GetUtcNow(),
                LastVerifiedBy = currentUser.DisplayName
            });
        }

        if (apply && valid > 0)
        {
            db.Add(new ResourceImportBatch
            {
                FacilityId = facilityId,
                SourceSystem = request.SourceSystem.Trim(),
                SourceReference = request.SourceReference.Trim(),
                FileHash = request.FileHash.Trim(),
                SubmittedByUserId = currentUser.UserId,
                SubmittedAtUtc = timeProvider.GetUtcNow(),
                Status = "Confirmed",
                TotalRows = request.Rows.Count,
                ValidRows = valid,
                RejectedRows = errors.Count,
                DuplicateRows = duplicate,
                ConfirmedAtUtc = timeProvider.GetUtcNow(),
                AppliedRows = valid
            });
            await db.SaveChangesAsync(cancellationToken);
            await AuditAsync("ResourceImportConfirmed", "ResourceImportBatch", facilityId, cancellationToken);
        }

        return new ResourceImportResult(request.Rows.Count, valid, errors.Count, duplicate, apply ? valid : 0, errors);
    }

    private static void ValidateAssetRequest(ResourceAssetCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AssetCode) || string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new ArgumentException("كود المورد والاسم مطلوبان.");
        }

        if (request.ManufactureYear is < 1950 or > 2100)
        {
            throw new ArgumentException("سنة التصنيع غير صحيحة.");
        }
    }

    private void Require(string permission)
    {
        if (!currentUser.HasPermission(permission))
        {
            throw new UnauthorizedAccessException("لا تملك الصلاحية المطلوبة.");
        }
    }

    private async Task AuditAsync(string action, string entityType, Guid entityId, CancellationToken cancellationToken)
    {
        await audit.WriteAsync(new AuditEntry
        {
            Action = action,
            Module = "Resources",
            EntityType = entityType,
            EntityId = entityId.ToString(),
            NewValues = new Dictionary<string, string>
            {
                ["Actor"] = currentUser.DisplayName ?? currentUser.ExternalSubject ?? "unknown"
            },
            IsSensitiveView = false
        },
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string ExceptionType(ResourceStatus status, DateTimeOffset? lastVerified, DateTimeOffset staleBefore)
    {
        if (lastVerified is null || lastVerified < staleBefore) return "ResourceDataStale";
        return status switch
        {
            ResourceStatus.OutOfService => "CriticalResourceUnavailable",
            ResourceStatus.AwaitingParts => "AwaitingPartsOverdue",
            ResourceStatus.UnderMaintenance => "MaintenanceOverdue",
            ResourceStatus.Unknown => "AssetLocationUnknown",
            _ => "ResourceDataStale"
        };
    }

    private static string ExceptionReason(ResourceStatus status, DateTimeOffset? lastVerified, DateTimeOffset staleBefore)
    {
        if (lastVerified is null || lastVerified < staleBefore) return "بيانات المورد تحتاج تحققًا حديثًا.";
        return status switch
        {
            ResourceStatus.OutOfService => "المورد خارج الخدمة ويؤثر على الجاهزية.",
            ResourceStatus.AwaitingParts => "المورد بانتظار قطع.",
            ResourceStatus.UnderMaintenance => "المورد تحت الصيانة.",
            ResourceStatus.Unknown => "حالة المورد غير معروفة.",
            _ => "يتطلب متابعة."
        };
    }

    private sealed record ResourceStatusCounts(
        int Total,
        int Available,
        int Standby,
        int InUse,
        int Reserved,
        int UnderMaintenance,
        int OutOfService,
        int AwaitingParts,
        int Unknown,
        int Retired,
        int Transferred,
        int MissionCriticalUnavailable,
        int StaleRecords,
        int MissingData)
    {
        public static ResourceStatusCounts Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    private sealed record ResourceCategoryCounts(
        ResourceType Type,
        int Total,
        int Available,
        int Standby,
        int InUse,
        int Reserved,
        int UnderMaintenance,
        int OutOfService,
        int AwaitingParts,
        int Unknown,
        int Retired,
        int Transferred,
        int StaleRecords)
    {
        public static ResourceCategoryCounts Empty(ResourceType type) => new(type, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    private sealed record AssetProjection(
        Guid Id,
        ResourceType ResourceType,
        string AssetCode,
        string DisplayName,
        string? SerialNumber,
        string? PlateNumber,
        ResourceStatus CurrentStatus,
        ResourceCondition Condition,
        ResourceCriticality Criticality,
        string? UnitNameAr,
        string? CustodianNameAr,
        DateTimeOffset? LastVerifiedAtUtc);

    private sealed record FacilityScopeInfo(Guid FacilityId, Guid OrganizationId, Guid RegionId);
}
