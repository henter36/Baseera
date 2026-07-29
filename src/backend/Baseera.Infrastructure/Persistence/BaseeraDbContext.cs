namespace Baseera.Infrastructure.Persistence;

using Baseera.Domain.Attachments;
using Baseera.Domain.Audit;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data;

public sealed class BaseeraDbContext(DbContextOptions<BaseeraDbContext> options) : DbContext(options), Application.Abstractions.IBaseeraDbContext
{
    private const string InMemoryProviderName = "Microsoft.EntityFrameworkCore.InMemory";

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<FacilityUnit> FacilityUnits => Set<FacilityUnit>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<FacilityAssetLocation> FacilityAssetLocations => Set<FacilityAssetLocation>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserScope> UserScopes => Set<UserScope>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<NoteType> NoteTypes => Set<NoteType>();
    public DbSet<RoleNoteTypeGrant> RoleNoteTypeGrants => Set<RoleNoteTypeGrant>();
    public DbSet<UserNoteTypeOverride> UserNoteTypeOverrides => Set<UserNoteTypeOverride>();
    public DbSet<UserNoteIntakeProfile> UserNoteIntakeProfiles => Set<UserNoteIntakeProfile>();
    public DbSet<NoteRoutingRule> NoteRoutingRules => Set<NoteRoutingRule>();
    public DbSet<NoteRoutingDecision> NoteRoutingDecisions => Set<NoteRoutingDecision>();
    public DbSet<NoteRoutingRuleHistory> NoteRoutingRuleHistories => Set<NoteRoutingRuleHistory>();
    public DbSet<NoteTypeAccessChangeHistory> NoteTypeAccessChangeHistories => Set<NoteTypeAccessChangeHistory>();
    public DbSet<OperationalNote> OperationalNotes => Set<OperationalNote>();
    public DbSet<NoteAssignment> NoteAssignments => Set<NoteAssignment>();
    public DbSet<NoteStatusHistory> NoteStatusHistories => Set<NoteStatusHistory>();
    public DbSet<NoteDecisionApproval> NoteDecisionApprovals => Set<NoteDecisionApproval>();
    public DbSet<NotePartsRequirement> NotePartsRequirements => Set<NotePartsRequirement>();
    public DbSet<NoteSlaPausePeriod> NoteSlaPausePeriods => Set<NoteSlaPausePeriod>();
    public DbSet<CorrectiveAction> CorrectiveActions => Set<CorrectiveAction>();
    public DbSet<CorrectiveActionAssignment> CorrectiveActionAssignments => Set<CorrectiveActionAssignment>();
    public DbSet<CorrectiveActionStatusHistory> CorrectiveActionStatusHistories => Set<CorrectiveActionStatusHistory>();
    public DbSet<EscalationPolicy> EscalationPolicies => Set<EscalationPolicy>();
    public DbSet<EscalationRule> EscalationRules => Set<EscalationRule>();
    public DbSet<EscalationOccurrence> EscalationOccurrences => Set<EscalationOccurrence>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationDeliveryAttempt> NotificationDeliveryAttempts => Set<NotificationDeliveryAttempt>();
    public DbSet<BackgroundJobLease> BackgroundJobLeases => Set<BackgroundJobLease>();
    public DbSet<FormDefinition> FormDefinitions => Set<FormDefinition>();
    public DbSet<FormReviewDecision> FormReviewDecisions => Set<FormReviewDecision>();
    public DbSet<FormGovernancePolicy> FormGovernancePolicies => Set<FormGovernancePolicy>();
    public DbSet<FormAccessGrant> FormAccessGrants => Set<FormAccessGrant>();
    public DbSet<FormVersion> FormVersions => Set<FormVersion>();
    public DbSet<FormSchemaSnapshot> FormSchemaSnapshots => Set<FormSchemaSnapshot>();
    public DbSet<FormVersionReviewDecision> FormVersionReviewDecisions => Set<FormVersionReviewDecision>();
    public DbSet<FormTemplate> FormTemplates => Set<FormTemplate>();
    public DbSet<FormDefinitionVersionCounter> FormDefinitionVersionCounters => Set<FormDefinitionVersionCounter>();
    public DbSet<FormCampaign> FormCampaigns => Set<FormCampaign>();
    public DbSet<FormTargetRule> FormTargetRules => Set<FormTargetRule>();
    public DbSet<FormCampaignExclusion> FormCampaignExclusions => Set<FormCampaignExclusion>();
    public DbSet<FormCycle> FormCycles => Set<FormCycle>();
    public DbSet<FormFacilityAssignment> FormFacilityAssignments => Set<FormFacilityAssignment>();
    public DbSet<OrganizationBusinessCalendarDate> OrganizationBusinessCalendarDates => Set<OrganizationBusinessCalendarDate>();
    public DbSet<FormCampaignResponsePolicy> FormCampaignResponsePolicies => Set<FormCampaignResponsePolicy>();
    public DbSet<FormResponse> FormResponses => Set<FormResponse>();
    public DbSet<FormResponseSubmission> FormResponseSubmissions => Set<FormResponseSubmission>();
    public DbSet<FormResponseReviewDecision> FormResponseReviewDecisions => Set<FormResponseReviewDecision>();
    public DbSet<FormResponseReviewComment> FormResponseReviewComments => Set<FormResponseReviewComment>();
    public DbSet<FormResponseMutation> FormResponseMutations => Set<FormResponseMutation>();
    public DbSet<FormResponseHistory> FormResponseHistories => Set<FormResponseHistory>();
    public DbSet<FacilityCapacityBaseline> FacilityCapacityBaselines => Set<FacilityCapacityBaseline>();
    public DbSet<InmateCensusSnapshot> InmateCensusSnapshots => Set<InmateCensusSnapshot>();
    public DbSet<InmateMovementEvent> InmateMovementEvents => Set<InmateMovementEvent>();
    public DbSet<ResourceAsset> ResourceAssets => Set<ResourceAsset>();
    public DbSet<VehicleProfile> VehicleProfiles => Set<VehicleProfile>();
    public DbSet<CommunicationDeviceProfile> CommunicationDeviceProfiles => Set<CommunicationDeviceProfile>();
    public DbSet<EquipmentProfile> EquipmentProfiles => Set<EquipmentProfile>();
    public DbSet<FacilityAssetProfile> FacilityAssetProfiles => Set<FacilityAssetProfile>();
    public DbSet<ResourceStatusEvent> ResourceStatusEvents => Set<ResourceStatusEvent>();
    public DbSet<ResourcePlacement> ResourcePlacements => Set<ResourcePlacement>();
    public DbSet<MaintenanceWorkOrder> MaintenanceWorkOrders => Set<MaintenanceWorkOrder>();
    public DbSet<ResourceRequirement> ResourceRequirements => Set<ResourceRequirement>();
    public DbSet<ResourceImportBatch> ResourceImportBatches => Set<ResourceImportBatch>();
    public DbSet<WorkforceMember> WorkforceMembers => Set<WorkforceMember>();
    public DbSet<WorkforceRoleDefinition> WorkforceRoleDefinitions => Set<WorkforceRoleDefinition>();
    public DbSet<WorkforceQualification> WorkforceQualifications => Set<WorkforceQualification>();
    public DbSet<WorkforceAssignment> WorkforceAssignments => Set<WorkforceAssignment>();
    public DbSet<StaffingRequirement> StaffingRequirements => Set<StaffingRequirement>();
    public DbSet<ShiftDefinition> ShiftDefinitions => Set<ShiftDefinition>();
    public DbSet<DutyRoster> DutyRosters => Set<DutyRoster>();
    public DbSet<DutyRosterAssignment> DutyRosterAssignments => Set<DutyRosterAssignment>();
    public DbSet<WorkforceAvailabilityEvent> WorkforceAvailabilityEvents => Set<WorkforceAvailabilityEvent>();
    public DbSet<CriticalPositionRequirement> CriticalPositionRequirements => Set<CriticalPositionRequirement>();
    public DbSet<WorkforceReadinessSnapshot> WorkforceReadinessSnapshots => Set<WorkforceReadinessSnapshot>();
    public DbSet<WorkforceImportBatch> WorkforceImportBatches => Set<WorkforceImportBatch>();
    public DbSet<WorkforceReconciliationResolution> WorkforceReconciliationResolutions => Set<WorkforceReconciliationResolution>();
    public DbSet<WeaponTypeDefinition> WeaponTypeDefinitions => Set<WeaponTypeDefinition>();
    public DbSet<ArmoryLocation> ArmoryLocations => Set<ArmoryLocation>();
    public DbSet<WeaponAsset> WeaponAssets => Set<WeaponAsset>();
    public DbSet<CustodyTransaction> CustodyTransactions => Set<CustodyTransaction>();
    public DbSet<AmmunitionType> AmmunitionTypes => Set<AmmunitionType>();
    public DbSet<AmmunitionLot> AmmunitionLots => Set<AmmunitionLot>();
    public DbSet<AmmunitionTransaction> AmmunitionTransactions => Set<AmmunitionTransaction>();
    public DbSet<SensitiveResourceRequirement> SensitiveResourceRequirements => Set<SensitiveResourceRequirement>();
    public DbSet<InventorySession> InventorySessions => Set<InventorySession>();
    public DbSet<InventoryEntry> InventoryEntries => Set<InventoryEntry>();
    public DbSet<WeaponInspection> WeaponInspections => Set<WeaponInspection>();
    public DbSet<SensitiveCustodyImportBatch> SensitiveCustodyImportBatches => Set<SensitiveCustodyImportBatch>();
    public DbSet<SensitiveCustodyReconciliationResolution> SensitiveCustodyReconciliationResolutions => Set<SensitiveCustodyReconciliationResolution>();
    public DbSet<RiskCategory> RiskCategories => Set<RiskCategory>();
    public DbSet<RiskRecord> RiskRecords => Set<RiskRecord>();
    public DbSet<RiskStatusHistory> RiskStatusHistories => Set<RiskStatusHistory>();
    public DbSet<RiskAssessmentMatrix> RiskAssessmentMatrices => Set<RiskAssessmentMatrix>();
    public DbSet<LikelihoodLevel> LikelihoodLevels => Set<LikelihoodLevel>();
    public DbSet<ImpactDimension> ImpactDimensions => Set<ImpactDimension>();
    public DbSet<ImpactLevel> ImpactLevels => Set<ImpactLevel>();
    public DbSet<RiskRatingBand> RiskRatingBands => Set<RiskRatingBand>();
    public DbSet<RiskAssessment> RiskAssessments => Set<RiskAssessment>();
    public DbSet<RiskAssessmentImpact> RiskAssessmentImpacts => Set<RiskAssessmentImpact>();
    public DbSet<RiskControl> RiskControls => Set<RiskControl>();
    public DbSet<RiskTreatmentPlan> RiskTreatmentPlans => Set<RiskTreatmentPlan>();
    public DbSet<RiskTreatmentAction> RiskTreatmentActions => Set<RiskTreatmentAction>();
    public DbSet<RiskSourceLink> RiskSourceLinks => Set<RiskSourceLink>();
    public DbSet<RiskReview> RiskReviews => Set<RiskReview>();
    public DbSet<RiskImportBatch> RiskImportBatches => Set<RiskImportBatch>();
    public DbSet<RiskReconciliationRecord> RiskReconciliationRecords => Set<RiskReconciliationRecord>();

