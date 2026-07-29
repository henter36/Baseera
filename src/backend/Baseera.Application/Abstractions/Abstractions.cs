namespace Baseera.Application.Abstractions;

using Baseera.Domain.Audit;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.CorrectiveActions;
using Baseera.Domain.Escalations;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Domain.Occupancy;
using Baseera.Domain.Organization;
using Baseera.Domain.Resources;
using Baseera.Domain.RiskManagement;
using Baseera.Domain.SensitiveCustody;
using Baseera.Domain.Workforce;
using System.Data;

public interface IBaseeraDbContext
{
    IQueryable<Organization> Organizations { get; }
    IQueryable<Region> Regions { get; }
    IQueryable<Facility> Facilities { get; }
    IQueryable<FacilityUnit> FacilityUnits { get; }
    IQueryable<Building> Buildings { get; }
    IQueryable<FacilityAssetLocation> FacilityAssetLocations { get; }
    IQueryable<Department> Departments { get; }
    IQueryable<User> Users { get; }
    /// <summary>Includes soft-deleted users for administrative validation only.</summary>
    IQueryable<User> UsersIncludingDeleted { get; }
    IQueryable<Role> Roles { get; }
    /// <summary>Includes soft-deleted roles for administrative validation only.</summary>
    IQueryable<Role> RolesIncludingDeleted { get; }
    IQueryable<Permission> Permissions { get; }
    IQueryable<UserRole> UserRoles { get; }
    IQueryable<RolePermission> RolePermissions { get; }
    IQueryable<UserScope> UserScopes { get; }
    IQueryable<AuditLog> AuditLogs { get; }
    IQueryable<Attachment> Attachments { get; }
    IQueryable<NoteType> NoteTypes { get; }
    IQueryable<RoleNoteTypeGrant> RoleNoteTypeGrants { get; }
    IQueryable<UserNoteTypeOverride> UserNoteTypeOverrides { get; }
    IQueryable<UserNoteIntakeProfile> UserNoteIntakeProfiles { get; }
    IQueryable<NoteRoutingRule> NoteRoutingRules { get; }
    IQueryable<NoteRoutingRule> NoteRoutingRulesIncludingDeleted { get; }
    IQueryable<NoteRoutingDecision> NoteRoutingDecisions { get; }
    IQueryable<NoteRoutingRuleHistory> NoteRoutingRuleHistories { get; }
    IQueryable<NoteTypeAccessChangeHistory> NoteTypeAccessChangeHistories { get; }
    IQueryable<OperationalNote> OperationalNotes { get; }
    /// <summary>Includes soft-deleted notes for archive restore only.</summary>
    IQueryable<OperationalNote> OperationalNotesIncludingDeleted { get; }
    IQueryable<NoteAssignment> NoteAssignments { get; }
    IQueryable<NoteStatusHistory> NoteStatusHistories { get; }
    IQueryable<NoteDecisionApproval> NoteDecisionApprovals { get; }
    IQueryable<NotePartsRequirement> NotePartsRequirements { get; }
    IQueryable<NoteSlaPausePeriod> NoteSlaPausePeriods { get; }
    IQueryable<CorrectiveAction> CorrectiveActions { get; }
    IQueryable<CorrectiveAction> CorrectiveActionsIncludingDeleted { get; }
    IQueryable<CorrectiveActionAssignment> CorrectiveActionAssignments { get; }
    IQueryable<CorrectiveActionStatusHistory> CorrectiveActionStatusHistories { get; }
    IQueryable<EscalationPolicy> EscalationPolicies { get; }
    IQueryable<EscalationPolicy> EscalationPoliciesIncludingDeleted { get; }
    IQueryable<EscalationRule> EscalationRules { get; }
    IQueryable<EscalationRule> EscalationRulesIncludingDeleted { get; }
    IQueryable<EscalationOccurrence> EscalationOccurrences { get; }
    IQueryable<Notification> Notifications { get; }
    IQueryable<NotificationDeliveryAttempt> NotificationDeliveryAttempts { get; }
    IQueryable<BackgroundJobLease> BackgroundJobLeases { get; }
    IQueryable<FormDefinition> FormDefinitions { get; }
    IQueryable<FormDefinition> FormDefinitionsIncludingDeleted { get; }
    IQueryable<FormReviewDecision> FormReviewDecisions { get; }
    IQueryable<FormGovernancePolicy> FormGovernancePolicies { get; }
    IQueryable<FormAccessGrant> FormAccessGrants { get; }
    IQueryable<FormAccessGrant> FormAccessGrantsIncludingDeleted { get; }
    IQueryable<FormVersion> FormVersions { get; }
    IQueryable<FormSchemaSnapshot> FormSchemaSnapshots { get; }
    IQueryable<FormVersionReviewDecision> FormVersionReviewDecisions { get; }
    IQueryable<FormTemplate> FormTemplates { get; }
    IQueryable<FormTemplate> FormTemplatesIncludingDeleted { get; }
    IQueryable<FormDefinitionVersionCounter> FormDefinitionVersionCounters { get; }
    IQueryable<FormCampaign> FormCampaigns { get; }
    IQueryable<FormCampaign> FormCampaignsIncludingDeleted { get; }
    IQueryable<FormTargetRule> FormTargetRules { get; }
    IQueryable<FormCampaignExclusion> FormCampaignExclusions { get; }
    IQueryable<FormCycle> FormCycles { get; }
    IQueryable<FormFacilityAssignment> FormFacilityAssignments { get; }
    IQueryable<OrganizationBusinessCalendarDate> OrganizationBusinessCalendarDates { get; }
    IQueryable<FormCampaignResponsePolicy> FormCampaignResponsePolicies { get; }
    IQueryable<FormResponse> FormResponses { get; }
    IQueryable<FormResponseSubmission> FormResponseSubmissions { get; }
    IQueryable<FormResponseReviewDecision> FormResponseReviewDecisions { get; }
    IQueryable<FormResponseReviewComment> FormResponseReviewComments { get; }
    IQueryable<FormResponseMutation> FormResponseMutations { get; }
    IQueryable<FormResponseHistory> FormResponseHistories { get; }
    IQueryable<FacilityCapacityBaseline> FacilityCapacityBaselines { get; }
    IQueryable<InmateCensusSnapshot> InmateCensusSnapshots { get; }
    IQueryable<InmateMovementEvent> InmateMovementEvents { get; }
    IQueryable<ResourceAsset> ResourceAssets { get; }
    IQueryable<VehicleProfile> VehicleProfiles { get; }
    IQueryable<CommunicationDeviceProfile> CommunicationDeviceProfiles { get; }
    IQueryable<EquipmentProfile> EquipmentProfiles { get; }
    IQueryable<FacilityAssetProfile> FacilityAssetProfiles { get; }
    IQueryable<ResourceStatusEvent> ResourceStatusEvents { get; }
    IQueryable<ResourcePlacement> ResourcePlacements { get; }
    IQueryable<MaintenanceWorkOrder> MaintenanceWorkOrders { get; }
    IQueryable<ResourceRequirement> ResourceRequirements { get; }
    IQueryable<ResourceImportBatch> ResourceImportBatches { get; }
    IQueryable<WorkforceMember> WorkforceMembers { get; }
    IQueryable<WorkforceRoleDefinition> WorkforceRoleDefinitions { get; }
    IQueryable<WorkforceQualification> WorkforceQualifications { get; }
    IQueryable<WorkforceAssignment> WorkforceAssignments { get; }
    IQueryable<StaffingRequirement> StaffingRequirements { get; }
    IQueryable<ShiftDefinition> ShiftDefinitions { get; }
    IQueryable<DutyRoster> DutyRosters { get; }
    IQueryable<DutyRosterAssignment> DutyRosterAssignments { get; }
    IQueryable<WorkforceAvailabilityEvent> WorkforceAvailabilityEvents { get; }
    IQueryable<CriticalPositionRequirement> CriticalPositionRequirements { get; }
    IQueryable<WorkforceReadinessSnapshot> WorkforceReadinessSnapshots { get; }
    IQueryable<WorkforceImportBatch> WorkforceImportBatches { get; }
    IQueryable<WorkforceReconciliationResolution> WorkforceReconciliationResolutions { get; }
    IQueryable<WeaponTypeDefinition> WeaponTypeDefinitions => Enumerable.Empty<WeaponTypeDefinition>().AsQueryable();
    IQueryable<ArmoryLocation> ArmoryLocations => Enumerable.Empty<ArmoryLocation>().AsQueryable();
    IQueryable<WeaponAsset> WeaponAssets => Enumerable.Empty<WeaponAsset>().AsQueryable();
    IQueryable<CustodyTransaction> CustodyTransactions => Enumerable.Empty<CustodyTransaction>().AsQueryable();
    IQueryable<AmmunitionType> AmmunitionTypes => Enumerable.Empty<AmmunitionType>().AsQueryable();
    IQueryable<AmmunitionLot> AmmunitionLots => Enumerable.Empty<AmmunitionLot>().AsQueryable();
    IQueryable<AmmunitionTransaction> AmmunitionTransactions => Enumerable.Empty<AmmunitionTransaction>().AsQueryable();
    IQueryable<SensitiveResourceRequirement> SensitiveResourceRequirements => Enumerable.Empty<SensitiveResourceRequirement>().AsQueryable();
    IQueryable<InventorySession> InventorySessions => Enumerable.Empty<InventorySession>().AsQueryable();
    IQueryable<InventoryEntry> InventoryEntries => Enumerable.Empty<InventoryEntry>().AsQueryable();
    IQueryable<WeaponInspection> WeaponInspections => Enumerable.Empty<WeaponInspection>().AsQueryable();
    IQueryable<SensitiveCustodyImportBatch> SensitiveCustodyImportBatches => Enumerable.Empty<SensitiveCustodyImportBatch>().AsQueryable();
    IQueryable<SensitiveCustodyReconciliationResolution> SensitiveCustodyReconciliationResolutions => Enumerable.Empty<SensitiveCustodyReconciliationResolution>().AsQueryable();
    IQueryable<RiskCategory> RiskCategories => Enumerable.Empty<RiskCategory>().AsQueryable();
    IQueryable<RiskRecord> RiskRecords => Enumerable.Empty<RiskRecord>().AsQueryable();
    IQueryable<RiskStatusHistory> RiskStatusHistories => Enumerable.Empty<RiskStatusHistory>().AsQueryable();
    IQueryable<RiskAssessmentMatrix> RiskAssessmentMatrices => Enumerable.Empty<RiskAssessmentMatrix>().AsQueryable();
    IQueryable<LikelihoodLevel> LikelihoodLevels => Enumerable.Empty<LikelihoodLevel>().AsQueryable();
    IQueryable<ImpactDimension> ImpactDimensions => Enumerable.Empty<ImpactDimension>().AsQueryable();
    IQueryable<ImpactLevel> ImpactLevels => Enumerable.Empty<ImpactLevel>().AsQueryable();
    IQueryable<RiskRatingBand> RiskRatingBands => Enumerable.Empty<RiskRatingBand>().AsQueryable();
    IQueryable<RiskAssessment> RiskAssessments => Enumerable.Empty<RiskAssessment>().AsQueryable();
    IQueryable<RiskAssessmentImpact> RiskAssessmentImpacts => Enumerable.Empty<RiskAssessmentImpact>().AsQueryable();
    IQueryable<RiskControl> RiskControls => Enumerable.Empty<RiskControl>().AsQueryable();
    IQueryable<RiskTreatmentPlan> RiskTreatmentPlans => Enumerable.Empty<RiskTreatmentPlan>().AsQueryable();
    IQueryable<RiskTreatmentAction> RiskTreatmentActions => Enumerable.Empty<RiskTreatmentAction>().AsQueryable();
    IQueryable<RiskSourceLink> RiskSourceLinks => Enumerable.Empty<RiskSourceLink>().AsQueryable();
    IQueryable<RiskReview> RiskReviews => Enumerable.Empty<RiskReview>().AsQueryable();
    IQueryable<RiskImportBatch> RiskImportBatches => Enumerable.Empty<RiskImportBatch>().AsQueryable();
    IQueryable<RiskReconciliationRecord> RiskReconciliationRecords => Enumerable.Empty<RiskReconciliationRecord>().AsQueryable();

