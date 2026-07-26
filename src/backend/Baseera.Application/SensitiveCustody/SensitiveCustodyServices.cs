namespace Baseera.Application.SensitiveCustody;

using Baseera.Application.Abstractions;
using Baseera.Domain.Identity;
using Baseera.Domain.SensitiveCustody;
using Baseera.Domain.Workforce;
using Microsoft.EntityFrameworkCore;

public interface ISensitiveCustodyReadinessService
{
    Task<SensitiveCustodyWorkspacePayload> GetWorkspacePayloadAsync(Guid facilityId, CancellationToken cancellationToken);
    Task<SensitiveCustodySummaryDto> GetSummaryAsync(Guid facilityId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SensitiveCustodyTimelineItemDto>> GetTimelineAsync(Guid facilityId, int limit, CancellationToken cancellationToken);
}

public interface IWeaponAssetQueryService
{
    Task<IReadOnlyList<WeaponAssetListItemDto>> ListWeaponsAsync(Guid facilityId, string? search, int pageSize, CancellationToken cancellationToken);
    Task<WeaponAssetDetailDto?> GetWeaponAsync(Guid facilityId, Guid weaponId, CancellationToken cancellationToken);
}

public interface IWeaponAssetCommandService
{
    Task<Guid> CreateWeaponAsync(Guid facilityId, WeaponAssetCreateRequest request, CancellationToken cancellationToken);
    Task UpdateWeaponAsync(Guid facilityId, Guid weaponId, WeaponAssetUpdateRequest request, CancellationToken cancellationToken);
}

public interface ICustodyTransactionService
{
    Task<IReadOnlyList<CustodyTransactionDto>> ListTransactionsAsync(Guid facilityId, int page, int pageSize, CancellationToken cancellationToken);
    Task<Guid> CreateTransactionAsync(Guid facilityId, CustodyTransactionCreateRequest request, CancellationToken cancellationToken);
    Task ApproveAsync(Guid facilityId, Guid transactionId, SensitiveCustodyTransitionRequest request, CancellationToken cancellationToken);
    Task HandoverAsync(Guid facilityId, Guid transactionId, SensitiveCustodyTransitionRequest request, CancellationToken cancellationToken);
    Task ReceiveAsync(Guid facilityId, Guid transactionId, SensitiveCustodyTransitionRequest request, CancellationToken cancellationToken);
    Task ReverseAsync(Guid facilityId, Guid transactionId, SensitiveCustodyTransitionRequest request, CancellationToken cancellationToken);
}

public interface IAmmunitionLedgerService
{
    Task<IReadOnlyList<AmmunitionLotDto>> ListLotsAsync(Guid facilityId, int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<AmmunitionTransactionDto>> ListLedgerAsync(Guid facilityId, int page, int pageSize, CancellationToken cancellationToken);
    Task<Guid> RecordTransactionAsync(Guid facilityId, AmmunitionTransactionRequest request, CancellationToken cancellationToken);
}

public interface IInventorySessionService
{
    Task<IReadOnlyList<InventorySessionDto>> ListInventoriesAsync(Guid facilityId, int page, int pageSize, CancellationToken cancellationToken);
    Task<Guid> StartInventoryAsync(Guid facilityId, InventorySessionCreateRequest request, CancellationToken cancellationToken);
    Task<Guid> AddEntryAsync(Guid facilityId, Guid inventoryId, InventoryEntryRequest request, CancellationToken cancellationToken);
    Task CompleteAsync(Guid facilityId, Guid inventoryId, SensitiveCustodyTransitionRequest request, CancellationToken cancellationToken);
    Task ApproveInventoryAsync(Guid facilityId, Guid inventoryId, SensitiveCustodyTransitionRequest request, CancellationToken cancellationToken);
}

public interface IWeaponInspectionService
{
    Task<IReadOnlyList<WeaponInspectionDto>> ListInspectionsAsync(Guid facilityId, int page, int pageSize, CancellationToken cancellationToken);
    Task<Guid> RecordInspectionAsync(Guid facilityId, WeaponInspectionRequest request, CancellationToken cancellationToken);
}

public interface ISensitiveCustodyDataQualityService
{
    Task<IReadOnlyList<SensitiveCustodyDataQualityIssueDto>> GetDataQualityAsync(Guid facilityId, CancellationToken cancellationToken);
}

public interface ISensitiveCustodyImportService
{
    Task<SensitiveCustodyImportResult> PreviewAsync(Guid facilityId, SensitiveCustodyImportPreviewRequest request, CancellationToken cancellationToken);
    Task<SensitiveCustodyImportResult> ConfirmAsync(Guid facilityId, SensitiveCustodyImportPreviewRequest request, CancellationToken cancellationToken);
}

public interface ISensitiveCustodyReconciliationService
{
    Task<IReadOnlyList<SensitiveCustodyInterventionDto>> ListReconciliationAsync(Guid facilityId, CancellationToken cancellationToken);
    Task<int> ReconcileAsync(Guid facilityId, CancellationToken cancellationToken);
}

public sealed class SensitiveCustodyOptions
{
    public int StaleVerificationDays { get; init; } = 30;
    public int DefaultPageSize { get; init; } = 50;
    public int MaxPageSize { get; init; } = 100;
    public int WorkspaceLimit { get; init; } = 20;
}

public sealed class SensitiveCustodyService(
    IBaseeraDbContext db,
    IOrganizationalScopeService scope,
    ICurrentUser currentUser,
    IAuditService audit,
    TimeProvider timeProvider)
    : ISensitiveCustodyReadinessService,
      IWeaponAssetQueryService,
      IWeaponAssetCommandService,
      ICustodyTransactionService,
      IAmmunitionLedgerService,
      IInventorySessionService,
      IWeaponInspectionService,
      ISensitiveCustodyDataQualityService,
      ISensitiveCustodyImportService,
      ISensitiveCustodyReconciliationService
{
    private const string MissingPermissionMessage = "لا تملك الصلاحية المطلوبة.";
    private const string FacilityNotFoundMessage = "السجن غير موجود.";
    private const string SensitiveModule = "SensitiveCustody";
    private readonly SensitiveCustodyOptions options = new();

    public async Task<SensitiveCustodyWorkspacePayload> GetWorkspacePayloadAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.SensitiveCustodyViewSummary);
        var summary = await GetSummaryAsync(facilityId, cancellationToken);
        var interventions = await ListReconciliationAsync(facilityId, cancellationToken);
        var dataQuality = await GetDataQualityAsync(facilityId, cancellationToken);
        var timeline = currentUser.HasPermission(PermissionCodes.SensitiveCustodyViewCustodyTransactions)
            ? await GetTimelineAsync(facilityId, options.WorkspaceLimit, cancellationToken)
            : [];

        return new SensitiveCustodyWorkspacePayload
        {
            Summary = summary,
            Interventions = interventions.Take(options.WorkspaceLimit).ToList(),
            DataQuality = dataQuality,
            Timeline = timeline,
            AllowedActions = BuildAllowedActions()
        };
    }