    IQueryable<Organization> Application.Abstractions.IBaseeraDbContext.Organizations => Organizations;
    IQueryable<Region> Application.Abstractions.IBaseeraDbContext.Regions => Regions;
    IQueryable<Facility> Application.Abstractions.IBaseeraDbContext.Facilities => Facilities;
    IQueryable<FacilityUnit> Application.Abstractions.IBaseeraDbContext.FacilityUnits => FacilityUnits;
    IQueryable<Building> Application.Abstractions.IBaseeraDbContext.Buildings => Buildings;
    IQueryable<FacilityAssetLocation> Application.Abstractions.IBaseeraDbContext.FacilityAssetLocations => FacilityAssetLocations;
    IQueryable<Department> Application.Abstractions.IBaseeraDbContext.Departments => Departments;
    IQueryable<User> Application.Abstractions.IBaseeraDbContext.Users => Users;
    IQueryable<User> Application.Abstractions.IBaseeraDbContext.UsersIncludingDeleted => Users.IgnoreQueryFilters();
    IQueryable<Role> Application.Abstractions.IBaseeraDbContext.Roles => Roles;
    IQueryable<Role> Application.Abstractions.IBaseeraDbContext.RolesIncludingDeleted => Roles.IgnoreQueryFilters();
    IQueryable<Permission> Application.Abstractions.IBaseeraDbContext.Permissions => Permissions;
    IQueryable<UserRole> Application.Abstractions.IBaseeraDbContext.UserRoles => UserRoles;
    IQueryable<RolePermission> Application.Abstractions.IBaseeraDbContext.RolePermissions => RolePermissions;
    IQueryable<UserScope> Application.Abstractions.IBaseeraDbContext.UserScopes => UserScopes;
    IQueryable<AuditLog> Application.Abstractions.IBaseeraDbContext.AuditLogs => AuditLogs;
    IQueryable<Attachment> Application.Abstractions.IBaseeraDbContext.Attachments => Attachments;
    IQueryable<NoteType> Application.Abstractions.IBaseeraDbContext.NoteTypes => NoteTypes;
    IQueryable<RoleNoteTypeGrant> Application.Abstractions.IBaseeraDbContext.RoleNoteTypeGrants => RoleNoteTypeGrants;
    IQueryable<UserNoteTypeOverride> Application.Abstractions.IBaseeraDbContext.UserNoteTypeOverrides => UserNoteTypeOverrides;
    IQueryable<UserNoteIntakeProfile> Application.Abstractions.IBaseeraDbContext.UserNoteIntakeProfiles => UserNoteIntakeProfiles;
    IQueryable<NoteRoutingRule> Application.Abstractions.IBaseeraDbContext.NoteRoutingRules => NoteRoutingRules;
    IQueryable<NoteRoutingRule> Application.Abstractions.IBaseeraDbContext.NoteRoutingRulesIncludingDeleted => NoteRoutingRules.IgnoreQueryFilters();
    IQueryable<NoteRoutingDecision> Application.Abstractions.IBaseeraDbContext.NoteRoutingDecisions => NoteRoutingDecisions;
    IQueryable<NoteRoutingRuleHistory> Application.Abstractions.IBaseeraDbContext.NoteRoutingRuleHistories => NoteRoutingRuleHistories;
    IQueryable<NoteTypeAccessChangeHistory> Application.Abstractions.IBaseeraDbContext.NoteTypeAccessChangeHistories => NoteTypeAccessChangeHistories;
    IQueryable<OperationalNote> Application.Abstractions.IBaseeraDbContext.OperationalNotes => OperationalNotes;
    IQueryable<OperationalNote> Application.Abstractions.IBaseeraDbContext.OperationalNotesIncludingDeleted => OperationalNotes.IgnoreQueryFilters();
    IQueryable<NoteAssignment> Application.Abstractions.IBaseeraDbContext.NoteAssignments => NoteAssignments;
    IQueryable<NoteStatusHistory> Application.Abstractions.IBaseeraDbContext.NoteStatusHistories => NoteStatusHistories;
    IQueryable<NoteDecisionApproval> Application.Abstractions.IBaseeraDbContext.NoteDecisionApprovals => NoteDecisionApprovals;
    IQueryable<NotePartsRequirement> Application.Abstractions.IBaseeraDbContext.NotePartsRequirements => NotePartsRequirements;
    IQueryable<NoteSlaPausePeriod> Application.Abstractions.IBaseeraDbContext.NoteSlaPausePeriods => NoteSlaPausePeriods;
    IQueryable<CorrectiveAction> Application.Abstractions.IBaseeraDbContext.CorrectiveActions => CorrectiveActions;
    IQueryable<CorrectiveAction> Application.Abstractions.IBaseeraDbContext.CorrectiveActionsIncludingDeleted => CorrectiveActions.IgnoreQueryFilters();
    IQueryable<CorrectiveActionAssignment> Application.Abstractions.IBaseeraDbContext.CorrectiveActionAssignments => CorrectiveActionAssignments;
    IQueryable<CorrectiveActionStatusHistory> Application.Abstractions.IBaseeraDbContext.CorrectiveActionStatusHistories => CorrectiveActionStatusHistories;
    IQueryable<EscalationPolicy> Application.Abstractions.IBaseeraDbContext.EscalationPolicies => EscalationPolicies;
    IQueryable<EscalationPolicy> Application.Abstractions.IBaseeraDbContext.EscalationPoliciesIncludingDeleted => EscalationPolicies.IgnoreQueryFilters();
    IQueryable<EscalationRule> Application.Abstractions.IBaseeraDbContext.EscalationRules => EscalationRules;
    IQueryable<EscalationRule> Application.Abstractions.IBaseeraDbContext.EscalationRulesIncludingDeleted => EscalationRules.IgnoreQueryFilters();
    IQueryable<EscalationOccurrence> Application.Abstractions.IBaseeraDbContext.EscalationOccurrences => EscalationOccurrences;
    IQueryable<Notification> Application.Abstractions.IBaseeraDbContext.Notifications => Notifications;
    IQueryable<NotificationDeliveryAttempt> Application.Abstractions.IBaseeraDbContext.NotificationDeliveryAttempts => NotificationDeliveryAttempts;
    IQueryable<BackgroundJobLease> Application.Abstractions.IBaseeraDbContext.BackgroundJobLeases => BackgroundJobLeases;
    IQueryable<FormDefinition> Application.Abstractions.IBaseeraDbContext.FormDefinitions => FormDefinitions;
    IQueryable<FormDefinition> Application.Abstractions.IBaseeraDbContext.FormDefinitionsIncludingDeleted => FormDefinitions.IgnoreQueryFilters();
    IQueryable<FormReviewDecision> Application.Abstractions.IBaseeraDbContext.FormReviewDecisions => FormReviewDecisions;
    IQueryable<FormGovernancePolicy> Application.Abstractions.IBaseeraDbContext.FormGovernancePolicies => FormGovernancePolicies;
    IQueryable<FormAccessGrant> Application.Abstractions.IBaseeraDbContext.FormAccessGrants => FormAccessGrants;
    IQueryable<FormAccessGrant> Application.Abstractions.IBaseeraDbContext.FormAccessGrantsIncludingDeleted => FormAccessGrants.IgnoreQueryFilters();
    IQueryable<FormVersion> Application.Abstractions.IBaseeraDbContext.FormVersions => FormVersions;
    IQueryable<FormSchemaSnapshot> Application.Abstractions.IBaseeraDbContext.FormSchemaSnapshots => FormSchemaSnapshots;
    IQueryable<FormVersionReviewDecision> Application.Abstractions.IBaseeraDbContext.FormVersionReviewDecisions => FormVersionReviewDecisions;
    IQueryable<FormTemplate> Application.Abstractions.IBaseeraDbContext.FormTemplates => FormTemplates;
    IQueryable<FormTemplate> Application.Abstractions.IBaseeraDbContext.FormTemplatesIncludingDeleted => FormTemplates.IgnoreQueryFilters();
    IQueryable<FormDefinitionVersionCounter> Application.Abstractions.IBaseeraDbContext.FormDefinitionVersionCounters => FormDefinitionVersionCounters;
    IQueryable<FormCampaign> Application.Abstractions.IBaseeraDbContext.FormCampaigns => FormCampaigns;
    IQueryable<FormCampaign> Application.Abstractions.IBaseeraDbContext.FormCampaignsIncludingDeleted => FormCampaigns.IgnoreQueryFilters();
    IQueryable<FormTargetRule> Application.Abstractions.IBaseeraDbContext.FormTargetRules => FormTargetRules;
    IQueryable<FormCampaignExclusion> Application.Abstractions.IBaseeraDbContext.FormCampaignExclusions => FormCampaignExclusions;
    IQueryable<FormCycle> Application.Abstractions.IBaseeraDbContext.FormCycles => FormCycles;
    IQueryable<FormFacilityAssignment> Application.Abstractions.IBaseeraDbContext.FormFacilityAssignments => FormFacilityAssignments;
    IQueryable<OrganizationBusinessCalendarDate> Application.Abstractions.IBaseeraDbContext.OrganizationBusinessCalendarDates => OrganizationBusinessCalendarDates;
    IQueryable<FormCampaignResponsePolicy> Application.Abstractions.IBaseeraDbContext.FormCampaignResponsePolicies => FormCampaignResponsePolicies;
    IQueryable<FormResponse> Application.Abstractions.IBaseeraDbContext.FormResponses => FormResponses;
    IQueryable<FormResponseSubmission> Application.Abstractions.IBaseeraDbContext.FormResponseSubmissions => FormResponseSubmissions;
    IQueryable<FormResponseReviewDecision> Application.Abstractions.IBaseeraDbContext.FormResponseReviewDecisions => FormResponseReviewDecisions;
    IQueryable<FormResponseReviewComment> Application.Abstractions.IBaseeraDbContext.FormResponseReviewComments => FormResponseReviewComments;
    IQueryable<FormResponseMutation> Application.Abstractions.IBaseeraDbContext.FormResponseMutations => FormResponseMutations;
    IQueryable<FormResponseHistory> Application.Abstractions.IBaseeraDbContext.FormResponseHistories => FormResponseHistories;
    IQueryable<FacilityCapacityBaseline> Application.Abstractions.IBaseeraDbContext.FacilityCapacityBaselines => FacilityCapacityBaselines;
    IQueryable<InmateCensusSnapshot> Application.Abstractions.IBaseeraDbContext.InmateCensusSnapshots => InmateCensusSnapshots;
    IQueryable<InmateMovementEvent> Application.Abstractions.IBaseeraDbContext.InmateMovementEvents => InmateMovementEvents;
    IQueryable<ResourceAsset> Application.Abstractions.IBaseeraDbContext.ResourceAssets => ResourceAssets;
    IQueryable<VehicleProfile> Application.Abstractions.IBaseeraDbContext.VehicleProfiles => VehicleProfiles;
    IQueryable<CommunicationDeviceProfile> Application.Abstractions.IBaseeraDbContext.CommunicationDeviceProfiles => CommunicationDeviceProfiles;
    IQueryable<EquipmentProfile> Application.Abstractions.IBaseeraDbContext.EquipmentProfiles => EquipmentProfiles;
    IQueryable<FacilityAssetProfile> Application.Abstractions.IBaseeraDbContext.FacilityAssetProfiles => FacilityAssetProfiles;
    IQueryable<ResourceStatusEvent> Application.Abstractions.IBaseeraDbContext.ResourceStatusEvents => ResourceStatusEvents;
    IQueryable<ResourcePlacement> Application.Abstractions.IBaseeraDbContext.ResourcePlacements => ResourcePlacements;
    IQueryable<MaintenanceWorkOrder> Application.Abstractions.IBaseeraDbContext.MaintenanceWorkOrders => MaintenanceWorkOrders;
    IQueryable<ResourceRequirement> Application.Abstractions.IBaseeraDbContext.ResourceRequirements => ResourceRequirements;
    IQueryable<ResourceImportBatch> Application.Abstractions.IBaseeraDbContext.ResourceImportBatches => ResourceImportBatches;
    IQueryable<WorkforceMember> Application.Abstractions.IBaseeraDbContext.WorkforceMembers => WorkforceMembers;
    IQueryable<WorkforceRoleDefinition> Application.Abstractions.IBaseeraDbContext.WorkforceRoleDefinitions => WorkforceRoleDefinitions;
    IQueryable<WorkforceQualification> Application.Abstractions.IBaseeraDbContext.WorkforceQualifications => WorkforceQualifications;
    IQueryable<WorkforceAssignment> Application.Abstractions.IBaseeraDbContext.WorkforceAssignments => WorkforceAssignments;
    IQueryable<StaffingRequirement> Application.Abstractions.IBaseeraDbContext.StaffingRequirements => StaffingRequirements;
    IQueryable<ShiftDefinition> Application.Abstractions.IBaseeraDbContext.ShiftDefinitions => ShiftDefinitions;
    IQueryable<DutyRoster> Application.Abstractions.IBaseeraDbContext.DutyRosters => DutyRosters;
    IQueryable<DutyRosterAssignment> Application.Abstractions.IBaseeraDbContext.DutyRosterAssignments => DutyRosterAssignments;
    IQueryable<WorkforceAvailabilityEvent> Application.Abstractions.IBaseeraDbContext.WorkforceAvailabilityEvents => WorkforceAvailabilityEvents;
    IQueryable<CriticalPositionRequirement> Application.Abstractions.IBaseeraDbContext.CriticalPositionRequirements => CriticalPositionRequirements;
    IQueryable<WorkforceReadinessSnapshot> Application.Abstractions.IBaseeraDbContext.WorkforceReadinessSnapshots => WorkforceReadinessSnapshots;
    IQueryable<WorkforceImportBatch> Application.Abstractions.IBaseeraDbContext.WorkforceImportBatches => WorkforceImportBatches;
    IQueryable<WorkforceReconciliationResolution> Application.Abstractions.IBaseeraDbContext.WorkforceReconciliationResolutions => WorkforceReconciliationResolutions;
    IQueryable<WeaponTypeDefinition> Application.Abstractions.IBaseeraDbContext.WeaponTypeDefinitions => WeaponTypeDefinitions;
    IQueryable<ArmoryLocation> Application.Abstractions.IBaseeraDbContext.ArmoryLocations => ArmoryLocations;
    IQueryable<WeaponAsset> Application.Abstractions.IBaseeraDbContext.WeaponAssets => WeaponAssets;
    IQueryable<CustodyTransaction> Application.Abstractions.IBaseeraDbContext.CustodyTransactions => CustodyTransactions;
    IQueryable<AmmunitionType> Application.Abstractions.IBaseeraDbContext.AmmunitionTypes => AmmunitionTypes;
    IQueryable<AmmunitionLot> Application.Abstractions.IBaseeraDbContext.AmmunitionLots => AmmunitionLots;
    IQueryable<AmmunitionTransaction> Application.Abstractions.IBaseeraDbContext.AmmunitionTransactions => AmmunitionTransactions;
    IQueryable<SensitiveResourceRequirement> Application.Abstractions.IBaseeraDbContext.SensitiveResourceRequirements => SensitiveResourceRequirements;
    IQueryable<InventorySession> Application.Abstractions.IBaseeraDbContext.InventorySessions => InventorySessions;
    IQueryable<InventoryEntry> Application.Abstractions.IBaseeraDbContext.InventoryEntries => InventoryEntries;
    IQueryable<WeaponInspection> Application.Abstractions.IBaseeraDbContext.WeaponInspections => WeaponInspections;
    IQueryable<SensitiveCustodyImportBatch> Application.Abstractions.IBaseeraDbContext.SensitiveCustodyImportBatches => SensitiveCustodyImportBatches;
    IQueryable<SensitiveCustodyReconciliationResolution> Application.Abstractions.IBaseeraDbContext.SensitiveCustodyReconciliationResolutions => SensitiveCustodyReconciliationResolutions;
    IQueryable<RiskCategory> Application.Abstractions.IBaseeraDbContext.RiskCategories => RiskCategories;
    IQueryable<RiskRecord> Application.Abstractions.IBaseeraDbContext.RiskRecords => RiskRecords;
    IQueryable<RiskStatusHistory> Application.Abstractions.IBaseeraDbContext.RiskStatusHistories => RiskStatusHistories;
    IQueryable<RiskAssessmentMatrix> Application.Abstractions.IBaseeraDbContext.RiskAssessmentMatrices => RiskAssessmentMatrices;
    IQueryable<LikelihoodLevel> Application.Abstractions.IBaseeraDbContext.LikelihoodLevels => LikelihoodLevels;
    IQueryable<ImpactDimension> Application.Abstractions.IBaseeraDbContext.ImpactDimensions => ImpactDimensions;
    IQueryable<ImpactLevel> Application.Abstractions.IBaseeraDbContext.ImpactLevels => ImpactLevels;
    IQueryable<RiskRatingBand> Application.Abstractions.IBaseeraDbContext.RiskRatingBands => RiskRatingBands;
    IQueryable<RiskAssessment> Application.Abstractions.IBaseeraDbContext.RiskAssessments => RiskAssessments;
    IQueryable<RiskAssessmentImpact> Application.Abstractions.IBaseeraDbContext.RiskAssessmentImpacts => RiskAssessmentImpacts;
    IQueryable<RiskControl> Application.Abstractions.IBaseeraDbContext.RiskControls => RiskControls;
    IQueryable<RiskTreatmentPlan> Application.Abstractions.IBaseeraDbContext.RiskTreatmentPlans => RiskTreatmentPlans;
    IQueryable<RiskTreatmentAction> Application.Abstractions.IBaseeraDbContext.RiskTreatmentActions => RiskTreatmentActions;
    IQueryable<RiskSourceLink> Application.Abstractions.IBaseeraDbContext.RiskSourceLinks => RiskSourceLinks;
    IQueryable<RiskReview> Application.Abstractions.IBaseeraDbContext.RiskReviews => RiskReviews;
    IQueryable<RiskImportBatch> Application.Abstractions.IBaseeraDbContext.RiskImportBatches => RiskImportBatches;
    IQueryable<RiskReconciliationRecord> Application.Abstractions.IBaseeraDbContext.RiskReconciliationRecords => RiskReconciliationRecords;