    void Add<TEntity>(TEntity entity) where TEntity : class;
    void Update<TEntity>(TEntity entity) where TEntity : class;
    void Remove<TEntity>(TEntity entity) where TEntity : class;
    void Detach<TEntity>(TEntity entity) where TEntity : class;
    void ClearChanges();
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<long> NextOperationalNoteSequenceValueAsync(CancellationToken cancellationToken = default);
    Task<long> NextCorrectiveActionSequenceValueAsync(CancellationToken cancellationToken = default);
    Task<long> NextMaintenanceWorkOrderSequenceValueAsync(CancellationToken cancellationToken = default);
    Task<int> AllocateFormVersionNumberAsync(Guid formDefinitionId, CancellationToken cancellationToken = default);
    Task<long> NextRiskRecordSequenceValueAsync(CancellationToken cancellationToken = default);
}

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    string? ExternalSubject { get; }
    string? DisplayName { get; }
    string? IpAddress { get; }
    string? CorrelationId { get; }
    IReadOnlyCollection<string> Permissions { get; }
    IReadOnlyCollection<UserScopeSnapshot> Scopes { get; }
    bool HasPermission(string permissionCode);
    bool IsGlobalScope { get; }
    bool HasHeadquartersScope { get; }
}

public sealed record UserScopeSnapshot(
    ScopeType ScopeType,
    Guid? RegionId,
    Guid? FacilityId,
    Guid? FacilityUnitId);