    public async Task<SensitiveCustodySummaryDto> GetSummaryAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.SensitiveCustodyViewSummary);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var staleBefore = now.AddDays(-options.StaleVerificationDays);

        var weaponCounts = await WeaponsInFacility(facilityId)
            .GroupBy(_ => 1)
            .Select(group => new SensitiveWeaponCounts(
                group.Count(),
                group.Count(w => w.CurrentStatus == WeaponStatus.InArmory),
                group.Count(w => w.CurrentStatus == WeaponStatus.IssuedToMember),
                group.Count(w => w.CurrentStatus == WeaponStatus.IssuedToUnit),
                group.Count(w => w.CurrentStatus == WeaponStatus.UnderMaintenance || w.CurrentStatus == WeaponStatus.AwaitingParts),
                group.Count(w => w.CurrentStatus == WeaponStatus.OutOfService || w.Condition == WeaponCondition.Unserviceable),
                group.Count(w => w.CurrentStatus == WeaponStatus.Missing || w.CurrentStatus == WeaponStatus.UnderInvestigation),
                group.Count(w => w.NextInspectionDueAtUtc != null && w.NextInspectionDueAtUtc <= now),
                group.Count(w => w.LastVerifiedAtUtc == null || w.LastVerifiedAtUtc < staleBefore),
                group.Count(w => w.LastVerifiedAtUtc != null),
                group.Count(w => (w.CurrentStatus == WeaponStatus.InArmory || w.CurrentStatus == WeaponStatus.IssuedToMember || w.CurrentStatus == WeaponStatus.IssuedToUnit)
                    && (w.Condition == WeaponCondition.Serviceable || w.Condition == WeaponCondition.ServiceableWithRestrictions))))
            .FirstOrDefaultAsync(cancellationToken) ?? SensitiveWeaponCounts.Empty;

        var pendingApprovals = await db.CustodyTransactions
            .AsNoTracking()
            .CountAsync(t => !t.IsDeleted
                && t.FacilityId == facilityId
                && t.Status == CustodyTransactionStatus.PendingApproval,
                cancellationToken);

        var overdueReturns = await db.CustodyTransactions
            .AsNoTracking()
            .CountAsync(t => !t.IsDeleted
                && t.FacilityId == facilityId
                && t.ExpectedReturnAtUtc != null
                && t.ExpectedReturnAtUtc < now
                && t.ReturnedAtUtc == null
                && t.Status != CustodyTransactionStatus.Completed
                && t.Status != CustodyTransactionStatus.Reversed
                && t.Status != CustodyTransactionStatus.Cancelled,
                cancellationToken);

        var openDiscrepancies = await db.InventoryEntries
            .AsNoTracking()
            .CountAsync(e => !e.IsDeleted
                && e.InventorySession.FacilityId == facilityId
                && e.DiscrepancyType != InventoryDiscrepancyType.None
                && e.ResolvedAtUtc == null,
                cancellationToken);

        var ammunition = await db.AmmunitionLots
            .AsNoTracking()
            .Where(lot => !lot.IsDeleted && lot.FacilityId == facilityId)
            .Select(lot => new
            {
                Available = lot.CurrentQuantity - lot.ReservedQuantity - lot.QuarantinedQuantity - lot.DamagedQuantity
            })
            .ToListAsync(cancellationToken);
        var availableAmmunition = ammunition.Sum(a => Math.Max(0, a.Available));

        var minimumAmmunition = await ActiveRequirements(facilityId, now)
            .Where(r => r.AmmunitionTypeId != null)
            .SumAsync(r => (int?)r.MinimumOperationalQuantity, cancellationToken) ?? 0;

        var lastInventory = await db.InventorySessions
            .AsNoTracking()
            .Where(s => !s.IsDeleted && s.FacilityId == facilityId && s.Status == InventoryStatus.Approved)
            .MaxAsync(s => (DateTimeOffset?)s.CompletedAtUtc, cancellationToken);

        return new SensitiveCustodySummaryDto
        {
            FacilityId = facilityId,
            TotalWeapons = weaponCounts.Total,
            OperationallyReady = weaponCounts.Operational,
            Issued = weaponCounts.IssuedToMembers + weaponCounts.IssuedToUnits,
            InArmory = weaponCounts.InArmory,
            WithUnits = weaponCounts.IssuedToUnits,
            WithMembers = weaponCounts.IssuedToMembers,
            UnderMaintenance = weaponCounts.UnderMaintenance,
            OutOfService = weaponCounts.OutOfService,
            MissingOrDiscrepant = weaponCounts.MissingOrInvestigating + openDiscrepancies,
            OverdueReturns = overdueReturns,
            DueInspections = weaponCounts.DueInspections,
            OpenDiscrepancies = openDiscrepancies,
            PendingApprovals = pendingApprovals,
            AvailableAmmunition = availableAmmunition,
            MinimumAmmunition = minimumAmmunition,
            AmmunitionGap = Math.Max(0, minimumAmmunition - availableAmmunition),
            StaleRecords = weaponCounts.Stale,
            ReadinessRate = SensitiveCustodyReadinessPolicy.Rate(weaponCounts.Operational, weaponCounts.Total),
            VerificationCoverage = SensitiveCustodyReadinessPolicy.Rate(weaponCounts.Verified, weaponCounts.Total),
            FreshnessStatus = weaponCounts.Stale == 0 ? "Fresh" : "Stale",
            ConfidenceLevel = openDiscrepancies == 0 && weaponCounts.Stale == 0 ? "High" : "Medium",
            LastInventoryAtUtc = lastInventory,
            GeneratedAtUtc = now
        };
    }

    public async Task<IReadOnlyList<WeaponAssetListItemDto>> ListWeaponsAsync(
        Guid facilityId,
        string? search,
        int pageSize,
        CancellationToken cancellationToken)
    {
        Require(PermissionCodes.SensitiveCustodyViewWeapons);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var boundedPageSize = Math.Clamp(pageSize, 1, options.MaxPageSize);
        var query = WeaponsInFacility(facilityId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(w => w.InternalAssetCode.Contains(term));
        }

        var canViewArmory = currentUser.HasPermission(PermissionCodes.SensitiveCustodyViewArmoryLocations);
        var canViewSerial = currentUser.HasPermission(PermissionCodes.SensitiveCustodyViewSerialNumbers);
        var rows = await query
            .OrderBy(w => w.InternalAssetCode)
            .Take(boundedPageSize)
            .Select(w => new WeaponProjectionRow(
                w.Id,
                w.InternalAssetCode,
                w.SerialNumberEncrypted,
                w.SerialNumberHash,
                w.WeaponType.NameAr,
                w.Caliber,
                w.CurrentStatus,
                w.Condition,
                w.Criticality,
                w.CurrentCustodyLocationType,
                w.CurrentFacilityUnit == null ? null : w.CurrentFacilityUnit.NameAr,
                w.CurrentArmoryLocation == null ? null : w.CurrentArmoryLocation.Name,
                w.LastInspectionAtUtc,
                w.NextInspectionDueAtUtc,
                w.LastVerifiedAtUtc))
            .ToListAsync(cancellationToken);
        return rows.Select(row => ToWeaponListItem(row, canViewSerial, canViewArmory)).ToList();
    }

    public async Task<WeaponAssetDetailDto?> GetWeaponAsync(Guid facilityId, Guid weaponId, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.SensitiveCustodyViewWeapons);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var canViewArmory = currentUser.HasPermission(PermissionCodes.SensitiveCustodyViewArmoryLocations);
        var canViewSerial = currentUser.HasPermission(PermissionCodes.SensitiveCustodyViewSerialNumbers);
        var weaponRow = await WeaponsInFacility(facilityId)
            .Where(w => w.Id == weaponId)
            .Select(w => new WeaponProjectionRow(
                w.Id,
                w.InternalAssetCode,
                w.SerialNumberEncrypted,
                w.SerialNumberHash,
                w.WeaponType.NameAr,
                w.Caliber,
                w.CurrentStatus,
                w.Condition,
                w.Criticality,
                w.CurrentCustodyLocationType,
                w.CurrentFacilityUnit == null ? null : w.CurrentFacilityUnit.NameAr,
                w.CurrentArmoryLocation == null ? null : w.CurrentArmoryLocation.Name,
                w.LastInspectionAtUtc,
                w.NextInspectionDueAtUtc,
                w.LastVerifiedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
        if (weaponRow is null)
        {
            return null;
        }

        var transactionRows = await db.CustodyTransactions
            .AsNoTracking()
            .Where(t => !t.IsDeleted && t.FacilityId == facilityId && t.WeaponAssetId == weaponId)
            .OrderByDescending(t => t.IssuedAtUtc)
            .Take(20)
            .Select(t => new CustodyTransactionProjectionRow(
                t.Id,
                t.WeaponAssetId,
                t.TransactionType,
                t.Status,
                t.IssuedAtUtc,
                t.ExpectedReturnAtUtc,
                t.ReturnedAtUtc,
                t.PurposeCode,
                t.Reason,
                t.CreatedBy,
                t.ApprovedBy,
                t.ReceivedBy,
                t.RowVersion))
            .ToListAsync(cancellationToken);
        var inspections = await db.WeaponInspections
            .AsNoTracking()
            .Where(i => !i.IsDeleted && i.FacilityId == facilityId && i.WeaponAssetId == weaponId)
            .OrderByDescending(i => i.InspectedAtUtc)
            .Take(20)
            .Select(i => new WeaponInspectionDto
            {
                Id = i.Id,
                WeaponAssetId = i.WeaponAssetId,
                InspectionType = i.InspectionType,
                Result = i.Result,
                Condition = i.Condition,
                Restrictions = i.Restrictions,
                InspectedAtUtc = i.InspectedAtUtc,
                NextDueAtUtc = i.NextDueAtUtc
            })
            .ToListAsync(cancellationToken);

        return new WeaponAssetDetailDto
        {
            Weapon = ToWeaponListItem(weaponRow, canViewSerial, canViewArmory),
            RecentTransactions = transactionRows.Select(ToTransactionDto).ToList(),
            Inspections = inspections,
            AllowedActions = BuildAllowedActions()
        };
    }

    public async Task<Guid> CreateWeaponAsync(Guid facilityId, WeaponAssetCreateRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.SensitiveCustodyManageWeapons);
        var facility = await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        await EnsureWeaponTypeAsync(request.WeaponTypeId, facility.Region.OrganizationId, cancellationToken);
        await EnsureUnitInFacilityAsync(facilityId, request.CurrentFacilityUnitId, cancellationToken);
        await EnsureArmoryInFacilityAsync(facilityId, request.CurrentArmoryLocationId, cancellationToken);

        var serialHash = SensitiveSerialProtection.Hash(request.SerialNumber);
        var exists = await db.WeaponAssets
            .AnyAsync(w => !w.IsDeleted && w.OrganizationId == facility.Region.OrganizationId && w.SerialNumberHash == serialHash, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("يوجد سلاح بنفس رقم السجل المحمي.");
        }

        var weapon = new WeaponAsset
        {
            OrganizationId = facility.Region.OrganizationId,
            WeaponTypeId = request.WeaponTypeId,
            InternalAssetCode = request.InternalAssetCode.Trim(),
            SerialNumberEncrypted = SensitiveSerialProtection.ProtectForStorage(request.SerialNumber),
            SerialNumberHash = serialHash,
            Manufacturer = request.Manufacturer,
            Model = request.Model,
            Caliber = request.Caliber.Trim(),
            CurrentFacilityId = facilityId,
            CurrentFacilityUnitId = request.CurrentFacilityUnitId,
            CurrentArmoryLocationId = request.CurrentArmoryLocationId,
            CurrentCustodyLocationType = request.CurrentArmoryLocationId is null ? CustodyLocationType.Unknown : CustodyLocationType.Armory,
            CurrentStatus = request.CurrentStatus,
            Condition = request.Condition,
            Criticality = request.Criticality,
            LastVerifiedAtUtc = timeProvider.GetUtcNow(),
            SourceReference = request.SourceReference
        };

        db.Add(weapon);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("WeaponRegistered", "WeaponAsset", weapon.Id, cancellationToken);
        return weapon.Id;
    }

    public async Task UpdateWeaponAsync(Guid facilityId, Guid weaponId, WeaponAssetUpdateRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.SensitiveCustodyManageWeapons);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var weapon = await db.WeaponAssets.FirstOrDefaultAsync(w => !w.IsDeleted && w.Id == weaponId && w.CurrentFacilityId == facilityId, cancellationToken)
            ?? throw new KeyNotFoundException("السلاح غير موجود.");
        EnsureCurrentRowVersion(weapon, request.RowVersion);
        if (request.Condition.HasValue)
        {
            weapon.Condition = request.Condition.Value;
        }

        weapon.LastVerifiedAtUtc = request.LastVerifiedAtUtc ?? timeProvider.GetUtcNow();
        weapon.SourceReference = request.SourceReference ?? weapon.SourceReference;
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("WeaponUpdated", "WeaponAsset", weapon.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<CustodyTransactionDto>> ListTransactionsAsync(Guid facilityId, int page, int pageSize, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.SensitiveCustodyViewCustodyTransactions);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var take = Math.Clamp(pageSize, 1, options.MaxPageSize);
        var skip = Math.Max(0, page - 1) * take;
        var rows = await db.CustodyTransactions
            .AsNoTracking()
            .Where(t => !t.IsDeleted && t.FacilityId == facilityId)
            .OrderByDescending(t => t.IssuedAtUtc)
            .Skip(skip)
            .Take(take)
            .Select(t => new CustodyTransactionProjectionRow(
                t.Id,
                t.WeaponAssetId,
                t.TransactionType,
                t.Status,
                t.IssuedAtUtc,
                t.ExpectedReturnAtUtc,
                t.ReturnedAtUtc,
                t.PurposeCode,
                t.Reason,
                t.CreatedBy,
                t.ApprovedBy,
                t.ReceivedBy,
                t.RowVersion))
            .ToListAsync(cancellationToken);
        return rows.Select(ToTransactionDto).ToList();
    }

    public async Task<Guid> CreateTransactionAsync(Guid facilityId, CustodyTransactionCreateRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.SensitiveCustodyIssueWeapons);
        var facility = await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var weapon = await LoadWeaponForCommandAsync(facilityId, request.WeaponAssetId, cancellationToken);
        if (SensitiveCustodyReadinessPolicy.IsFinal(weapon.CurrentStatus))
        {
            throw new InvalidOperationException("لا يمكن إنشاء عهدة لسلاح في حالة نهائية.");
        }

        await EnsureDestinationEligibleAsync(facilityId, request, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var requiresApproval = SensitiveCustodyTransactionPolicy.RequiresApproval(request.TransactionType);
        var transaction = new CustodyTransaction
        {
            OrganizationId = facility.Region.OrganizationId,
            FacilityId = facilityId,
            WeaponAssetId = weapon.Id,
            TransactionType = request.TransactionType,
            FromCustodyType = weapon.CurrentCustodyLocationType,
            FromCustodyReferenceId = weapon.CurrentCustodyTransactionId,
            ToCustodyType = request.ToCustodyType,
            ToCustodyReferenceId = request.ToCustodyReferenceId,
            IssuedAtUtc = now,
            ExpectedReturnAtUtc = request.ExpectedReturnAtUtc,
            PurposeCode = request.PurposeCode.Trim(),
            Reason = request.Reason.Trim(),
            CreatedBy = ActorReference(),
            Status = requiresApproval ? CustodyTransactionStatus.PendingApproval : CustodyTransactionStatus.Approved
        };

        db.Add(transaction);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("CustodyTransactionCreated", "CustodyTransaction", transaction.Id, cancellationToken);
        return transaction.Id;
    }

    public Task ApproveAsync(Guid facilityId, Guid transactionId, SensitiveCustodyTransitionRequest request, CancellationToken cancellationToken) =>
        TransitionAsync(facilityId, transactionId, CustodyTransactionStatus.Approved, PermissionCodes.SensitiveCustodyApproveTransactions, "CustodyTransactionApproved", request, cancellationToken);

    public Task HandoverAsync(Guid facilityId, Guid transactionId, SensitiveCustodyTransitionRequest request, CancellationToken cancellationToken) =>
        TransitionAsync(facilityId, transactionId, CustodyTransactionStatus.HandedOver, PermissionCodes.SensitiveCustodyIssueWeapons, "CustodyHandedOver", request, cancellationToken);

    public Task ReceiveAsync(Guid facilityId, Guid transactionId, SensitiveCustodyTransitionRequest request, CancellationToken cancellationToken) =>
        TransitionAsync(facilityId, transactionId, CustodyTransactionStatus.Received, PermissionCodes.SensitiveCustodyReceiveWeapons, "CustodyReceived", request, cancellationToken);

    public Task ReverseAsync(Guid facilityId, Guid transactionId, SensitiveCustodyTransitionRequest request, CancellationToken cancellationToken) =>
        TransitionAsync(facilityId, transactionId, CustodyTransactionStatus.Reversed, PermissionCodes.SensitiveCustodyApproveTransactions, "CustodyReversed", request, cancellationToken);

    public async Task<IReadOnlyList<AmmunitionLotDto>> ListLotsAsync(Guid facilityId, int page, int pageSize, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.SensitiveCustodyViewAmmunition);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var take = Math.Clamp(pageSize, 1, options.MaxPageSize);
        var skip = Math.Max(0, page - 1) * take;
        return await db.AmmunitionLots
            .AsNoTracking()
            .Where(l => !l.IsDeleted && l.FacilityId == facilityId)
            .OrderBy(l => l.AmmunitionType.Code)
            .Skip(skip)
            .Take(take)
            .Select(l => new AmmunitionLotDto
            {
                Id = l.Id,
                TypeNameAr = l.AmmunitionType.NameAr,
                Caliber = l.AmmunitionType.Caliber,
                MaskedLotNumber = SensitiveSerialProtection.Mask(l.LotNumberHash),
                ExpiryDateUtc = l.ExpiryDateUtc,
                CurrentQuantity = l.CurrentQuantity,
                ReservedQuantity = l.ReservedQuantity,
                QuarantinedQuantity = l.QuarantinedQuantity,
                DamagedQuantity = l.DamagedQuantity,
                AvailableQuantity = l.CurrentQuantity - l.ReservedQuantity - l.QuarantinedQuantity - l.DamagedQuantity,
                UnitOfMeasure = l.UnitOfMeasure
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AmmunitionTransactionDto>> ListLedgerAsync(Guid facilityId, int page, int pageSize, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.SensitiveCustodyViewAmmunition);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var take = Math.Clamp(pageSize, 1, options.MaxPageSize);
        var skip = Math.Max(0, page - 1) * take;
        return await db.AmmunitionTransactions
            .AsNoTracking()
            .Where(t => !t.IsDeleted && t.FacilityId == facilityId)
            .OrderByDescending(t => t.OccurredAtUtc)
            .Skip(skip)
            .Take(take)
            .Select(t => new AmmunitionTransactionDto
            {
                Id = t.Id,
                AmmunitionLotId = t.AmmunitionLotId,
                TransactionType = t.TransactionType,
                Quantity = t.Quantity,
                OccurredAtUtc = t.OccurredAtUtc,
                Reason = t.Reason,
                CreatedBy = t.CreatedBy
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid> RecordTransactionAsync(Guid facilityId, AmmunitionTransactionRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.SensitiveCustodyManageAmmunition);
        var facility = await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var lot = await db.AmmunitionLots.FirstOrDefaultAsync(l => !l.IsDeleted && l.Id == request.AmmunitionLotId && l.FacilityId == facilityId, cancellationToken)
            ?? throw new KeyNotFoundException("دفعة الذخيرة غير موجودة.");
        lot.CurrentQuantity = AmmunitionLedgerPolicy.Apply(lot.CurrentQuantity, request.TransactionType, request.Quantity);
        var transaction = new AmmunitionTransaction
        {
            OrganizationId = facility.Region.OrganizationId,
            FacilityId = facilityId,
            AmmunitionLotId = lot.Id,
            TransactionType = request.TransactionType,
            Quantity = request.Quantity,
            OccurredAtUtc = timeProvider.GetUtcNow(),
            Reason = request.Reason.Trim(),
            CreatedBy = ActorReference(),
            Reference = request.Reference
        };
        db.Add(transaction);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("AmmunitionTransactionRecorded", "AmmunitionTransaction", transaction.Id, cancellationToken);
        return transaction.Id;
    }

    public async Task<IReadOnlyList<InventorySessionDto>> ListInventoriesAsync(Guid facilityId, int page, int pageSize, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.SensitiveCustodyConductInventory);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var take = Math.Clamp(pageSize, 1, options.MaxPageSize);
        var skip = Math.Max(0, page - 1) * take;
        var rows = await db.InventorySessions
            .AsNoTracking()
            .Where(s => !s.IsDeleted && s.FacilityId == facilityId)
            .OrderByDescending(s => s.StartedAtUtc)
            .Skip(skip)
            .Take(take)
            .Select(s => new InventorySessionProjectionRow(
                s.Id,
                s.InventoryType,
                s.Status,
                s.StartedAtUtc,
                s.CompletedAtUtc,
                s.ExpectedWeaponCount,
                s.CountedWeaponCount,
                s.ExpectedAmmunitionQuantity,
                s.CountedAmmunitionQuantity,
                s.DifferenceStatus,
                s.RowVersion))
            .ToListAsync(cancellationToken);
        return rows.Select(ToInventoryDto).ToList();
    }

    public async Task<Guid> StartInventoryAsync(Guid facilityId, InventorySessionCreateRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.SensitiveCustodyConductInventory);
        var facility = await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        await EnsureArmoryInFacilityAsync(facilityId, request.ArmoryLocationId, cancellationToken);
        var session = new InventorySession
        {
            OrganizationId = facility.Region.OrganizationId,
            FacilityId = facilityId,
            ArmoryLocationId = request.ArmoryLocationId,
            InventoryType = request.InventoryType,
            Status = InventoryStatus.InProgress,
            StartedAtUtc = timeProvider.GetUtcNow(),
            InitiatedBy = ActorReference(),
            ExpectedWeaponCount = await WeaponsInFacility(facilityId).CountAsync(cancellationToken),
            ExpectedAmmunitionQuantity = await db.AmmunitionLots.Where(l => !l.IsDeleted && l.FacilityId == facilityId).SumAsync(l => l.CurrentQuantity, cancellationToken),
            Notes = request.Notes
        };
        db.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("InventoryStarted", "InventorySession", session.Id, cancellationToken);
        return session.Id;
    }

    public async Task<Guid> AddEntryAsync(Guid facilityId, Guid inventoryId, InventoryEntryRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.SensitiveCustodyConductInventory);
        var session = await LoadInventoryAsync(facilityId, inventoryId, cancellationToken);
        if (session.Status != InventoryStatus.InProgress)
        {
            throw new InvalidOperationException("لا يمكن تعديل الجرد بعد إكماله.");
        }

        var entry = new InventoryEntry
        {
            InventorySessionId = session.Id,
            EntityType = request.EntityType,
            ExpectedReferenceId = request.ExpectedReferenceId,
            CountedStatus = request.CountedStatus,
            DiscrepancyType = request.DiscrepancyType,
            ExpectedQuantity = request.ExpectedQuantity,
            CountedQuantity = request.CountedQuantity,
            Notes = request.Notes,
            VerifiedBy = ActorReference(),
            VerifiedAtUtc = timeProvider.GetUtcNow()
        };
        db.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return entry.Id;
    }

    public async Task CompleteAsync(Guid facilityId, Guid inventoryId, SensitiveCustodyTransitionRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.SensitiveCustodyConductInventory);
        var session = await LoadInventoryAsync(facilityId, inventoryId, cancellationToken);
        EnsureCurrentRowVersion(session, request.RowVersion);
        session.Status = InventoryStatus.PendingApproval;
        session.CompletedAtUtc = timeProvider.GetUtcNow();
        session.CountedWeaponCount = await db.InventoryEntries.CountAsync(e => !e.IsDeleted && e.InventorySessionId == session.Id && e.EntityType == InventoryEntityType.WeaponAsset, cancellationToken);
        session.CountedAmmunitionQuantity = await db.InventoryEntries.Where(e => !e.IsDeleted && e.InventorySessionId == session.Id && e.EntityType == InventoryEntityType.AmmunitionLot).SumAsync(e => e.CountedQuantity ?? 0, cancellationToken);
        session.DifferenceStatus = await HasCriticalInventoryDifferenceAsync(session.Id, cancellationToken) ? InventoryDifferenceStatus.Critical : InventoryDifferenceStatus.None;
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("InventoryCompleted", "InventorySession", session.Id, cancellationToken);
    }

    public async Task ApproveInventoryAsync(Guid facilityId, Guid inventoryId, SensitiveCustodyTransitionRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.SensitiveCustodyApproveInventory);
        var session = await LoadInventoryAsync(facilityId, inventoryId, cancellationToken);
        EnsureCurrentRowVersion(session, request.RowVersion);
        EnforceFourEyes(session.InitiatedBy);
        session.Status = InventoryStatus.Approved;
        session.ApprovedBy = ActorReference();
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("InventoryApproved", "InventorySession", session.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<WeaponInspectionDto>> ListInspectionsAsync(Guid facilityId, int page, int pageSize, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.SensitiveCustodyManageInspections);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var take = Math.Clamp(pageSize, 1, options.MaxPageSize);
        var skip = Math.Max(0, page - 1) * take;
        return await db.WeaponInspections
            .AsNoTracking()
            .Where(i => !i.IsDeleted && i.FacilityId == facilityId)
            .OrderByDescending(i => i.InspectedAtUtc)
            .Skip(skip)
            .Take(take)
            .Select(i => new WeaponInspectionDto
            {
                Id = i.Id,
                WeaponAssetId = i.WeaponAssetId,
                InspectionType = i.InspectionType,
                Result = i.Result,
                Condition = i.Condition,
                Restrictions = i.Restrictions,
                InspectedAtUtc = i.InspectedAtUtc,
                NextDueAtUtc = i.NextDueAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid> RecordInspectionAsync(Guid facilityId, WeaponInspectionRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.SensitiveCustodyManageInspections);
        var facility = await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var weapon = await LoadWeaponForCommandAsync(facilityId, request.WeaponAssetId, cancellationToken);
        await EnsureInspectorEligibleAsync(facilityId, request.InspectorWorkforceMemberId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var inspection = new WeaponInspection
        {
            OrganizationId = facility.Region.OrganizationId,
            FacilityId = facilityId,
            WeaponAssetId = weapon.Id,
            InspectionType = request.InspectionType,
            Result = request.Result,
            Condition = request.Condition,
            Restrictions = request.Restrictions,
            InspectorWorkforceMemberId = request.InspectorWorkforceMemberId,
            InspectedAtUtc = now,
            NextDueAtUtc = request.NextDueAtUtc,
            StatusTransition = request.Result is WeaponInspectionResult.FailedMaintenanceRequired ? "UnderMaintenance" : "InspectionRecorded"
        };
        weapon.Condition = request.Condition;
        weapon.LastInspectionAtUtc = now;
        weapon.NextInspectionDueAtUtc = request.NextDueAtUtc;
        if (request.Result is WeaponInspectionResult.FailedQuarantine)
        {
            weapon.CurrentStatus = WeaponStatus.Quarantined;
        }
        else if (request.Result is WeaponInspectionResult.FailedMaintenanceRequired)
        {
            weapon.CurrentStatus = WeaponStatus.UnderMaintenance;
        }

        db.Add(inspection);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("WeaponInspectionRecorded", "WeaponInspection", inspection.Id, cancellationToken);
        return inspection.Id;
    }

    public async Task<IReadOnlyList<SensitiveCustodyDataQualityIssueDto>> GetDataQualityAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.SensitiveCustodyViewSummary);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var staleBefore = now.AddDays(-options.StaleVerificationDays);
        var issues = new List<SensitiveCustodyDataQualityIssueDto>
        {
            await IssueAsync(SensitiveCustodyOperationalCatalog.DataQuality.MissingAssetCode, "High", "لا يمكن تتبع عهدة بلا كود داخلي.", "WeaponAssets", "تحديث كود الأصل", "weapons",
                WeaponsInFacility(facilityId).CountAsync(w => w.InternalAssetCode == "", cancellationToken)),
            await IssueAsync(SensitiveCustodyOperationalCatalog.DataQuality.MissingEncryptedSerial, "Critical", "السجل الحساس غير محمي.", "WeaponAssets", "إعادة إدخال السجل المشفر", "weapons",
                WeaponsInFacility(facilityId).CountAsync(w => w.SerialNumberEncrypted == "" || w.SerialNumberHash == "", cancellationToken)),
            await IssueAsync(SensitiveCustodyOperationalCatalog.DataQuality.UnknownStatus, "High", "الحالة المجهولة لا تدخل في الجاهزية.", "WeaponAssets", "تحديث الحالة", "weapons",
                WeaponsInFacility(facilityId).CountAsync(w => w.CurrentStatus == WeaponStatus.Unknown, cancellationToken)),
            await IssueAsync(SensitiveCustodyOperationalCatalog.DataQuality.UnknownCondition, "High", "الصلاحية التشغيلية غير قابلة للاعتماد.", "WeaponAssets", "تسجيل فحص", "inspections",
                WeaponsInFacility(facilityId).CountAsync(w => w.Condition == WeaponCondition.Unknown, cancellationToken)),
            await IssueAsync(SensitiveCustodyOperationalCatalog.DataQuality.StaleVerification, "Medium", "آخر تحقق قديم.", "WeaponAssets", "تنفيذ تحقق أو جرد", "inventories",
                WeaponsInFacility(facilityId).CountAsync(w => w.LastVerifiedAtUtc == null || w.LastVerifiedAtUtc < staleBefore, cancellationToken)),
            await IssueAsync(SensitiveCustodyOperationalCatalog.DataQuality.ExpiredInspection, "High", "فحص السلاح مستحق أو منته.", "WeaponInspections", "تسجيل فحص", "inspections",
                WeaponsInFacility(facilityId).CountAsync(w => w.NextInspectionDueAtUtc != null && w.NextInspectionDueAtUtc < now, cancellationToken)),
            await IssueAsync(SensitiveCustodyOperationalCatalog.DataQuality.OpenTransactionWithoutCompletion, "High", "توجد عهدة غير مكتملة.", "CustodyTransactions", "إكمال أو عكس العهدة", "transactions",
                db.CustodyTransactions.CountAsync(t => !t.IsDeleted && t.FacilityId == facilityId && !new[] { CustodyTransactionStatus.Completed, CustodyTransactionStatus.Reversed, CustodyTransactionStatus.Cancelled, CustodyTransactionStatus.Rejected }.Contains(t.Status), cancellationToken)),
            await IssueAsync(SensitiveCustodyOperationalCatalog.DataQuality.OverdueReturn, "Critical", "توجد عهد متأخرة في الإعادة.", "CustodyTransactions", "تسجيل الإعادة أو التصعيد", "transactions",
                db.CustodyTransactions.CountAsync(t => !t.IsDeleted && t.FacilityId == facilityId && t.ExpectedReturnAtUtc != null && t.ExpectedReturnAtUtc < now && t.ReturnedAtUtc == null, cancellationToken)),
            await IssueAsync(SensitiveCustodyOperationalCatalog.DataQuality.InventoryDiscrepancyUnresolved, "Critical", "فروقات الجرد تحتاج معالجة.", "InventoryEntries", "فتح معالجة الفرق", "inventories",
                db.InventoryEntries.CountAsync(e => !e.IsDeleted && e.InventorySession.FacilityId == facilityId && e.DiscrepancyType != InventoryDiscrepancyType.None && e.ResolvedAtUtc == null, cancellationToken)),
            await IssueAsync(SensitiveCustodyOperationalCatalog.DataQuality.AmmunitionNegativeProjection, "Critical", "رصيد ذخيرة سلبي غير مقبول.", "AmmunitionLots", "مراجعة ledger", "ammunition",
                db.AmmunitionLots.CountAsync(l => !l.IsDeleted && l.FacilityId == facilityId && l.CurrentQuantity < 0, cancellationToken)),
            await IssueAsync(SensitiveCustodyOperationalCatalog.DataQuality.ExpiredAmmunitionStillAvailable, "High", "ذخيرة منتهية لا يجب أن تكون متاحة.", "AmmunitionLots", "حجر أو إتلاف الدفعة", "ammunition",
                db.AmmunitionLots.CountAsync(l => !l.IsDeleted && l.FacilityId == facilityId && l.ExpiryDateUtc != null && l.ExpiryDateUtc < now && l.CurrentQuantity > 0, cancellationToken)),
            await IssueAsync(SensitiveCustodyOperationalCatalog.DataQuality.MissingRequirementBaseline, "Medium", "لا توجد حدود دنيا موثقة.", "SensitiveResourceRequirements", "تسجيل متطلبات معتمدة", "requirements",
                MissingRequirementCountAsync(facilityId, now, cancellationToken))
        };

        return issues.Where(i => i.Count > 0).ToList();
    }

    public async Task<SensitiveCustodyImportResult> PreviewAsync(Guid facilityId, SensitiveCustodyImportPreviewRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.SensitiveCustodyImport);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var result = ValidateImport(request);
        await AuditAsync("SensitiveImportPreviewed", "Facility", facilityId, cancellationToken);
        return result;
    }

    public async Task<SensitiveCustodyImportResult> ConfirmAsync(Guid facilityId, SensitiveCustodyImportPreviewRequest request, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.SensitiveCustodyImport);
        var facility = await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var validation = ValidateImport(request);
        if (validation.RejectedRows > 0)
        {
            return validation;
        }

        var batch = new SensitiveCustodyImportBatch
        {
            OrganizationId = facility.Region.OrganizationId,
            FacilityId = facilityId,
            ImportKind = request.ImportKind,
            SourceSystem = request.SourceSystem,
            SourceReference = request.SourceReference,
            FileHash = request.FileHash,
            Status = SensitiveCustodyImportStatuses.Confirmed,
            TotalRows = validation.TotalRows,
            ValidRows = validation.ValidRows,
            DuplicateRows = validation.DuplicateRows,
            AppliedRows = validation.ValidRows,
            ConfirmedAtUtc = timeProvider.GetUtcNow()
        };
        db.Add(batch);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("SensitiveImportConfirmed", "SensitiveCustodyImportBatch", batch.Id, cancellationToken);
        return validation with { AppliedRows = validation.ValidRows };
    }

    public async Task<IReadOnlyList<SensitiveCustodyInterventionDto>> ListReconciliationAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.SensitiveCustodyViewSummary);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var interventions = new List<SensitiveCustodyInterventionDto>();
        interventions.AddRange(await WeaponInterventionsAsync(facilityId, now, cancellationToken));
        interventions.AddRange(await AmmunitionInterventionsAsync(facilityId, now, cancellationToken));
        interventions.AddRange(await InventoryInterventionsAsync(facilityId, now, cancellationToken));
        return interventions
            .OrderByDescending(i => SeverityRank(i.Severity))
            .ThenBy(i => i.Code)
            .Take(options.WorkspaceLimit)
            .ToList();
    }

    public async Task<int> ReconcileAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.SensitiveCustodyReconcile);
        var items = await ListReconciliationAsync(facilityId, cancellationToken);
        await AuditAsync("ReconciliationCompleted", "Facility", facilityId, cancellationToken);
        return items.Count;
    }

    public async Task<IReadOnlyList<SensitiveCustodyTimelineItemDto>> GetTimelineAsync(Guid facilityId, int limit, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.SensitiveCustodyViewCustodyTransactions);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        return await db.CustodyTransactions
            .AsNoTracking()
            .Where(t => !t.IsDeleted && t.FacilityId == facilityId)
            .OrderByDescending(t => t.IssuedAtUtc)
            .Take(Math.Clamp(limit, 1, options.MaxPageSize))
            .Select(t => new SensitiveCustodyTimelineItemDto
            {
                EventType = t.TransactionType.ToString(),
                TitleAr = "حدث عهدة حساس",
                DescriptionAr = t.Status.ToString(),
                OccurredAtUtc = t.IssuedAtUtc,
                EntityReference = t.Id.ToString(),
                Tone = t.Status == CustodyTransactionStatus.Completed ? "success" : "attention"
            })
            .ToListAsync(cancellationToken);
    }

    private async Task TransitionAsync(
        Guid facilityId,
        Guid transactionId,
        CustodyTransactionStatus nextStatus,
        string permission,
        string auditAction,
        SensitiveCustodyTransitionRequest request,
        CancellationToken cancellationToken)
    {
        Require(permission);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var transaction = await db.CustodyTransactions.FirstOrDefaultAsync(t => !t.IsDeleted && t.Id == transactionId && t.FacilityId == facilityId, cancellationToken)
            ?? throw new KeyNotFoundException("عملية العهدة غير موجودة.");
        EnsureCurrentRowVersion(transaction, request.RowVersion);
        if (!SensitiveCustodyTransactionPolicy.CanTransition(transaction.Status, nextStatus))
        {
            throw new InvalidOperationException("انتقال حالة العهدة غير صالح.");
        }

        if (nextStatus == CustodyTransactionStatus.Approved)
        {
            EnforceFourEyes(transaction.CreatedBy);
            transaction.ApprovedBy = ActorReference();
        }
        else if (nextStatus == CustodyTransactionStatus.Received)
        {
            transaction.ReceivedBy = ActorReference();
        }

        transaction.Status = nextStatus;
        if (nextStatus is CustodyTransactionStatus.Completed or CustodyTransactionStatus.Reversed)
        {
            await ApplyCompletedTransactionAsync(transaction, nextStatus, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync(auditAction, "CustodyTransaction", transaction.Id, cancellationToken);
    }

    private async Task ApplyCompletedTransactionAsync(CustodyTransaction transaction, CustodyTransactionStatus nextStatus, CancellationToken cancellationToken)
    {
        var weapon = await LoadWeaponForCommandAsync(transaction.FacilityId, transaction.WeaponAssetId, cancellationToken);
        var previous = await db.CustodyTransactions
            .Where(t => !t.IsDeleted && t.WeaponAssetId == transaction.WeaponAssetId && t.IsCurrent)
            .ToListAsync(cancellationToken);
        foreach (var item in previous)
        {
            item.IsCurrent = false;
        }

        if (nextStatus == CustodyTransactionStatus.Completed)
        {
            transaction.IsCurrent = !SensitiveCustodyReadinessPolicy.IsFinal(SensitiveCustodyTransactionPolicy.CompletionStatus(transaction.TransactionType));
            transaction.ReturnedAtUtc ??= transaction.TransactionType == CustodyTransactionType.ReturnToArmory ? timeProvider.GetUtcNow() : null;
            weapon.CurrentStatus = SensitiveCustodyTransactionPolicy.CompletionStatus(transaction.TransactionType);
            weapon.CurrentCustodyLocationType = transaction.ToCustodyType;
            weapon.CurrentCustodyTransactionId = transaction.Id;
        }
    }

    private async Task EnsureDestinationEligibleAsync(Guid facilityId, CustodyTransactionCreateRequest request, CancellationToken cancellationToken)
    {
        if (request.ToCustodyType == CustodyLocationType.WorkforceMember && request.ToCustodyReferenceId is Guid memberId)
        {
            await EnsureEligibleCustodianAsync(facilityId, memberId, cancellationToken);
        }
        else if (request.ToCustodyType == CustodyLocationType.Armory && request.ToCustodyReferenceId is Guid armoryId)
        {
            await EnsureArmoryInFacilityAsync(facilityId, armoryId, cancellationToken);
        }
        else if (request.TransactionType is CustodyTransactionType.IssueToMember or CustodyTransactionType.IssueToUnit)
        {
            throw new InvalidOperationException("وجهة العهدة مطلوبة.");
        }
    }

    private async Task EnsureEligibleCustodianAsync(Guid facilityId, Guid memberId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var member = await db.WorkforceMembers
            .AsNoTracking()
            .Where(m => !m.IsDeleted && m.Id == memberId && m.CurrentOperationalFacilityId == facilityId)
            .Select(m => new { m.EmploymentStatus, m.IsOperational })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("المستلم غير مؤهل للعهدة.");
        if (!SensitiveCustodyEligibilityPolicy.IsEligibleMember(member.EmploymentStatus, member.IsOperational))
        {
            throw new InvalidOperationException("المستلم غير متاح تشغيليًا للعهدة.");
        }

        var unavailable = await db.WorkforceAvailabilityEvents
            .AsNoTracking()
            .AnyAsync(e => !e.IsDeleted
                && e.WorkforceMemberId == memberId
                && e.StartsAtUtc <= now
                && (e.EndsAtUtc == null || e.EndsAtUtc > now)
                && e.AffectsOperationalAvailability
                && (e.AvailabilityType == AvailabilityType.AnnualLeave
                    || e.AvailabilityType == AvailabilityType.SickLeave
                    || e.AvailabilityType == AvailabilityType.Training
                    || e.AvailabilityType == AvailabilityType.ExternalAssignment
                    || e.AvailabilityType == AvailabilityType.Suspended
                    || e.AvailabilityType == AvailabilityType.RestrictedDuty
                    || e.AvailabilityType == AvailabilityType.EmergencyLeave
                    || e.AvailabilityType == AvailabilityType.UnexcusedAbsence),
                cancellationToken);
        if (unavailable)
        {
            throw new InvalidOperationException("المستلم في حالة توفر تمنع حمل العهدة.");
        }

        var authorizedRoleIds = await db.WorkforceRoleDefinitions
            .AsNoTracking()
            .Where(role => role.Code == "AuthorizedCarrier")
            .Select(role => role.Id)
            .ToListAsync(cancellationToken);
        var qualified = await db.WorkforceQualifications
            .AsNoTracking()
            .AnyAsync(q => !q.IsDeleted
                && q.WorkforceMemberId == memberId
                && q.RoleDefinitionId != null
                && authorizedRoleIds.Contains(q.RoleDefinitionId.Value)
                && (q.Status == QualificationStatus.Valid || q.Status == QualificationStatus.ExpiringSoon)
                && (q.ExpiresAtUtc == null || q.ExpiresAtUtc > now),
                cancellationToken);
        if (!qualified)
        {
            throw new InvalidOperationException("المستلم لا يحمل تأهيل حمل سلاح فعال.");
        }
    }

    private Task EnsureInspectorEligibleAsync(Guid facilityId, Guid memberId, CancellationToken cancellationToken) =>
        EnsureEligibleCustodianAsync(facilityId, memberId, cancellationToken);

    private async Task<WeaponAsset> LoadWeaponForCommandAsync(Guid facilityId, Guid weaponId, CancellationToken cancellationToken) =>
        await db.WeaponAssets.FirstOrDefaultAsync(w => !w.IsDeleted && w.Id == weaponId && w.CurrentFacilityId == facilityId, cancellationToken)
        ?? throw new KeyNotFoundException("السلاح غير موجود.");

    private async Task<InventorySession> LoadInventoryAsync(Guid facilityId, Guid inventoryId, CancellationToken cancellationToken) =>
        await db.InventorySessions.FirstOrDefaultAsync(s => !s.IsDeleted && s.Id == inventoryId && s.FacilityId == facilityId, cancellationToken)
        ?? throw new KeyNotFoundException("جلسة الجرد غير موجودة.");

    private async Task<Baseera.Domain.Organization.Facility> EnsureFacilityVisibleAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        var facility = await db.Facilities
            .AsNoTracking()
            .Include(f => f.Region)
            .FirstOrDefaultAsync(f => f.Id == facilityId, cancellationToken)
            ?? throw new KeyNotFoundException(FacilityNotFoundMessage);
        if (!scope.CanAccessFacility(facilityId))
        {
            throw new KeyNotFoundException(FacilityNotFoundMessage);
        }

        return facility;
    }

    private async Task EnsureWeaponTypeAsync(Guid weaponTypeId, Guid organizationId, CancellationToken cancellationToken)
    {
        var exists = await db.WeaponTypeDefinitions
            .AnyAsync(t => !t.IsDeleted && t.Id == weaponTypeId && t.OrganizationId == organizationId && t.IsActive, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException("نوع السلاح غير صالح.");
        }
    }

    private async Task EnsureUnitInFacilityAsync(Guid facilityId, Guid? unitId, CancellationToken cancellationToken)
    {
        if (unitId is null)
        {
            return;
        }

        var exists = await db.FacilityUnits.AnyAsync(u => u.Id == unitId && u.FacilityId == facilityId, cancellationToken);
        if (!exists)
        {
            throw new KeyNotFoundException("الوحدة غير موجودة.");
        }
    }

    private async Task EnsureArmoryInFacilityAsync(Guid facilityId, Guid? armoryId, CancellationToken cancellationToken)
    {
        if (armoryId is null)
        {
            return;
        }

        var exists = await db.ArmoryLocations.AnyAsync(a => !a.IsDeleted && a.Id == armoryId && a.FacilityId == facilityId, cancellationToken);
        if (!exists)
        {
            throw new KeyNotFoundException("موقع العهدة غير موجود.");
        }
    }

    private IQueryable<WeaponAsset> WeaponsInFacility(Guid facilityId) =>
        db.WeaponAssets.AsNoTracking().Where(w => !w.IsDeleted && w.CurrentFacilityId == facilityId);

    private IQueryable<SensitiveResourceRequirement> ActiveRequirements(Guid facilityId, DateTimeOffset now) =>
        db.SensitiveResourceRequirements.AsNoTracking().Where(r => !r.IsDeleted
            && r.FacilityId == facilityId
            && r.EffectiveFromUtc <= now
            && (r.EffectiveToUtc == null || r.EffectiveToUtc > now));

    private async Task<bool> HasCriticalInventoryDifferenceAsync(Guid sessionId, CancellationToken cancellationToken) =>
        await db.InventoryEntries.AnyAsync(e => !e.IsDeleted
            && e.InventorySessionId == sessionId
            && e.DiscrepancyType != InventoryDiscrepancyType.None,
            cancellationToken);

    private async Task<int> MissingRequirementCountAsync(Guid facilityId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var hasRequirements = await ActiveRequirements(facilityId, now).AnyAsync(cancellationToken);
        return hasRequirements ? 0 : 1;
    }

    private async Task<SensitiveCustodyDataQualityIssueDto> IssueAsync(
        string code,
        string severity,
        string impact,
        string source,
        string correctiveAction,
        string drillDown,
        Task<int> countTask) =>
        new()
        {
            Code = code,
            Count = await countTask,
            Severity = severity,
            ImpactAr = impact,
            Source = source,
            OwnerRole = "ArmamentOfficer",
            CorrectiveActionAr = correctiveAction,
            DrillDown = drillDown
        };

    private async Task<IReadOnlyList<SensitiveCustodyInterventionDto>> WeaponInterventionsAsync(Guid facilityId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var rows = await WeaponsInFacility(facilityId)
            .Where(w => w.CurrentStatus == WeaponStatus.Missing
                || w.CurrentStatus == WeaponStatus.UnderInvestigation
                || w.Condition == WeaponCondition.Unserviceable
                || (w.NextInspectionDueAtUtc != null && w.NextInspectionDueAtUtc < now)
                || w.LastVerifiedAtUtc == null)
            .OrderBy(w => w.InternalAssetCode)
            .Take(options.WorkspaceLimit)
            .Select(w => new { w.Id, w.InternalAssetCode, w.CurrentStatus, w.Condition, w.NextInspectionDueAtUtc, w.LastVerifiedAtUtc })
            .ToListAsync(cancellationToken);
        return rows.Select(w => BuildWeaponIntervention(w.Id, w.InternalAssetCode, w.CurrentStatus, w.Condition, w.NextInspectionDueAtUtc, w.LastVerifiedAtUtc)).ToList();
    }

    private static SensitiveCustodyInterventionDto BuildWeaponIntervention(
        Guid id,
        string assetCode,
        WeaponStatus status,
        WeaponCondition condition,
        DateTimeOffset? nextInspectionDue,
        DateTimeOffset? lastVerified)
    {
        if (status == WeaponStatus.Missing)
        {
            return Intervention(SensitiveCustodyOperationalCatalog.Interventions.WeaponMissing, "Critical", $"السلاح {assetCode} مفقود.", "WeaponAsset", id, "فتح بلاغ فقد", $"weapon:{id}");
        }

        if (status == WeaponStatus.UnderInvestigation)
        {
            return Intervention(SensitiveCustodyOperationalCatalog.Interventions.WeaponUnaccountedFor, "Critical", $"السلاح {assetCode} قيد التحقيق.", "WeaponAsset", id, "متابعة التحقيق", $"weapon:{id}");
        }

        if (condition == WeaponCondition.Unserviceable)
        {
            return Intervention(SensitiveCustodyOperationalCatalog.Interventions.WeaponUnserviceable, "High", $"السلاح {assetCode} غير صالح.", "WeaponAsset", id, "إرساله للصيانة", $"weapon:{id}");
        }

        if (nextInspectionDue is not null)
        {
            return Intervention(SensitiveCustodyOperationalCatalog.Interventions.WeaponInspectionExpired, "High", $"فحص السلاح {assetCode} مستحق.", "WeaponAsset", id, "تسجيل فحص", $"weapon:{id}", nextInspectionDue);
        }

        return Intervention(SensitiveCustodyOperationalCatalog.Interventions.UnverifiedWeapon, "Medium", $"السلاح {assetCode} غير متحقق.", "WeaponAsset", id, "تنفيذ تحقق", $"weapon:{id}", lastVerified);
    }

    private async Task<IReadOnlyList<SensitiveCustodyInterventionDto>> AmmunitionInterventionsAsync(Guid facilityId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var expired = await db.AmmunitionLots.AsNoTracking()
            .Where(l => !l.IsDeleted && l.FacilityId == facilityId && l.ExpiryDateUtc != null && l.ExpiryDateUtc < now && l.CurrentQuantity > 0)
            .Take(options.WorkspaceLimit)
            .Select(l => new { l.Id, l.ExpiryDateUtc })
            .ToListAsync(cancellationToken);
        return expired
            .Select(l => Intervention(SensitiveCustodyOperationalCatalog.Interventions.AmmunitionExpired, "High", "دفعة ذخيرة منتهية ما زالت متاحة.", "AmmunitionLot", l.Id, "حجر أو إتلاف", $"ammunition:{l.Id}", l.ExpiryDateUtc))
            .ToList();
    }

    private async Task<IReadOnlyList<SensitiveCustodyInterventionDto>> InventoryInterventionsAsync(Guid facilityId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var discrepancies = await db.InventoryEntries.AsNoTracking()
            .Where(e => !e.IsDeleted
                && e.InventorySession.FacilityId == facilityId
                && e.DiscrepancyType != InventoryDiscrepancyType.None
                && e.ResolvedAtUtc == null)
            .Take(options.WorkspaceLimit)
            .Select(e => new { e.Id, e.DiscrepancyType, e.VerifiedAtUtc })
            .ToListAsync(cancellationToken);
        return discrepancies
            .Select(e => Intervention(SensitiveCustodyOperationalCatalog.Interventions.InventoryDiscrepancyCritical, "Critical", $"فرق جرد غير محلول: {e.DiscrepancyType}.", "InventoryEntry", e.Id, "معالجة الفرق", $"inventory-entry:{e.Id}", e.VerifiedAtUtc))
            .ToList();
    }

    private static SensitiveCustodyInterventionDto Intervention(
        string code,
        string severity,
        string reason,
        string entityType,
        Guid? entityId,
        string primaryAction,
        string drillDown,
        DateTimeOffset? dueAt = null) =>
        new()
        {
            Code = code,
            Severity = severity,
            ReasonAr = reason,
            SourceEntityType = entityType,
            SourceEntityId = entityId,
            OwnerRole = "ArmamentOfficer",
            DueAtUtc = dueAt,
            PrimaryAction = primaryAction,
            DrillDown = drillDown
        };

    private static int SeverityRank(string severity) =>
        severity switch
        {
            "Critical" => 4,
            "High" => 3,
            "Medium" => 2,
            _ => 1
        };

    private static WeaponAssetListItemDto ToWeaponListItem(WeaponProjectionRow row, bool canViewSerial, bool canViewArmory) =>
        new()
        {
            Id = row.Id,
            InternalAssetCode = row.InternalAssetCode,
            MaskedSerial = SensitiveSerialProtection.Mask(row.SerialNumberHash),
            FullSerial = canViewSerial ? row.SerialNumberEncrypted : null,
            TypeNameAr = row.TypeNameAr,
            Caliber = row.Caliber,
            CurrentStatus = row.CurrentStatus,
            Condition = row.Condition,
            Criticality = row.Criticality,
            CustodyType = row.CustodyType,
            FacilityUnitNameAr = row.FacilityUnitNameAr,
            ArmoryLocationName = canViewArmory ? row.ArmoryLocationName : null,
            LastInspectionAtUtc = row.LastInspectionAtUtc,
            NextInspectionDueAtUtc = row.NextInspectionDueAtUtc,
            LastVerifiedAtUtc = row.LastVerifiedAtUtc
        };

    private static CustodyTransactionDto ToTransactionDto(CustodyTransactionProjectionRow row) =>
        new()
        {
            Id = row.Id,
            WeaponAssetId = row.WeaponAssetId,
            TransactionType = row.TransactionType,
            Status = row.Status,
            IssuedAtUtc = row.IssuedAtUtc,
            ExpectedReturnAtUtc = row.ExpectedReturnAtUtc,
            ReturnedAtUtc = row.ReturnedAtUtc,
            PurposeCode = row.PurposeCode,
            Reason = row.Reason,
            CreatedBy = row.CreatedBy,
            ApprovedBy = row.ApprovedBy,
            ReceivedBy = row.ReceivedBy,
            RowVersion = Convert.ToBase64String(row.RowVersion)
        };

    private static InventorySessionDto ToInventoryDto(InventorySessionProjectionRow row) =>
        new()
        {
            Id = row.Id,
            InventoryType = row.InventoryType,
            Status = row.Status,
            StartedAtUtc = row.StartedAtUtc,
            CompletedAtUtc = row.CompletedAtUtc,
            ExpectedWeaponCount = row.ExpectedWeaponCount,
            CountedWeaponCount = row.CountedWeaponCount,
            ExpectedAmmunitionQuantity = row.ExpectedAmmunitionQuantity,
            CountedAmmunitionQuantity = row.CountedAmmunitionQuantity,
            DifferenceStatus = row.DifferenceStatus,
            RowVersion = Convert.ToBase64String(row.RowVersion)
        };

    private IReadOnlyList<SensitiveCustodyAllowedActionDto> BuildAllowedActions() =>
    [
        new("CREATE_WEAPON", "تسجيل سلاح", currentUser.HasPermission(PermissionCodes.SensitiveCustodyManageWeapons), "تحتاج صلاحية إدارة الأسلحة."),
        new("ISSUE", "إصدار عهدة", currentUser.HasPermission(PermissionCodes.SensitiveCustodyIssueWeapons), "تحتاج صلاحية إصدار العهد."),
        new("RECEIVE", "استلام عهدة", currentUser.HasPermission(PermissionCodes.SensitiveCustodyReceiveWeapons), "تحتاج صلاحية استلام العهد."),
        new("APPROVE_TRANSACTION", "اعتماد عملية عهدة", currentUser.HasPermission(PermissionCodes.SensitiveCustodyApproveTransactions), "تحتاج صلاحية الاعتماد."),
        new("INVENTORY", "بدء جرد", currentUser.HasPermission(PermissionCodes.SensitiveCustodyConductInventory), "تحتاج صلاحية الجرد."),
        new("APPROVE_INVENTORY", "اعتماد جرد", currentUser.HasPermission(PermissionCodes.SensitiveCustodyApproveInventory), "تحتاج صلاحية اعتماد الجرد."),
        new("INSPECTION", "تسجيل فحص", currentUser.HasPermission(PermissionCodes.SensitiveCustodyManageInspections), "تحتاج صلاحية إدارة الفحص."),
        new("AMMUNITION", "تسجيل حركة ذخيرة", currentUser.HasPermission(PermissionCodes.SensitiveCustodyManageAmmunition), "تحتاج صلاحية إدارة الذخيرة.")
    ];

    private SensitiveCustodyImportResult ValidateImport(SensitiveCustodyImportPreviewRequest request)
    {
        var errors = new List<string>();
        if (request.Rows.Count > 1000)
        {
            errors.Add("تجاوز عدد الصفوف الحد الأقصى.");
        }

        var duplicateRows = request.Rows
            .Where(r => !string.IsNullOrWhiteSpace(r.AssetCode))
            .GroupBy(r => r.AssetCode!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Sum(g => g.Count());
        var rejected = request.Rows.Count(r => string.IsNullOrWhiteSpace(r.AssetCode) && request.ImportKind == SensitiveCustodyImportKind.WeaponMaster);
        return new SensitiveCustodyImportResult(
            request.Rows.Count,
            request.Rows.Count - rejected,
            rejected,
            duplicateRows,
            0,
            errors);
    }

    private void Require(string permission)
    {
        if (!currentUser.HasPermission(permission))
        {
            throw new UnauthorizedAccessException(MissingPermissionMessage);
        }
    }

    private void EnforceFourEyes(string createdBy)
    {
        if (string.Equals(createdBy, ActorReference(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("لا يمكن لمنشئ العملية اعتمادها.");
        }
    }

    private string ActorReference() =>
        currentUser.ExternalSubject ?? currentUser.DisplayName ?? currentUser.UserId?.ToString() ?? "unknown";

    private static void EnsureCurrentRowVersion(Baseera.Domain.Common.EntityBase entity, string rowVersion)
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
            throw new InvalidOperationException("تم تعديل السجل بواسطة مستخدم آخر.");
        }
    }

    private async Task AuditAsync(string action, string entityType, Guid entityId, CancellationToken cancellationToken)
    {
        await audit.WriteAsync(new AuditEntry
        {
            Action = action,
            Module = SensitiveModule,
            EntityType = entityType,
            EntityId = entityId.ToString(),
            NewValues = new Dictionary<string, string>
            {
                ["Actor"] = ActorReference()
            },
            IsSensitiveView = true
        },
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record SensitiveWeaponCounts(
        int Total,
        int InArmory,
        int IssuedToMembers,
        int IssuedToUnits,
        int UnderMaintenance,
        int OutOfService,
        int MissingOrInvestigating,
        int DueInspections,
        int Stale,
        int Verified,
        int Operational)
    {
        public static SensitiveWeaponCounts Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    private sealed record WeaponProjectionRow(
        Guid Id,
        string InternalAssetCode,
        string SerialNumberEncrypted,
        string SerialNumberHash,
        string TypeNameAr,
        string Caliber,
        WeaponStatus CurrentStatus,
        WeaponCondition Condition,
        WeaponCriticality Criticality,
        CustodyLocationType CustodyType,
        string? FacilityUnitNameAr,
        string? ArmoryLocationName,
        DateTimeOffset? LastInspectionAtUtc,
        DateTimeOffset? NextInspectionDueAtUtc,
        DateTimeOffset? LastVerifiedAtUtc);

    private sealed record CustodyTransactionProjectionRow(
        Guid Id,
        Guid WeaponAssetId,
        CustodyTransactionType TransactionType,
        CustodyTransactionStatus Status,
        DateTimeOffset IssuedAtUtc,
        DateTimeOffset? ExpectedReturnAtUtc,
        DateTimeOffset? ReturnedAtUtc,
        string PurposeCode,
        string Reason,
        string CreatedBy,
        string? ApprovedBy,
        string? ReceivedBy,
        byte[] RowVersion);

    private sealed record InventorySessionProjectionRow(
        Guid Id,
        InventoryType InventoryType,
        InventoryStatus Status,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset? CompletedAtUtc,
        int ExpectedWeaponCount,
        int CountedWeaponCount,
        int ExpectedAmmunitionQuantity,
        int CountedAmmunitionQuantity,
        InventoryDifferenceStatus DifferenceStatus,
        byte[] RowVersion);
}