    public void Detach<TEntity>(TEntity entity) where TEntity : class => Entry(entity).State = EntityState.Detached;
    public void ClearChanges() => ChangeTracker.Clear();

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
    {
        if (Database.ProviderName == InMemoryProviderName)
        {
            return await operation(cancellationToken);
        }

        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Database.BeginTransactionAsync(isolationLevel, cancellationToken);
            var result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    public new void Add<TEntity>(TEntity entity) where TEntity : class => Set<TEntity>().Add(entity);
    public new void Update<TEntity>(TEntity entity) where TEntity : class => Set<TEntity>().Update(entity);
    public new void Remove<TEntity>(TEntity entity) where TEntity : class => Set<TEntity>().Remove(entity);

    public async Task<long> NextOperationalNoteSequenceValueAsync(CancellationToken cancellationToken = default)
    {
        var rows = await Database
            .SqlQueryRaw<SequenceValueRow>("SELECT NEXT VALUE FOR [OperationalNoteReferenceSequence] AS [Value]")
            .ToListAsync(cancellationToken);
        return rows.Single().Value;
    }

    public async Task<long> NextCorrectiveActionSequenceValueAsync(CancellationToken cancellationToken = default)
    {
        var rows = await Database
            .SqlQueryRaw<SequenceValueRow>("SELECT NEXT VALUE FOR [CorrectiveActionReferenceSequence] AS [Value]")
            .ToListAsync(cancellationToken);
        return rows.Single().Value;
    }

    public async Task<long> NextMaintenanceWorkOrderSequenceValueAsync(CancellationToken cancellationToken = default)
    {
        if (Database.ProviderName == InMemoryProviderName)
        {
            var numbers = await MaintenanceWorkOrders
                .IgnoreQueryFilters()
                .Select(order => order.WorkOrderNumber)
                .ToListAsync(cancellationToken);
            long max = 0;
            foreach (var number in numbers)
            {
                if (number.StartsWith("MWO-", StringComparison.OrdinalIgnoreCase)
                    && long.TryParse(number.AsSpan(4), out var parsed)
                    && parsed > max)
                {
                    max = parsed;
                }
            }

            return max + 1;
        }

        var rows = await Database
            .SqlQueryRaw<SequenceValueRow>("SELECT NEXT VALUE FOR [MaintenanceWorkOrderNumberSequence] AS [Value]")
            .ToListAsync(cancellationToken);
        return rows.Single().Value;
    }

    public async Task<long> NextRiskRecordSequenceValueAsync(CancellationToken cancellationToken = default)
    {
        if (Database.ProviderName == InMemoryProviderName)
        {
            var codes = await RiskRecords.IgnoreQueryFilters().Select(r => r.RiskCode).ToListAsync(cancellationToken);
            long max = 0;
            foreach (var code in codes)
            {
                if (code.StartsWith("RSK-", StringComparison.OrdinalIgnoreCase)
                    && long.TryParse(code.AsSpan(4), out var parsed)
                    && parsed > max)
                {
                    max = parsed;
                }
            }

            return max + 1;
        }

        var rows = await Database
            .SqlQueryRaw<SequenceValueRow>("SELECT NEXT VALUE FOR [RiskRecordReferenceSequence] AS [Value]")
            .ToListAsync(cancellationToken);
        return rows.Single().Value;
    }

    public async Task<int> AllocateFormVersionNumberAsync(Guid formDefinitionId, CancellationToken cancellationToken = default)
    {
        if (formDefinitionId == Guid.Empty)
        {
            throw new ArgumentException("معرّف النموذج مطلوب.", nameof(formDefinitionId));
        }

        if (Database.ProviderName == InMemoryProviderName)
        {
            var max = await FormVersions
                .Where(v => v.FormDefinitionId == formDefinitionId)
                .Select(v => (int?)v.VersionNumber)
                .MaxAsync(cancellationToken);
            return (max ?? 0) + 1;
        }

        var rows = await Database.SqlQueryRaw<SequenceValueRow>(
            """
            MERGE [FormDefinitionVersionCounters] WITH (HOLDLOCK) AS target
            USING (SELECT {0} AS [FormDefinitionId]) AS source
            ON target.[FormDefinitionId] = source.[FormDefinitionId]
            WHEN MATCHED THEN
                UPDATE SET [NextVersionNumber] = target.[NextVersionNumber] + 1
            WHEN NOT MATCHED THEN
                INSERT ([FormDefinitionId], [NextVersionNumber]) VALUES (source.[FormDefinitionId], 2)
            OUTPUT CAST(CASE WHEN $action = N'INSERT' THEN 1 ELSE deleted.[NextVersionNumber] END AS bigint) AS [Value];
            """,
            formDefinitionId).ToListAsync(cancellationToken);
        return checked((int)rows.Single().Value);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        RegisterSequences(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BaseeraDbContext).Assembly);

        ConfigureIdentityAndAccessQueryFilters(modelBuilder);
        ConfigureNotesAndActionsQueryFilters(modelBuilder);
        ConfigureFormsQueryFilters(modelBuilder);
        ConfigureOccupancyAndResourcesQueryFilters(modelBuilder);
        ConfigureWorkforceQueryFilters(modelBuilder);
        ConfigureSensitiveCustodyQueryFilters(modelBuilder);
        ConfigureRiskManagementQueryFilters(modelBuilder);

        ConfigureUserScopeCheckConstraints(modelBuilder);
    }