public interface IOrganizationalScopeService
{
    bool HasNationalAccess { get; }
    bool HasHeadquartersAccess { get; }
    bool CanAccessRegion(Guid regionId);
    bool CanAccessFacility(Guid facilityId);
    bool CanAccessFacilityUnit(Guid facilityUnitId);
    IQueryable<Region> FilterRegions(IQueryable<Region> query);
    IQueryable<Facility> FilterFacilities(IQueryable<Facility> query);
    bool CanAccess(IScopedEntity entity);
    string SummarizeScopes();
}

public interface IAuditService
{
    Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}

public sealed class AuditEntry
{
    public required string Action { get; init; }
    public required string Module { get; init; }
    public required string EntityType { get; init; }
    public string? EntityId { get; init; }
    public object? OldValues { get; init; }
    public object? NewValues { get; init; }
    public string? Reason { get; init; }
    public string Outcome { get; init; } = "Success";
    public bool IsSensitiveView { get; init; }
}

public interface IFileStorage
{
    Task<StoredFileResult> SaveAsync(Stream content, string storedFileName, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);
}

public sealed record StoredFileResult(string StoragePath);

public interface IAttachmentService
{
    Task<Attachment> UploadAsync(UploadAttachmentRequest request, CancellationToken cancellationToken = default);
    Task<(Attachment Attachment, Stream Content)> DownloadAsync(Guid attachmentId, CancellationToken cancellationToken = default);
    /// <summary>Metadata-only listing for an entity. Missing/out-of-scope entities throw KeyNotFoundException (404), matching DownloadAsync.</summary>
    Task<IReadOnlyList<Attachment>> ListForEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default);
}

public sealed class UploadAttachmentRequest
{
    public required string EntityType { get; init; }
    public required Guid EntityId { get; init; }
    public required string OriginalFileName { get; init; }
    public required string ContentType { get; init; }
    public required Stream Content { get; init; }
    public required long SizeBytes { get; init; }
    public ClassificationLevel Classification { get; init; } = ClassificationLevel.Internal;
    public string? UploadReason { get; init; }
}