    private static void RegisterSequences(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<long>("OperationalNoteReferenceSequence")
            .StartsAt(1)
            .IncrementsBy(1);
        modelBuilder.HasSequence<long>("CorrectiveActionReferenceSequence")
            .StartsAt(1)
            .IncrementsBy(1);
        modelBuilder.HasSequence<long>("MaintenanceWorkOrderNumberSequence")
            .StartsAt(1)
            .IncrementsBy(1);
        modelBuilder.HasSequence<long>("RiskRecordReferenceSequence")
            .StartsAt(1)
            .IncrementsBy(1);
    }

    private static void ConfigureIdentityAndAccessQueryFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Region>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Facility>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<FacilityUnit>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Building>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<FacilityAssetLocation>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Department>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<User>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Role>().HasQueryFilter(e => !e.IsDeleted);
        // Join entities must filter deleted Role (and User) to avoid EF 10622 with required navigations.
        modelBuilder.Entity<UserRole>().HasQueryFilter(ur => !ur.Role.IsDeleted && !ur.User.IsDeleted);
        modelBuilder.Entity<RolePermission>().HasQueryFilter(rp => !rp.Role.IsDeleted);
        modelBuilder.Entity<UserScope>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Attachment>().HasQueryFilter(e => !e.IsDeleted);
    }

    private static void ConfigureNotesAndActionsQueryFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RoleNoteTypeGrant>().HasQueryFilter(g => !g.Role.IsDeleted);
        modelBuilder.Entity<UserNoteTypeOverride>().HasQueryFilter(o => !o.User.IsDeleted);
        modelBuilder.Entity<UserNoteIntakeProfile>().HasQueryFilter(p => !p.User.IsDeleted);
        modelBuilder.Entity<NoteRoutingRule>().HasQueryFilter(r => !r.IsDeleted);
        modelBuilder.Entity<OperationalNote>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<CorrectiveAction>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<EscalationPolicy>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<EscalationRule>().HasQueryFilter(e => !e.IsDeleted);
        // Join/dependent entities must filter deleted OperationalNote/User to avoid EF 10622 with required navigations.
        modelBuilder.Entity<NoteAssignment>().HasQueryFilter(na => !na.OperationalNote.IsDeleted && !na.AssignedByUser.IsDeleted);
        modelBuilder.Entity<NoteStatusHistory>().HasQueryFilter(h => !h.OperationalNote.IsDeleted && !h.ChangedByUser.IsDeleted);
        modelBuilder.Entity<NoteDecisionApproval>().HasQueryFilter(a => !a.OperationalNote.IsDeleted && !a.ProposedByUser.IsDeleted);
        modelBuilder.Entity<NotePartsRequirement>().HasQueryFilter(p => !p.OperationalNote.IsDeleted);
        modelBuilder.Entity<NoteSlaPausePeriod>().HasQueryFilter(p => !p.OperationalNote.IsDeleted);
        modelBuilder.Entity<CorrectiveActionAssignment>().HasQueryFilter(a => !a.CorrectiveAction.IsDeleted && !a.AssignedByUser.IsDeleted);
        modelBuilder.Entity<CorrectiveActionStatusHistory>().HasQueryFilter(h => !h.CorrectiveAction.IsDeleted && !h.ChangedByUser.IsDeleted);
        modelBuilder.Entity<EscalationRule>().HasQueryFilter(r => !r.IsDeleted && !r.EscalationPolicy.IsDeleted);
    }

    private static void ConfigureFormsQueryFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FormDefinition>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<FormAccessGrant>().HasQueryFilter(g => !g.IsDeleted);
        modelBuilder.Entity<FormTemplate>().HasQueryFilter(t => !t.IsDeleted);
        modelBuilder.Entity<FormCampaign>().HasQueryFilter(c => !c.IsDeleted);
        modelBuilder.Entity<FormVersionReviewDecision>().HasQueryFilter(d => !d.FormVersion.FormDefinition.IsDeleted);
        modelBuilder.Entity<FormReviewDecision>().HasQueryFilter(d => !d.FormDefinition.IsDeleted);
        modelBuilder.Entity<Notification>().HasQueryFilter(n => !n.RecipientUser.IsDeleted);
        modelBuilder.Entity<NotificationDeliveryAttempt>().HasQueryFilter(a => !a.Notification.RecipientUser.IsDeleted);
    }

    private static void ConfigureOccupancyAndResourcesQueryFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FacilityCapacityBaseline>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<InmateCensusSnapshot>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<InmateMovementEvent>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ResourceAsset>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<VehicleProfile>().HasQueryFilter(e => !e.ResourceAsset.IsDeleted);
        modelBuilder.Entity<CommunicationDeviceProfile>().HasQueryFilter(e => !e.ResourceAsset.IsDeleted);
        modelBuilder.Entity<EquipmentProfile>().HasQueryFilter(e => !e.ResourceAsset.IsDeleted);
        modelBuilder.Entity<FacilityAssetProfile>().HasQueryFilter(e => !e.ResourceAsset.IsDeleted);
        modelBuilder.Entity<ResourceStatusEvent>().HasQueryFilter(e => !e.ResourceAsset.IsDeleted);
        modelBuilder.Entity<ResourcePlacement>().HasQueryFilter(e => !e.ResourceAsset.IsDeleted);
        modelBuilder.Entity<MaintenanceWorkOrder>().HasQueryFilter(e => !e.IsDeleted && !e.ResourceAsset.IsDeleted);
        modelBuilder.Entity<ResourceRequirement>().HasQueryFilter(e => !e.IsDeleted);
    }

    private static void ConfigureWorkforceQueryFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkforceMember>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<WorkforceRoleDefinition>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<WorkforceQualification>().HasQueryFilter(e => !e.IsDeleted && !e.WorkforceMember.IsDeleted);
        modelBuilder.Entity<WorkforceAssignment>().HasQueryFilter(e => !e.IsDeleted && !e.WorkforceMember.IsDeleted);
        modelBuilder.Entity<StaffingRequirement>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ShiftDefinition>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<DutyRoster>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<DutyRosterAssignment>().HasQueryFilter(e => !e.IsDeleted && !e.DutyRoster.IsDeleted);
        modelBuilder.Entity<WorkforceAvailabilityEvent>().HasQueryFilter(e => !e.IsDeleted && !e.WorkforceMember.IsDeleted);
        modelBuilder.Entity<CriticalPositionRequirement>().HasQueryFilter(e => !e.IsDeleted);
    }

    private static void ConfigureSensitiveCustodyQueryFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WeaponTypeDefinition>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ArmoryLocation>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<WeaponAsset>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<CustodyTransaction>().HasQueryFilter(e => !e.IsDeleted && !e.WeaponAsset.IsDeleted);
        modelBuilder.Entity<AmmunitionType>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<AmmunitionLot>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<AmmunitionTransaction>().HasQueryFilter(e => !e.IsDeleted && !e.AmmunitionLot.IsDeleted);
        modelBuilder.Entity<SensitiveResourceRequirement>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<InventorySession>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<InventoryEntry>().HasQueryFilter(e => !e.IsDeleted && !e.InventorySession.IsDeleted);
        modelBuilder.Entity<WeaponInspection>().HasQueryFilter(e => !e.IsDeleted && !e.WeaponAsset.IsDeleted);
        modelBuilder.Entity<SensitiveCustodyImportBatch>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<SensitiveCustodyReconciliationResolution>().HasQueryFilter(e => !e.IsDeleted);
    }

    private static void ConfigureRiskManagementQueryFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RiskCategory>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<RiskRecord>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<RiskStatusHistory>().HasQueryFilter(h => !h.RiskRecord.IsDeleted);
        modelBuilder.Entity<RiskAssessmentMatrix>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<LikelihoodLevel>().HasQueryFilter(e => !e.IsDeleted && !e.Matrix.IsDeleted);
        modelBuilder.Entity<ImpactDimension>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ImpactLevel>().HasQueryFilter(e => !e.IsDeleted && !e.Matrix.IsDeleted);
        modelBuilder.Entity<RiskRatingBand>().HasQueryFilter(e => !e.IsDeleted && !e.Matrix.IsDeleted);
        modelBuilder.Entity<RiskAssessment>().HasQueryFilter(e => !e.IsDeleted && !e.RiskRecord.IsDeleted);
        modelBuilder.Entity<RiskAssessmentImpact>().HasQueryFilter(e => !e.IsDeleted && !e.RiskAssessment.IsDeleted);
        modelBuilder.Entity<RiskControl>().HasQueryFilter(e => !e.IsDeleted && !e.RiskRecord.IsDeleted);
        modelBuilder.Entity<RiskTreatmentPlan>().HasQueryFilter(e => !e.IsDeleted && !e.RiskRecord.IsDeleted);
        modelBuilder.Entity<RiskTreatmentAction>().HasQueryFilter(e => !e.IsDeleted && !e.TreatmentPlan.IsDeleted);
        modelBuilder.Entity<RiskSourceLink>().HasQueryFilter(e => !e.IsDeleted && !e.RiskRecord.IsDeleted);
        modelBuilder.Entity<RiskReview>().HasQueryFilter(e => !e.IsDeleted && !e.RiskRecord.IsDeleted);
        modelBuilder.Entity<RiskImportBatch>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<RiskReconciliationRecord>().HasQueryFilter(e => !e.IsDeleted);
    }

    private static void ConfigureUserScopeCheckConstraints(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserScope>().ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_UserScopes_GlobalHq_NoIds",
                "([ScopeType] NOT IN (0, 1)) OR ([RegionId] IS NULL AND [FacilityId] IS NULL AND [FacilityUnitId] IS NULL)");
            t.HasCheckConstraint(
                "CK_UserScopes_Region_RequiresRegion",
                "([ScopeType] NOT IN (2, 5)) OR ([RegionId] IS NOT NULL AND [FacilityId] IS NULL AND [FacilityUnitId] IS NULL)");
            t.HasCheckConstraint(
                "CK_UserScopes_Facility_RequiresFacility",
                "([ScopeType] NOT IN (3, 6)) OR ([FacilityId] IS NOT NULL AND [FacilityUnitId] IS NULL)");
            t.HasCheckConstraint(
                "CK_UserScopes_Unit_RequiresFacilityAndUnit",
                "([ScopeType] <> 4) OR ([FacilityId] IS NOT NULL AND [FacilityUnitId] IS NOT NULL)");
        });
    }

    public override int SaveChanges()
    {
        EnforceAppendOnlyGuards();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnforceAppendOnlyGuards();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void EnforceAppendOnlyGuards()
    {
        AuditAppendOnlyGuard.EnsureAuditEntriesAreAppendOnly(this);
        NoteStatusHistoryAppendOnlyGuard.EnsureEntriesAreAppendOnly(this);
        CorrectiveActionStatusHistoryAppendOnlyGuard.EnsureEntriesAreAppendOnly(this);
        EscalationAppendOnlyGuard.EnsureEntriesAreAppendOnly(this);
        NoteRoutingAppendOnlyGuard.EnsureEntriesAreAppendOnly(this);
        ResourceStatusEventAppendOnlyGuard.EnsureEntriesAreAppendOnly(this);
        FormSchemaSnapshotImmutabilityGuard.EnsureImmutable(this);
        RiskStatusHistoryAppendOnlyGuard.EnsureEntriesAreAppendOnly(this);
    }

}

internal static class FormSchemaSnapshotImmutabilityGuard
{
    public static void EnsureImmutable(DbContext context)
    {
        var invalid = context.ChangeTracker
            .Entries<FormSchemaSnapshot>()
            .Where(e => e.State is EntityState.Modified or EntityState.Deleted);
        if (invalid.Any())
        {
            throw new InvalidOperationException("FormSchemaSnapshot is immutable and cannot be modified or deleted.");
        }
    }

}

internal sealed class SequenceValueRow
{
    public long Value { get; set; }
}

/// <summary>
/// Shared append-only enforcement used by DbContext overrides and the interceptor.
/// </summary>
internal static class AuditAppendOnlyGuard
{
    public static void EnsureAuditEntriesAreAppendOnly(DbContext context)
    {
        var invalidEntries = context.ChangeTracker
            .Entries<AuditLog>()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (invalidEntries.Any())
        {
            throw new InvalidOperationException("AuditLog is append-only and cannot be modified or deleted.");
        }
    }
}

internal static class NoteStatusHistoryAppendOnlyGuard
{
    public static void EnsureEntriesAreAppendOnly(DbContext context)
    {
        var invalidEntries = context.ChangeTracker
            .Entries<NoteStatusHistory>()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (invalidEntries.Any())
        {
            throw new InvalidOperationException("NoteStatusHistory is append-only and cannot be modified or deleted.");
        }
    }
}

internal static class CorrectiveActionStatusHistoryAppendOnlyGuard
{
    public static void EnsureEntriesAreAppendOnly(DbContext context)
    {
        var invalidEntries = context.ChangeTracker
            .Entries<CorrectiveActionStatusHistory>()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (invalidEntries.Any())
        {
            throw new InvalidOperationException("CorrectiveActionStatusHistory is append-only and cannot be modified or deleted.");
        }
    }
}

internal static class EscalationAppendOnlyGuard
{
    public static void EnsureEntriesAreAppendOnly(DbContext context)
    {
        var invalidOccurrences = context.ChangeTracker
            .Entries<EscalationOccurrence>()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        var invalidAttempts = context.ChangeTracker
            .Entries<NotificationDeliveryAttempt>()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (invalidOccurrences.Any())
        {
            throw new InvalidOperationException("EscalationOccurrence is append-only and cannot be modified or deleted.");
        }

        if (invalidAttempts.Any())
        {
            throw new InvalidOperationException("NotificationDeliveryAttempt is append-only and cannot be modified or deleted.");
        }
    }
}

internal static class NoteRoutingAppendOnlyGuard
{
    public static void EnsureEntriesAreAppendOnly(DbContext context)
    {
        var invalidDecisions = context.ChangeTracker
            .Entries<NoteRoutingDecision>()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        var invalidRuleHistory = context.ChangeTracker
            .Entries<NoteRoutingRuleHistory>()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        var invalidAccessHistory = context.ChangeTracker
            .Entries<NoteTypeAccessChangeHistory>()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (invalidDecisions.Any())
        {
            throw new InvalidOperationException("NoteRoutingDecision is append-only and cannot be modified or deleted.");
        }

        if (invalidRuleHistory.Any())
        {
            throw new InvalidOperationException("NoteRoutingRuleHistory is append-only and cannot be modified or deleted.");
        }

        if (invalidAccessHistory.Any())
        {
            throw new InvalidOperationException("NoteTypeAccessChangeHistory is append-only and cannot be modified or deleted.");
        }
    }
}

internal static class RiskStatusHistoryAppendOnlyGuard
{
    public static void EnsureEntriesAreAppendOnly(DbContext context)
    {
        var invalidEntries = context.ChangeTracker
            .Entries<RiskStatusHistory>()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (invalidEntries.Any())
        {
            throw new InvalidOperationException("RiskStatusHistory is append-only and cannot be modified or deleted.");
        }
    }
}

internal static class ResourceStatusEventAppendOnlyGuard
{
    public static void EnsureEntriesAreAppendOnly(DbContext context)
    {
        var invalidEntries = context.ChangeTracker
            .Entries<ResourceStatusEvent>()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (invalidEntries.Any())
        {
            throw new InvalidOperationException("ResourceStatusEvent is append-only and cannot be modified or deleted.");
        }
    }
}

public sealed class AuditImmutabilityInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            AuditAppendOnlyGuard.EnsureAuditEntriesAreAppendOnly(eventData.Context);
            NoteStatusHistoryAppendOnlyGuard.EnsureEntriesAreAppendOnly(eventData.Context);
            CorrectiveActionStatusHistoryAppendOnlyGuard.EnsureEntriesAreAppendOnly(eventData.Context);
            EscalationAppendOnlyGuard.EnsureEntriesAreAppendOnly(eventData.Context);
            NoteRoutingAppendOnlyGuard.EnsureEntriesAreAppendOnly(eventData.Context);
            ResourceStatusEventAppendOnlyGuard.EnsureEntriesAreAppendOnly(eventData.Context);
            FormSchemaSnapshotImmutabilityGuard.EnsureImmutable(eventData.Context);
            RiskStatusHistoryAppendOnlyGuard.EnsureEntriesAreAppendOnly(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            AuditAppendOnlyGuard.EnsureAuditEntriesAreAppendOnly(eventData.Context);
            NoteStatusHistoryAppendOnlyGuard.EnsureEntriesAreAppendOnly(eventData.Context);
            CorrectiveActionStatusHistoryAppendOnlyGuard.EnsureEntriesAreAppendOnly(eventData.Context);
            EscalationAppendOnlyGuard.EnsureEntriesAreAppendOnly(eventData.Context);
            NoteRoutingAppendOnlyGuard.EnsureEntriesAreAppendOnly(eventData.Context);
            ResourceStatusEventAppendOnlyGuard.EnsureEntriesAreAppendOnly(eventData.Context);
            FormSchemaSnapshotImmutabilityGuard.EnsureImmutable(eventData.Context);
            RiskStatusHistoryAppendOnlyGuard.EnsureEntriesAreAppendOnly(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
