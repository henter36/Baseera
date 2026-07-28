export type AuthMode = 'test' | 'entra'

export function getAuthMode(): AuthMode {
  const mode = import.meta.env.VITE_AUTH_MODE as AuthMode | undefined
  if (mode === 'test' || mode === 'entra') return mode
  // Production builds must set VITE_AUTH_MODE=entra explicitly via env.
  if (import.meta.env.PROD) {
    throw new Error('VITE_AUTH_MODE must be set to entra for production builds.')
  }
  // Dev-only fallback when Vite env file is incomplete.
  return 'entra'
}

export function isTestAuthAllowed(): boolean {
  return import.meta.env.DEV && getAuthMode() === 'test'
}

let accessTokenProvider: (() => Promise<string | null>) | null = null
let testSubject = ''

export function setAccessTokenProvider(provider: (() => Promise<string | null>) | null) {
  accessTokenProvider = provider
}

export function setTestSubject(subject: string) {
  if (!isTestAuthAllowed()) {
    throw new Error('TestAuth غير مسموح في هذا البناء.')
  }
  testSubject = subject
  sessionStorage.setItem('baseera.testSubject', subject)
}

export function getTestSubject() {
  if (!isTestAuthAllowed()) return ''
  return testSubject || sessionStorage.getItem('baseera.testSubject') || ''
}

export class ApiError extends Error {
  status: number
  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

const API_BASE = import.meta.env.VITE_API_BASE ?? ''

export type Me = {
  id: string
  displayNameAr: string
  email?: string | null
  permissions: string[]
  scopes: Array<{
    id: string
    scopeType: number
    regionId?: string | null
    facilityId?: string | null
    facilityUnitId?: string | null
    isActive: boolean
  }>
}

export type Paged<T> = {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

export type Region = {
  id: string
  code: string
  nameAr: string
  isActive: boolean
  createdAtUtc: string
  rowVersion: string
}

export type Facility = {
  id: string
  regionId: string
  code: string
  nameAr: string
  facilityType?: string | null
  isActive: boolean
  rowVersion: string
}

export type FacilityUnit = {
  id: string
  facilityId: string
  parentUnitId?: string | null
  code: string
  nameAr: string
  isActive: boolean
}

export type Department = {
  id: string
  organizationId: string
  parentDepartmentId?: string | null
  code: string
  nameAr: string
  isActive: boolean
}

export type User = {
  id: string
  externalSubject: string
  userName: string
  displayNameAr: string
  email?: string | null
  isActive: boolean
  roles: string[]
}

export type AuditLog = {
  id: string
  occurredAtUtc: string
  occurredAtSaudi: string
  userDisplayName?: string | null
  action: string
  module: string
  entityType: string
  entityId?: string | null
  outcome: string
  isSensitiveView: boolean
}

// Enums serialize as numbers (System.Text.Json default; Program.cs configures no
// JsonStringEnumConverter). Keep numeric values in sync with Baseera.Domain.Notes.

export type NoteListItem = {
  id: string
  referenceNumber: string
  title: string
  descriptionSnippet?: string | null
  status: number
  statusAr: string
  severity: number
  severityAr: string
  noteTypeId: string
  noteTypeCode: string
  noteTypeNameAr: string
  noteTypeIsActive: boolean
  classification: number
  scopeType: number
  regionId?: string | null
  facilityId?: string | null
  facilityUnitId?: string | null
  dueAtUtc?: string | null
  isOverdue: boolean
  currentAssigneeDisplay?: string | null
  createdAtUtc: string
  rowVersion: string
  isSensitiveRedacted: boolean
}

export type NoteAssignment = {
  id: string
  operationalNoteId: string
  assignedToUserId?: string | null
  assignedToUserDisplayName?: string | null
  assignedToDepartmentId?: string | null
  assignedToDepartmentName?: string | null
  assignedByUserId: string
  assignedByDisplayName?: string | null
  assignedAtUtc: string
  dueAtUtc?: string | null
  reason: string
  acceptedAtUtc?: string | null
  completedAtUtc?: string | null
  endedAtUtc?: string | null
  endReason?: string | null
  isCurrent: boolean
}

export type NoteDetail = {
  id: string
  referenceNumber: string
  title: string
  description: string
  status: number
  statusAr: string
  severity: number
  severityAr: string
  noteTypeId: string
  noteTypeCode: string
  noteTypeNameAr: string
  noteTypeDescriptionAr?: string | null
  noteTypeEntryInstructionsAr?: string | null
  noteTypeIsActive: boolean
  sourceType: number
  sourceAr: string
  sourceReference?: string | null
  classification: number
  scopeType: number
  regionId?: string | null
  facilityId?: string | null
  facilityUnitId?: string | null
  ownerDepartmentId?: string | null
  reportedByUserId: string
  reportedByDisplayName?: string | null
  reportedAtUtc: string
  dueAtUtc?: string | null
  isOverdue: boolean
  submittedAtUtc?: string | null
  workStartedAtUtc?: string | null
  submittedForVerificationAtUtc?: string | null
  closedAtUtc?: string | null
  closedByUserId?: string | null
  closureSummary?: string | null
  reopenedAtUtc?: string | null
  reopenReason?: string | null
  currentAssignment?: NoteAssignment | null
  createdAtUtc: string
  rowVersion: string
  isSensitiveRedacted: boolean
}

export type NoteStatusHistoryEntry = {
  id: string
  fromStatus?: number | null
  toStatus: number
  toStatusAr: string
  changedByUserId: string
  changedByDisplayName?: string | null
  changedAtUtc: string
  reason?: string | null
  assignmentId?: string | null
}

export type NoteWorkspaceSummary = {
  openCorrectiveActions: number
  attachmentCount: number
  waitingResource: boolean
  waitingVerification: boolean
  waitingClosureApproval: boolean
  hasEscalation: boolean
  progressPercent: number
  currentBlockerAr?: string | null
  lastUpdatedAtUtc: string
}

export type NoteWorkspaceTimelineEntry = {
  id: string
  type: string
  titleAr: string
  descriptionAr?: string | null
  actorDisplayName?: string | null
  occurredAtUtc: string
  tone: 'danger' | 'ok' | 'info' | 'muted' | 'warn'
}

export type NoteWorkspaceAllowedAction =
  | 'SUBMIT'
  | 'ASSIGN'
  | 'REASSIGN'
  | 'START_WORK'
  | 'ADD_ACTION'
  | 'REQUEST_VERIFICATION'
  | 'REJECT_VERIFICATION'
  | 'VERIFY_CLOSURE'
  | 'REOPEN'
  | 'CANCEL'

export type NoteWorkspaceList = {
  notes: Paged<NoteListItem>
}

// Resources/Decisions/Links were removed from the server contract (phase1a-observation-implementation-gap.md):
// they were always empty placeholder arrays backed by no real Domain entity.
export type NoteWorkspaceDetail = {
  note: NoteDetail
  allowedActions: NoteWorkspaceAllowedAction[]
  summary: NoteWorkspaceSummary
  assignments: NoteAssignment[]
  correctiveActions: Paged<CorrectiveActionListItem>
  attachments: Attachment[]
  timeline: NoteWorkspaceTimelineEntry[]
}

export type EligibleUser = {
  id: string
  displayNameAr: string
  userName: string
}

export type NoteListFilters = {
  page?: number
  pageSize?: number
  search?: string
  status?: number
  severity?: number
  noteTypeId?: string
  sourceType?: number
  classification?: number
  regionId?: string
  facilityId?: string
  facilityUnitId?: string
  ownerDepartmentId?: string
  assignedToUserId?: string
  overdueOnly?: boolean
  dueSoonDays?: number
  unassignedOnly?: boolean
  dueFrom?: string
  dueTo?: string
  createdFrom?: string
  createdTo?: string
  sortBy?: string
  sortDesc?: boolean
  requiresMyAction?: boolean
  requiresRouting?: boolean
}

export type CreateNoteRequest = {
  title: string
  description: string
  noteTypeId: string
  severity: number
  sourceType: number
  sourceReference?: string | null
  classification: number
  scopeType: number
  regionId?: string | null
  facilityId?: string | null
  facilityUnitId?: string | null
  ownerDepartmentId?: string | null
  dueAtUtc?: string | null
}

export type UpdateNoteRequest = {
  title: string
  description: string
  noteTypeId: string
  severity: number
  sourceType: number
  sourceReference?: string | null
  classification: number
  ownerDepartmentId?: string | null
  dueAtUtc?: string | null
  rowVersion: string
}

export type AssignNoteRequest = {
  assignedToUserId?: string | null
  assignedToDepartmentId?: string | null
  dueAtUtc?: string | null
  reason: string
  rowVersion: string
}

export type TransitionNoteRequest = {
  reason: string
  rowVersion: string
}

export type WorkflowActionRequest = {
  reason?: string | null
  rowVersion: string
}

export type CloseNoteRequest = {
  reason: string
  closureSummary: string
  rowVersion: string
}

export type ReopenNoteRequest = {
  reason: string
  rowVersion: string
}

export type NoteType = {
  id: string
  code: string
  nameAr: string
  descriptionAr?: string | null
  entryInstructionsAr?: string | null
  sortOrder: number
  isActive: boolean
  defaultSeverity: number
  defaultSeverityAr: string
  defaultDueDays?: number | null
  rowVersion: string
}

export type NoteIntakeContext = {
  lockType: number
  lockedRegionId?: string | null
  lockedRegionNameAr?: string | null
  lockedFacilityId?: string | null
  lockedFacilityNameAr?: string | null
  regions: Array<{ id: string; nameAr: string }>
  creatableNoteTypes: NoteType[]
}

export type CorrectiveActionListItem = {
  id: string
  referenceNumber: string
  operationalNoteId: string
  operationalNoteReferenceNumber?: string | null
  title: string
  descriptionSnippet?: string | null
  priority: number
  priorityAr: string
  status: number
  statusAr: string
  classification: number
  ownerDepartmentId?: string | null
  dueAtUtc?: string | null
  isOverdue: boolean
  isDueSoon: boolean
  overdueDays?: number | null
  currentAssigneeDisplay?: string | null
  createdAtUtc: string
  rowVersion: string
  isSensitiveRedacted: boolean
}

export type CorrectiveActionAssignment = {
  id: string
  correctiveActionId: string
  assignedToUserId?: string | null
  assignedToUserDisplayName?: string | null
  assignedToDepartmentId?: string | null
  assignedToDepartmentName?: string | null
  assignedByUserId: string
  assignedByDisplayName?: string | null
  assignedAtUtc: string
  dueAtUtc?: string | null
  reason: string
  acceptedAtUtc?: string | null
  completedAtUtc?: string | null
  endedAtUtc?: string | null
  endReason?: string | null
  isCurrent: boolean
}

export type CorrectiveActionDetail = CorrectiveActionListItem & {
  description: string
  createdByUserId: string
  createdByDisplayName?: string | null
  submittedAtUtc?: string | null
  workStartedAtUtc?: string | null
  submittedForVerificationAtUtc?: string | null
  completedAtUtc?: string | null
  completedByUserId?: string | null
  completionSummary?: string | null
  reopenedAtUtc?: string | null
  reopenReason?: string | null
  cancelledAtUtc?: string | null
  cancelReason?: string | null
  currentAssignment?: CorrectiveActionAssignment | null
}

export type CorrectiveActionStatusHistoryEntry = {
  id: string
  fromStatus?: number | null
  toStatus: number
  toStatusAr: string
  changedByUserId: string
  changedByDisplayName?: string | null
  changedAtUtc: string
  reason?: string | null
  assignmentId?: string | null
  metadataJson?: string | null
}

export type CorrectiveActionListFilters = {
  page?: number
  pageSize?: number
  search?: string
  noteId?: string
  status?: number
  priority?: number
  classification?: number
  ownerDepartmentId?: string
  assignedToUserId?: string
  regionId?: string
  facilityId?: string
  facilityUnitId?: string
  overdueOnly?: boolean
  dueSoonDays?: number
  dueFrom?: string
  dueTo?: string
  createdFrom?: string
  createdTo?: string
  sortBy?: string
  sortDesc?: boolean
}

export type CreateCorrectiveActionRequest = {
  title: string
  description: string
  priority: number
  classification?: number | null
  ownerDepartmentId?: string | null
  dueAtUtc?: string | null
}

export type UpdateCorrectiveActionRequest = CreateCorrectiveActionRequest & {
  classification: number
  rowVersion: string
}

export type CompleteCorrectiveActionRequest = { reason: string; completionSummary: string; rowVersion: string }

export type RowVersionRequest = { rowVersion: string }

export type EscalationPolicy = {
  id: string
  code: string
  nameAr: string
  description?: string | null
  targetType: number
  isEnabled: boolean
  scopeType: number
  regionId?: string | null
  facilityId?: string | null
  facilityUnitId?: string | null
  ruleCount: number
  rowVersion: string
}

export type EscalationRule = {
  id: string
  escalationPolicyId: string
  level: number
  priority: number
  triggerType: number
  thresholdDays: number
  repeatEveryDays?: number | null
  maximumOccurrences?: number | null
  recipientStrategy: number
  recipientRoleCode?: string | null
  specificRecipientUserId?: string | null
  titleTemplateAr: string
  messageTemplateAr: string
  isEnabled: boolean
  rowVersion: string
}

export type CreateEscalationPolicyRequest = {
  code: string
  nameAr: string
  description?: string | null
  targetType: number
  scopeType: number
  regionId?: string | null
  facilityId?: string | null
  facilityUnitId?: string | null
}

export type UpdateEscalationPolicyRequest = Omit<CreateEscalationPolicyRequest, 'code' | 'targetType'> & {
  rowVersion: string
}

export type CreateEscalationRuleRequest = {
  level: number
  priority: number
  triggerType: number
  thresholdDays: number
  repeatEveryDays?: number | null
  maximumOccurrences?: number | null
  recipientStrategy: number
  recipientRoleCode?: string | null
  specificRecipientUserId?: string | null
  titleTemplateAr: string
  messageTemplateAr: string
}

export type UpdateEscalationRuleRequest = Omit<CreateEscalationRuleRequest, 'level'> & {
  rowVersion: string
}

export type EscalationOccurrence = {
  id: string
  policyId: string
  ruleId: string
  targetType: number
  targetId: string
  targetReferenceNumber: string
  escalationLevel: number
  triggerType: number
  occurrenceNumber: number
  dueAtUtc: string
  detectedAtUtc: string
  recipientCount: number
  status: number
  suppressionReason?: string | null
}

export type EscalationRunResult = {
  policiesEvaluated: number
  candidatesEvaluated: number
  occurrencesCreated: number
  notificationsCreated: number
  suppressed: number
  failed: number
}

export type NoteRoutingRule = {
  id: string
  code: string
  nameAr: string
  descriptionAr?: string | null
  noteTypeId: string
  noteTypeNameAr?: string | null
  scopeType: number
  regionId?: string | null
  facilityId?: string | null
  facilityUnitId?: string | null
  priority: number
  processingTargetType: number
  processingDepartmentId?: string | null
  processingDepartmentNameAr?: string | null
  processingRoleId?: string | null
  processingRoleNameAr?: string | null
  reviewerRoleId?: string | null
  reviewerRoleNameAr?: string | null
  defaultDueDays?: number | null
  autoAssignOnSubmit: boolean
  autoReassignOnReopen: boolean
  isActive: boolean
  rowVersion: string
}

export type NoteRoutingRuleRequest = {
  code: string
  nameAr: string
  descriptionAr?: string | null
  noteTypeId: string
  scopeType: number
  regionId?: string | null
  facilityId?: string | null
  facilityUnitId?: string | null
  priority: number
  processingTargetType: number
  processingDepartmentId?: string | null
  processingRoleId?: string | null
  reviewerRoleId?: string | null
  defaultDueDays?: number | null
  autoAssignOnSubmit: boolean
  autoReassignOnReopen: boolean
  reason: string
}

export type UpdateNoteRoutingRuleRequest = Omit<NoteRoutingRuleRequest, 'code'> & {
  rowVersion: string
}

export type NoteRoutingPreview = {
  winningRule?: NoteRoutingRule | null
  reason: string
  specificity: number
  expectedTarget: string
  eligibleUserCount: number
  expectedUserId?: string | null
  reviewerRoleId?: string | null
  dueAtUtc?: string | null
  warnings: string[]
}

export type NoteRoutingEffectiveness = {
  totalAttempts: number
  autoAssignmentSuccessRate: number
  assignedToDepartment: number
  assignedToUser: number
  noMatchingRule: number
  noEligibleUser: number
  invalidTarget: number
  manualOverride: number
  requiresRoutingCount: number
}

export type DashboardOperationsFilters = {
  periodDays?: number
  fromUtc?: string
  toUtc?: string
  regionId?: string
  facilityId?: string
  facilityUnitId?: string
  noteTypeId?: string
  severity?: number
  status?: number
  breakdownBy?: number
  queue?: number
}

export type FormComplianceFilters = {
  fromUtc?: string
  toUtc?: string
  formDefinitionId?: string
  campaignId?: string
  cycleId?: string
  regionId?: string
  facilityId?: string
  cycleStatus?: number
  completionBasis?: number
  responseStatus?: number
  isCompleted?: boolean
  isOverdue?: boolean
  isAvailable?: boolean
  search?: string
  sort?: string
  page?: number
  pageSize?: number
  groupBy?: number
  view?: number
}

export type FormComplianceSummary = {
  targetedAssignmentCount: number
  distinctFacilityCount: number
  unavailableAssignmentCount: number
  eligibleAssignmentCount: number
  completedCount: number
  remainingCount: number
  completionRate?: number | null
  notStartedCount: number
  draftCount: number
  submittedCount: number
  underReviewCount: number
  returnedCount: number
  approvedCount: number
  rejectedCount: number
  closedCount: number
  overdueCount: number
  completedOnTimeCount: number
  completedLateCount: number
  averageCompletionMinutes?: number | null
  unknownCompletionTimestampCount: number
  invalidCompletionDurationCount: number
  statusBucketTotal: number
  statusReconciliationValid: boolean
  generatedAtUtc: string
}

export type FormComplianceRegionRow = {
  regionIdAtAssignment: string
  regionNameAtAssignment: string
  targetedAssignmentCount: number
  unavailableAssignmentCount: number
  eligibleAssignmentCount: number
  completedCount: number
  remainingCount: number
  completionRate?: number | null
  overdueCount: number
  notStartedCount: number
  returnedCount: number
  averageCompletionMinutes?: number | null
  rank: number
}

export type FormComplianceFacilityRow = {
  facilityId: string
  facilityCodeAtAssignment: string
  facilityNameAtAssignment: string
  regionIdAtAssignment: string
  regionNameAtAssignment: string
  cycleCount: number
  eligibleAssignmentCount: number
  completedCount: number
  remainingCount: number
  completionRate?: number | null
  overdueCount: number
  latestEffectiveDueAtUtc?: string | null
  responsibleUserId?: string | null
  responsibleUserName?: string | null
  allowedActions: string[]
}

export type FormComplianceCycleRow = {
  cycleId: string
  campaignId: string
  campaignCode: string
  campaignNameAr: string
  sequenceNumber: number
  occurrenceKey: string
  scheduledOccurrenceUtc: string
  openAtUtc: string
  dueAtUtc: string
  closeAtUtc: string
  cycleStatus: number
  completionBasis: number
  targetedAssignmentCount: number
  eligibleAssignmentCount: number
  completedCount: number
  remainingCount: number
  completionRate?: number | null
  overdueCount: number
  averageCompletionMinutes?: number | null
  previousCycleCompletionRate?: number | null
  completionRateDelta?: number | null
}

export type FormCompliancePendingItem = {
  assignmentId: string
  campaignId: string
  campaignNameAr: string
  cycleId: string
  occurrenceKey: string
  facilityId: string
  facilityNameAtAssignment: string
  regionIdAtAssignment: string
  regionNameAtAssignment: string
  responseId?: string | null
  responseStatus?: number | null
  workStatus: number
  isOverdue: boolean
  openAtUtc: string
  effectiveDueAtUtc: string
  daysOverdue?: number | null
  lastSavedAtUtc?: string | null
  submittedAtUtc?: string | null
  responsibleUserId?: string | null
  responsibleUserName?: string | null
  allowedActions: string[]
}

export type FormComplianceTrendPoint = {
  occurrenceUtc?: string | null
  dateLocal?: string | null
  eligibleAssignmentCount: number
  completedCount: number
  completionRate?: number | null
  overdueCount: number
  averageCompletionMinutes?: number | null
  completedThatDay?: number | null
  cumulativeCompleted?: number | null
  cumulativeCompletionRate?: number | null
}

export type DashboardWorkloadSummary = {
  openTotal: number
  assigned: number
  inProgress: number
  pendingVerification: number
  reopened: number
  unassigned: number
  requiresRouting: number
}

export type DashboardRiskSummary = {
  overdue: number
  dueSoon: number
  criticalOrHigh: number
  overdueUnassigned: number
  activeEscalations: number
  routingFailureNoRule: number
  routingFailureNoEligibleUser: number
  routingFailureInvalidTarget: number
}

export type DashboardCorrectiveActionsSummary = {
  active: number
  overdue: number
  pendingVerification: number
  reopened: number
  notesWithStalledActions: number
}

export type DashboardRoutingSummary = {
  requiresRouting: number
  failureNoRule: number
  failureNoEligibleUser: number
  failureInvalidTarget: number
}

export type DashboardOperationsSummary = {
  workload?: DashboardWorkloadSummary | null
  risk?: DashboardRiskSummary | null
  correctiveActions?: DashboardCorrectiveActionsSummary | null
  routing?: DashboardRoutingSummary | null
  fromUtc: string
  toUtc: string
  dueSoonDays: number
}

export type WorkspaceLevel = 1 | 2 | 3 | 4
export type DataFreshnessStatus = 1 | 2 | 3 | 4 | 5
export type ConfidenceLevel = 1 | 2 | 3 | 4
export type WorkspaceWidgetSize = 1 | 2 | 3 | 4

export type DataFreshness = {
  status: DataFreshnessStatus
  labelAr: string
  reasonAr?: string | null
}

export type WorkspaceConfidence = {
  level: ConfidenceLevel
  labelAr: string
  reasonAr?: string | null
}

export type WorkspaceAllowedAction = {
  code: string
  labelAr: string
  enabled: boolean
  disabledReasonAr?: string | null
  requiresConfirmation: boolean
  target?: { kind: string; routeKey?: string | null; routeParameters: Record<string, string> } | null
}

export type WorkspaceDrillDownTarget = {
  routeKey: string
  labelAr: string
  routeParameters: Record<string, string>
  preservedFilters: Record<string, string>
  requiredPermission: string
}

export type WorkspaceScopeSummary = {
  level: WorkspaceLevel
  labelAr: string
  regionId?: string | null
  facilityId?: string | null
  isSensitive: boolean
}

export type WorkspaceWidgetDefinition = {
  key: string
  titleAr: string
  titleEn: string
  descriptionAr?: string | null
  category: number
  supportedLevels: WorkspaceLevel[]
  requiredPermission?: string | null
  requiredDataCapability?: string | null
  defaultSize: WorkspaceWidgetSize
  minSize: WorkspaceWidgetSize
  maxSize: WorkspaceWidgetSize
  refreshPolicy: { minimumRefreshSeconds: number; supportsManualRefresh: boolean }
  dataFreshnessPolicy: { currentForSeconds: number; delayedAfterSeconds: number; staleAfterSeconds: number }
  emptyErrorBehavior: { emptyMessageAr: string; errorMessageAr: string; allowPartialFailure: boolean }
  supportsDrillDown: boolean
  isConfigurable: boolean
  containsSensitiveData: boolean
  isEnabled: boolean
}

export type WorkspaceDefinition = {
  key: string
  titleAr: string
  titleEn: string
  supportedLevels: WorkspaceLevel[]
  requiredPermissions: string[]
  registeredWidgets: string[]
  defaultLayout: { items: Array<{ widgetKey: string; order: number; size: WorkspaceWidgetSize; isPinned: boolean }>; version: number }
  availableFilters: Array<{ key: string; labelAr: string; type: string; isServerSide: boolean }>
  supportedDrillDowns: Array<{ routeKey: string; labelAr: string; requiredPermission: string }>
  features: { supportsSavedViews: boolean; supportsWidgetConfiguration: boolean; supportsExport: boolean; isReferenceOnly: boolean }
  version: number
}

export type WorkspaceContext = {
  workspaceKey: string
  level: WorkspaceLevel
  organizationId?: string | null
  regionId?: string | null
  facilityId?: string | null
  entityId?: string | null
  scopeLabelAr: string
  fromUtc: string
  toUtc: string
  locale: string
  timeZone: string
  includesSensitiveData: boolean
}

export type ReferenceOperationalSummaryPayload = {
  openNotes: number
  inProgressNotes: number
  pendingVerificationNotes: number
  unassignedNotes: number
  requiresRouting: number
  overdueNotes: number
  dueSoonNotes: number
  criticalOrHighNotes: number
}

export type ReferenceCorrectiveActionsPayload = {
  activeActions: number
  overdueActions: number
  pendingVerificationActions: number
  reopenedActions: number
  notesWithStalledActions: number
}

export type FacilityHeaderPayload = {
  facilityId: string
  facilityNameAr: string
  regionId: string
  regionNameAr: string
  facilityType?: string | null
  fromUtc: string
  toUtc: string
  calculatedAtUtc: string
}

export type FacilityExecutiveSummaryPayload = {
  statusCode: string
  statusAr: string
  priorityIssues: number
  topDriverAr: string
  changeSummaryAr: string
  topPendingActionAr: string
  confidenceReasons: string[]
  calculatedAtUtc: string
}

export type FacilityNotesOverviewPayload = {
  openNotes: number
  criticalNotes: number
  overdueNotes: number
  unassignedNotes: number
  requiresMyAction: number
  newInPeriod: number
  topNoteTypes: Array<{ labelAr: string; count: number }>
}

export type FacilityCorrectiveActionsPayload = {
  openActions: number
  overdueActions: number
  inProgressActions: number
  pendingVerificationActions: number
  reopenedActions: number
  criticalActions: number
  averageClosureHours?: number | null
}

export type WorkspaceVisualTone =
  | 'danger'
  | 'ok'
  | 'info'
  | 'muted'
  | 'warn'

export type FacilityAlertsEscalationsPayload = {
  personalUnreadNotifications: number
  openEscalations: number
  criticalEscalations: number
  overdueAlerts: number
  lastEscalationProcessedAtUtc?: string | null
}

export type FacilityFormCompliancePayload = {
  targetedForms: number
  completedForms: number
  remainingForms: number
  overdueForms: number
  completionRate?: number | null
  nearestDueAtUtc?: string | null
  notStartedForms: number
  pendingReviewForms: number
}

export type OccupancySummaryPayload = {
  facilityId: string
  approvedCapacity?: number | null
  currentCount?: number | null
  occupancyRate?: number | null
  availablePlaces?: number | null
  overCapacityCount?: number | null
  statusCode: string
  statusAr: string
  unitCount: number
  overloadedUnits: number
  emptyUnits: number
  latestSnapshotAtUtc?: string | null
  sourceCode: string
  sourceAr: string
  freshnessStatus: string
  confidenceLevel: string
  isPartial: boolean
  warnings: string[]
}

export type OccupancyUnitPayload = {
  unitId: string
  unitNameAr: string
  unitCode: string
  approvedCapacity?: number | null
  currentCount?: number | null
  occupancyRate?: number | null
  availablePlaces?: number | null
  overloadCount?: number | null
  statusCode: string
  statusAr: string
  lastUpdatedAtUtc?: string | null
  dataSourceAr: string
  openNotesCount: number
  openIncidentsCount: number
  riskCount: number
  alertReasons: string[]
}

export type OccupancyMovementSummaryPayload = {
  admissions: number
  releases: number
  transferIn: number
  transferOut: number
  internalTransfers: number
  temporaryLeave: number
  returns: number
  death: number
  hospitalTransfers: number
  courtTransfers: number
  corrections: number
  otherMovements: number
  netMovement: number
  dailyTrend: Array<{
    date: string
    admissions: number
    releases: number
    transfersIn: number
    transfersOut: number
    net: number
  }>
}

export const OccupancyCapacityType = {
  ApprovedOperational: 0,
} as const
export type OccupancyCapacityType = (typeof OccupancyCapacityType)[keyof typeof OccupancyCapacityType]

export const OccupancySourceType = {
  Manual: 0,
  ExternalSystem: 1,
  Import: 2,
  Reconciliation: 3,
} as const
export type OccupancySourceType = (typeof OccupancySourceType)[keyof typeof OccupancySourceType]

export const CensusQualityStatus = {
  Complete: 0,
  Partial: 1,
  Stale: 2,
  Missing: 3,
  Conflicting: 4,
} as const
export type CensusQualityStatus = (typeof CensusQualityStatus)[keyof typeof CensusQualityStatus]

export const OccupancyMovementType = {
  Admission: 0,
  Release: 1,
  TransferIn: 2,
  TransferOut: 3,
  InternalTransfer: 4,
  TemporaryLeave: 5,
  ReturnFromLeave: 6,
  HospitalTransfer: 7,
  CourtTransfer: 8,
  Death: 9,
  Correction: 10,
  Other: 99,
} as const
export type OccupancyMovementType = (typeof OccupancyMovementType)[keyof typeof OccupancyMovementType]

export type OccupancyCapacityRequest = {
  facilityUnitId?: string | null
  capacityType: OccupancyCapacityType
  approvedCapacity: number
  effectiveFromUtc: string
  sourceType: OccupancySourceType
  sourceReference: string
}

export type OccupancySnapshotRequest = {
  facilityUnitId?: string | null
  capturedAtUtc: string
  inmateCount: number
  sourceType: OccupancySourceType
  sourceReference: string
  isAuthoritative: boolean
  qualityStatus: CensusQualityStatus
}

export type OccupancyMovementImportRow = {
  inmateReferenceHash: string
  movementType: OccupancyMovementType
  fromFacilityId?: string
  toFacilityId?: string
  fromFacilityUnitId?: string
  toFacilityUnitId?: string
  occurredAtUtc: string
  externalEventId: string
}

export type OccupancyMovementImportRequest = {
  sourceSystem: string
  importReference: string
  rows: OccupancyMovementImportRow[]
}

export type OccupancyImportResult = {
  acceptedRows: number
  duplicateRows: number
  rejectedRows: string[]
}

export type OccupancyWorkspacePayload = {
  summary: OccupancySummaryPayload
  unitBreakdown: {
    units: OccupancyUnitPayload[]
  }
  movementSummary: OccupancyMovementSummaryPayload
  interventions: Array<{
    type: string
    reference: string
    titleAr: string
    severityAr: string
    priorityRank: number
    reasonAr: string
    actionLabelAr: string
    unitId?: string | null
    dueAtUtc?: string | null
  }>
}

export const ResourceType = {
  Vehicle: 0,
  CommunicationDevice: 1,
  OperationalEquipment: 2,
  SecurityEquipment: 3,
  FacilityAsset: 4,
} as const
export type ResourceType = (typeof ResourceType)[keyof typeof ResourceType]

export const ResourceStatus = {
  Available: 0,
  InUse: 1,
  Standby: 2,
  Reserved: 3,
  UnderInspection: 4,
  UnderMaintenance: 5,
  OutOfService: 6,
  AwaitingParts: 7,
  Lost: 8,
  Transferred: 9,
  Retired: 10,
  Unknown: 11,
} as const
export type ResourceStatus = (typeof ResourceStatus)[keyof typeof ResourceStatus]

export const ResourceCondition = {
  Excellent: 0,
  Good: 1,
  Fair: 2,
  Poor: 3,
  Unserviceable: 4,
  Unknown: 5,
} as const
export type ResourceCondition = (typeof ResourceCondition)[keyof typeof ResourceCondition]

export const ResourceCriticality = {
  Low: 0,
  Medium: 1,
  High: 2,
  MissionCritical: 3,
} as const
export type ResourceCriticality = (typeof ResourceCriticality)[keyof typeof ResourceCriticality]

export const ResourceSourceType = {
  Manual: 0,
  Import: 1,
  ExternalSystem: 2,
  Audit: 3,
  Other: 4,
} as const
export type ResourceSourceType = (typeof ResourceSourceType)[keyof typeof ResourceSourceType]

export type ResourceSummaryPayload = {
  facilityId: string
  totalRegistered: number
  operational: number
  available: number
  standby: number
  inUse: number
  underMaintenance: number
  outOfService: number
  awaitingParts: number
  unknown: number
  retired: number
  required: number
  gap: number
  surplus: number
  readinessRate?: number | null
  availabilityRate?: number | null
  dataCompletenessRate: number
  missionCriticalUnavailable: number
  staleRecords: number
  missingDataRecords: number
  freshnessStatus: string
  confidenceLevel: string
  isPartial: boolean
  warnings: string[]
  generatedAtUtc: string
  dataEffectiveAtUtc?: string | null
}

export type ResourceCategoryReadinessPayload = {
  resourceType: ResourceType
  resourceTypeCode: string
  labelAr: string
  total: number
  operational: number
  available: number
  underMaintenance: number
  outOfService: number
  awaitingParts: number
  required: number
  gap: number
  readinessRate?: number | null
  freshnessStatus: string
  confidenceLevel: string
}

export type ResourceExceptionPayload = {
  type: string
  resourceAssetId?: string | null
  resourceType?: ResourceType | null
  reference: string
  titleAr: string
  severityAr: string
  priorityRank: number
  reasonAr: string
  ownerAr?: string | null
  dueAtUtc?: string | null
  actionLabelAr: string
}

export type ResourceUnitDistributionPayload = {
  facilityUnitId?: string | null
  unitNameAr: string
  vehicles: number
  communicationDevices: number
  equipment: number
  facilityAssets: number
  readinessRate?: number | null
  gap: number
  criticalExceptions: number
}

export type ResourceActivityPayload = {
  eventType: string
  titleAr: string
  descriptionAr?: string | null
  occurredAtUtc: string
  entityReference: string
  tone: WorkspaceVisualTone
  resourceAssetId?: string | null
}

export type ResourceWorkspacePayload = {
  summary: ResourceSummaryPayload
  categories: ResourceCategoryReadinessPayload[]
  exceptions: ResourceExceptionPayload[]
  unitDistribution: ResourceUnitDistributionPayload[]
  timeline: ResourceActivityPayload[]
}

export const EmploymentStatus = {
  Active: 0,
  SecondedIn: 1,
  SecondedOut: 2,
  Suspended: 3,
  LongLeave: 4,
  Retired: 5,
  Terminated: 6,
  Unknown: 7,
} as const
export type EmploymentStatus = (typeof EmploymentStatus)[keyof typeof EmploymentStatus]

export const WorkforceRoleCategory = {
  Command: 0,
  Security: 1,
  Control: 2,
  Escort: 3,
  Medical: 4,
  Social: 5,
  Technical: 6,
  Logistics: 7,
  Administrative: 8,
  Other: 9,
} as const
export type WorkforceRoleCategory = (typeof WorkforceRoleCategory)[keyof typeof WorkforceRoleCategory]

export const WorkforceRoleCriticality = {
  Low: 0,
  Medium: 1,
  High: 2,
  MissionCritical: 3,
} as const
export type WorkforceRoleCriticality = (typeof WorkforceRoleCriticality)[keyof typeof WorkforceRoleCriticality]

export const QualificationType = {
  RoleCertification: 0,
  Skill: 1,
  License: 2,
  SecurityClearance: 3,
  FitnessClearance: 4,
  Other: 5,
} as const
export type QualificationType = (typeof QualificationType)[keyof typeof QualificationType]

export const QualificationStatus = {
  Valid: 0,
  ExpiringSoon: 1,
  Expired: 2,
  Suspended: 3,
  PendingVerification: 4,
  Unknown: 5,
} as const
export type QualificationStatus = (typeof QualificationStatus)[keyof typeof QualificationStatus]

export const AssignmentType = {
  Permanent: 0,
  Temporary: 1,
  Acting: 2,
  EmergencySupport: 3,
  Secondment: 4,
  TrainingCoverage: 5,
  Other: 6,
} as const
export type AssignmentType = (typeof AssignmentType)[keyof typeof AssignmentType]

export const AvailabilityType = {
  Available: 0,
  AnnualLeave: 1,
  SickLeave: 2,
  Training: 3,
  ExternalAssignment: 4,
  InternalAssignment: 5,
  Suspended: 6,
  RestrictedDuty: 7,
  EmergencyLeave: 8,
  UnexcusedAbsence: 9,
  Other: 10,
} as const
export type AvailabilityType = (typeof AvailabilityType)[keyof typeof AvailabilityType]

export const OperationalRestrictionCode = {
  CannotDrive: 0,
  CannotCarryWeapon: 1,
  CannotWorkNightShift: 2,
  CannotPerformEscort: 3,
  AdministrativeDutyOnly: 4,
} as const
export type OperationalRestrictionCode = (typeof OperationalRestrictionCode)[keyof typeof OperationalRestrictionCode]

export const RosterAssignmentStatus = {
  Planned: 0,
  Confirmed: 1,
  Present: 2,
  Late: 3,
  Absent: 4,
  Excused: 5,
  Replaced: 6,
  Completed: 7,
  Cancelled: 8,
  Unknown: 9,
} as const
export type RosterAssignmentStatus = (typeof RosterAssignmentStatus)[keyof typeof RosterAssignmentStatus]

export const WorkforceSourceType = {
  Manual: 0,
  Import: 1,
  ExternalSystem: 2,
  Audit: 3,
  Other: 4,
} as const
export type WorkforceSourceType = (typeof WorkforceSourceType)[keyof typeof WorkforceSourceType]

export const WorkforceCoverageStatus = {
  Ready: 0,
  Attention: 1,
  Critical: 2,
  Unsafe: 3,
  Unknown: 4,
} as const
export type WorkforceCoverageStatus = (typeof WorkforceCoverageStatus)[keyof typeof WorkforceCoverageStatus]

export type WorkforceSummaryPayload = {
  facilityId: string
  totalMembers: number
  operationallyEligible: number
  required: number
  minimumSafe: number
  scheduled: number
  present: number
  operationallyAvailable: number
  onLeave: number
  inTraining: number
  restricted: number
  gap: number
  safeGap: number
  coverageRate?: number | null
  qualificationCoverage?: number | null
  coverageStatus: WorkforceCoverageStatus
  criticalPositionsAtRisk: number
  staleRecords: number
  missingDataRecords: number
  freshnessStatus: string
  confidenceLevel: string
  isPartial: boolean
  warnings: string[]
  fatigueIndicators: string[]
  generatedAtUtc: string
  dataEffectiveAtUtc?: string | null
}

export type WorkforceCoverageRowPayload = {
  roleDefinitionId: string
  roleCode: string
  roleNameAr: string
  facilityUnitId?: string | null
  unitNameAr?: string | null
  shiftDefinitionId?: string | null
  shiftCode?: string | null
  required: number
  minimumSafe: number
  scheduled: number
  present: number
  operationallyAvailable: number
  gap: number
  safeGap: number
  coverageRate?: number | null
  coverageStatus: WorkforceCoverageStatus
}

export type WorkforceUnitCoveragePayload = {
  facilityUnitId?: string | null
  unitNameAr: string
  required: number
  operationallyAvailable: number
  gap: number
  coverageRate?: number | null
  coverageStatus: WorkforceCoverageStatus
}

export type WorkforceRoleDefinitionPayload = {
  id: string
  code: string
  nameAr: string
  nameEn?: string | null
  category: WorkforceRoleCategory
  criticality: WorkforceRoleCriticality
  requiresCertification: boolean
  isShiftBased: boolean
  isSensitive: boolean
}

export type WorkforceMemberListItem = {
  id: string
  employeeNumber: string
  displayName: string
  employmentStatus: EmploymentStatus
  jobTitle: string
  primarySpecialty: string
  currentOperationalUnitId?: string | null
  currentOperationalUnitNameAr?: string | null
  isOperational: boolean
  isSensitiveRole: boolean
  lastVerifiedAtUtc?: string | null
  rowVersion?: string | null
  dataQualityIssues: string[]
}

export type WorkforceAssignmentPayload = {
  id: string
  roleDefinitionId: string
  roleCode: string
  roleNameAr: string
  facilityUnitId?: string | null
  assignmentType: AssignmentType
  effectiveFromUtc: string
  effectiveToUtc?: string | null
  isPrimary: boolean
}

export type WorkforceQualificationPayload = {
  id: string
  qualificationType: QualificationType
  roleDefinitionId?: string | null
  name: string
  expiresAtUtc?: string | null
  status: QualificationStatus
}

export type WorkforceQualificationListItem = WorkforceQualificationPayload & {
  memberId: string
  memberDisplayName: string
  roleCode?: string | null
}

export type WorkforceQualificationList = {
  items: WorkforceQualificationListItem[]
  totalCount: number
  page: number
  pageSize: number
}

export type WorkforceAvailabilityPayload = {
  id: string
  availabilityType: AvailabilityType
  startsAtUtc: string
  endsAtUtc?: string | null
  affectsOperationalAvailability: boolean
  restrictionCodes?: string[] | null
}

export type WorkforceMemberDetail = {
  member: WorkforceMemberListItem
  assignments: WorkforceAssignmentPayload[]
  qualifications: WorkforceQualificationPayload[]
  availability: WorkforceAvailabilityPayload[]
  restrictionCodes?: string[] | null
}

export type WorkforceDataQualityPayload = {
  totalMembers: number
  missingEmployeeNumber: number
  unknownEmploymentStatus: number
  missingHomeOrOperationalFacility: number
  missingOperationalFacility: number
  staleVerification: number
  openImportIssues: number
  warnings: string[]
}

export type WorkforceWorkspacePayload = {
  summary: WorkforceSummaryPayload
  coverage: WorkforceCoverageRowPayload[]
  units: WorkforceUnitCoveragePayload[]
  roles: WorkforceRoleDefinitionPayload[]
  dataQuality: WorkforceDataQualityPayload
}

export type WorkforceMemberCreateRequest = {
  displayName: string
  employeeNumber: string
  externalPersonnelId?: string | null
  employmentStatus?: EmploymentStatus
  rankOrGrade?: string | null
  jobTitle: string
  primarySpecialty: string
  homeFacilityId?: string | null
  currentOperationalUnitId?: string | null
  supervisorWorkforceMemberId?: string | null
  isOperational?: boolean
  isSensitiveRole?: boolean
  sourceType?: WorkforceSourceType
  sourceReference?: string | null
}

export type WorkforceAssignmentRequest = {
  workforceMemberId: string
  roleDefinitionId: string
  facilityUnitId?: string | null
  assignmentType?: AssignmentType
  effectiveFromUtc: string
  effectiveToUtc?: string | null
  isPrimary?: boolean
  sourceReference?: string | null
  reason?: string | null
}

export type WorkforceQualificationRequest = {
  workforceMemberId: string
  qualificationType: QualificationType
  roleDefinitionId?: string | null
  name: string
  issuedAtUtc?: string | null
  expiresAtUtc?: string | null
  issuer?: string | null
  reference?: string | null
  status?: QualificationStatus
}

export type StaffingRequirementRequest = {
  facilityUnitId?: string | null
  roleDefinitionId: string
  shiftDefinitionId?: string | null
  requiredHeadcount: number
  minimumSafeHeadcount: number
  effectiveFromUtc: string
  effectiveToUtc?: string | null
  sourceReference: string
  approvalReference?: string | null
  notes?: string | null
}

export type StaffingRequirementPayload = {
  id: string
  facilityUnitId?: string | null
  roleDefinitionId: string
  roleCode?: string | null
  shiftDefinitionId?: string | null
  requiredHeadcount: number
  minimumSafeHeadcount: number
  effectiveFromUtc: string
  effectiveToUtc?: string | null
  sourceReference: string
}

export type DutyRosterCreateRequest = {
  facilityUnitId?: string | null
  shiftDefinitionId: string
  dutyDate: string
}

export type DutyRosterAssignmentRequest = {
  workforceMemberId: string
  roleDefinitionId: string
  status?: RosterAssignmentStatus
  replacementForAssignmentId?: string | null
  notes?: string | null
}

export type DutyRosterPayload = {
  id: string
  facilityUnitId?: string | null
  shiftDefinitionId: string
  dutyDate: string
  status: string
  publishedAtUtc?: string | null
  assignmentCount: number
}

export type WorkforceAvailabilityRequest = {
  workforceMemberId: string
  availabilityType: AvailabilityType
  startsAtUtc: string
  endsAtUtc?: string | null
  affectsOperationalAvailability?: boolean
  sourceType?: WorkforceSourceType
  sourceReference?: string | null
  reasonCode?: string | null
  restrictionCodes?: OperationalRestrictionCode[] | null
}

export const WorkforceImportKind = {
  PersonnelMaster: 0,
  Assignments: 1,
  Qualifications: 2,
  Rosters: 3,
  Availability: 4,
  AttendanceSummary: 5,
} as const
export type WorkforceImportKind = (typeof WorkforceImportKind)[keyof typeof WorkforceImportKind]

export type WorkforceImportRow = {
  employeeNumber: string
  displayName: string
  externalPersonnelId?: string | null
  employmentStatus?: EmploymentStatus
  jobTitle: string
  primarySpecialty: string
  currentOperationalUnitId?: string | null
  isOperational?: boolean
}

export type WorkforceImportPreviewRequest = {
  importKind?: WorkforceImportKind
  sourceSystem: string
  sourceReference: string
  fileHash: string
  rows: WorkforceImportRow[]
}

export type WorkforceImportResult = {
  totalRows: number
  validRows: number
  rejectedRows: number
  duplicateRows: number
  appliedRows: number
  errors: string[]
}

export type WorkforceMemberUpdateRequest = {
  displayName: string
  employmentStatus: EmploymentStatus
  rankOrGrade?: string | null
  jobTitle: string
  primarySpecialty: string
  currentOperationalUnitId?: string | null
  supervisorWorkforceMemberId?: string | null
  isOperational: boolean
  isSensitiveRole: boolean
  rowVersion?: string | null
}

export type WorkforceReconciliationItem = {
  id: string
  issueType: string
  severity: string
  titleAr: string
  detailAr: string
  entityType: string
  entityId?: string | null
  sourceSystem?: string | null
  suggestedActionAr: string
  responsibleHintAr: string
  detectedAtUtc: string
}

export type WorkforceReconciliationList = {
  items: WorkforceReconciliationItem[]
  totalCount: number
  page: number
  pageSize: number
}

export type WorkforceReconciliationResolveRequest = {
  resolutionAction: string
  notes?: string | null
}

export type WorkforceCriticalPosition = {
  id: string
  roleDefinitionId: string
  roleCode: string
  roleNameAr: string
  facilityUnitId?: string | null
  shiftDefinitionId?: string | null
  requiredPrimaryCount: number
  requiredAlternateCount: number
  primaryFilled: number
  alternateFilled: number
  vacantPrimary: number
  vacantAlternate: number
  actingCount: number
  singlePointOfFailure: boolean
  criticality: WorkforceRoleCriticality
  statusAr: string
}

export type ResourceAssetListItem = {
  id: string
  resourceType: ResourceType
  assetCode: string
  displayName: string
  serialNumber?: string | null
  plateNumber?: string | null
  currentStatus: ResourceStatus
  condition: ResourceCondition
  criticality: ResourceCriticality
  operationalFacilityUnitNameAr?: string | null
  custodianNameAr?: string | null
  lastVerifiedAtUtc?: string | null
  hasOpenMaintenance: boolean
  dataQualityIssues: string[]
}

export type ResourceAssetCreateRequest = {
  resourceType: ResourceType
  assetCode: string
  displayName: string
  serialNumber?: string | null
  ownershipOrganizationId: string
  operationalFacilityUnitId?: string | null
  currentStatus: ResourceStatus
  condition: ResourceCondition
  criticality: ResourceCriticality
  sourceType: ResourceSourceType
  sourceReference?: string | null
}

export type ResourceImportRow = {
  resourceType: ResourceType
  assetCode: string
  displayName: string
  serialNumber?: string | null
  currentStatus: ResourceStatus
  condition: ResourceCondition
  criticality: ResourceCriticality
}

export type ResourceImportPreviewRequest = {
  sourceSystem: string
  sourceReference: string
  fileHash: string
  rows: ResourceImportRow[]
}

export type ResourceImportResult = {
  totalRows: number
  validRows: number
  rejectedRows: number
  duplicateRows: number
  appliedRows: number
  errors: string[]
}

export type FacilityPriorityQueuePayload = {
  limit: number
  items: Array<{
    type: string
    reference: string
    titleAr: string
    severityAr: string
    priorityRank: number
    reasonAr: string
    dueAtUtc?: string | null
    overdueDays?: number | null
    ownerAr?: string | null
    actionLabelAr: string
    drillDownTarget: WorkspaceDrillDownTarget
  }>
}

export type FacilityRecentActivityPayload = {
  limit: number
  items: Array<{
    eventType: string
    titleAr: string
    descriptionAr?: string | null
    occurredAtUtc: string
    actorDisplayName?: string | null
    entityReference: string
    tone: WorkspaceVisualTone
    drillDownTarget: WorkspaceDrillDownTarget
  }>
}

export type FacilityStructurePayload = {
  unitsCount: number
  buildingsCount: number
  assetLocationsCount: number
  units: Array<{
    unitId: string
    code: string
    nameAr: string
    parentUnitNameAr?: string | null
    openNotes: number
    overdueNotes: number
    openCorrectiveActions: number
  }>
}

export type FacilityDataQualityPayload = {
  domains: Array<{
    key: string
    labelAr: string
    statusCode: string
    statusAr: string
    confidenceAr: string
    lastUpdatedAtUtc?: string | null
    impactAr: string
    followUpIssue?: string | null
  }>
}

export type SensitiveCustodyWorkspacePayload = {
  summary: {
    totalWeapons: number
    serviceableWeapons: number
    issuedWeapons: number
    inArmoryWeapons: number
    underMaintenanceWeapons: number
    outOfServiceWeapons: number
    missingOrUnaccountedWeapons: number
    overdueReturns: number
    inspectionsDue: number
    openDiscrepancies: number
    ammunitionAvailable: number
    ammunitionMinimum: number
    ammunitionGap: number
    pendingApprovals: number
    staleDataItems: number
    lastInventoryAtUtc?: string | null
    readinessRate?: number | null
    verificationCoverage?: number | null
    inspectionCompliance?: number | null
    freshness: string
    confidence: string
    warnings: string[]
  }
  interventions: Array<{
    code: string
    severity: string
    reasonAr: string
    sourceEntityId?: string | null
    sourceEntityType: string
    facilityUnitId?: string | null
    ownerRoleAr?: string | null
    dueAtUtc?: string | null
    primaryActionAr: string
    drillDownTarget: WorkspaceDrillDownTarget
  }>
  dataQualityIssues: Array<{
    code: string
    count: number
    severity: string
    impactAr: string
    sourceAr: string
    ownerAr?: string | null
    correctiveActionAr: string
    drillDownTarget: WorkspaceDrillDownTarget
  }>
  timeline: Array<{
    eventType: string
    titleAr: string
    descriptionAr: string
    occurredAtUtc: string
    actorAr?: string | null
    entityReference: string
    drillDownTarget: WorkspaceDrillDownTarget
  }>
  allowedActions: Array<{
    key: string
    labelAr: string
    routeKey: string
    requiresReason: boolean
    requiresRowVersion: boolean
  }>
}

export type WorkspaceWidgetPayload =
  | ReferenceOperationalSummaryPayload
  | ReferenceCorrectiveActionsPayload
  | FacilityHeaderPayload
  | FacilityExecutiveSummaryPayload
  | FacilityNotesOverviewPayload
  | FacilityCorrectiveActionsPayload
  | FacilityAlertsEscalationsPayload
  | FacilityFormCompliancePayload
  | OccupancyWorkspacePayload
  | ResourceWorkspacePayload
  | WorkforceWorkspacePayload
  | SensitiveCustodyWorkspacePayload
  | FacilityPriorityQueuePayload
  | FacilityRecentActivityPayload
  | FacilityStructurePayload
  | FacilityDataQualityPayload
  | Record<string, unknown>

export type WorkspaceWidgetEnvelope<TPayload = WorkspaceWidgetPayload> = {
  widgetKey: string
  generatedAtUtc: string
  dataEffectiveAtUtc?: string | null
  freshness: DataFreshness
  confidence: WorkspaceConfidence
  scopeSummary: WorkspaceScopeSummary
  isPartial: boolean
  warningMessages: string[]
  payload: TPayload
  drillDownTargets: WorkspaceDrillDownTarget[]
  allowedActions: WorkspaceAllowedAction[]
}

export type WorkspaceShell = {
  definition: WorkspaceDefinition
  context: WorkspaceContext
  generatedAtUtc: string
  freshness: DataFreshness
  confidence: WorkspaceConfidence
  allowedActions: WorkspaceAllowedAction[]
  widgetDefinitions: WorkspaceWidgetDefinition[]
  widgets: WorkspaceWidgetEnvelope[]
  widgetFailures: Array<{ widgetKey: string; messageAr: string; isPartialSafe: boolean }>
  isPartial: boolean
}

export type WorkspaceFilters = {
  level?: WorkspaceLevel
  regionId?: string
  facilityId?: string
  entityId?: string
  fromUtc?: string
  toUtc?: string
  locale?: string
  timeZone?: string
}

export type DashboardTrendPoint = {
  bucketStartUtc: string
  bucketEndUtc: string
  labelAr: string
  notesCreated: number
  notesCompleted: number
  notesBecameOverdue: number
  correctiveActionsCompleted: number
  routingSuccess: number
  routingFailure: number
}

export type DashboardOperationsTrends = {
  points: DashboardTrendPoint[]
  fromUtc: string
  toUtc: string
  granularity: string
}

export type DashboardBreakdownRow = {
  key: string
  labelAr: string
  entityId?: string | null
  openBurden: number
  overdue: number
  critical: number
  unassigned: number
  correctiveActionsOverdue: number
  closureRateWithinDue?: number | null
}

export type DashboardOperationsBreakdowns = {
  dimension: number
  rows: DashboardBreakdownRow[]
}

export type DashboardOverdueNoteQueueItem = {
  id: string
  referenceNumber: string
  title: string
  severity: number
  severityAr: string
  status: number
  statusAr: string
  dueAtUtc?: string | null
  overdueDays?: number | null
  regionId?: string | null
  facilityId?: string | null
  facilityNameAr?: string | null
}

export type DashboardOverdueLocationQueueItem = {
  facilityId: string
  facilityNameAr: string
  regionId?: string | null
  regionNameAr?: string | null
  overdueCount: number
}

export type DashboardOverdueCorrectiveActionQueueItem = {
  id: string
  referenceNumber: string
  title: string
  status: number
  statusAr: string
  dueAtUtc?: string | null
  overdueDays?: number | null
  operationalNoteId: string
  noteReferenceNumber: string
}

export type DashboardRoutingFailureQueueItem = {
  noteId: string
  referenceNumber: string
  title: string
  failureCode: string
  failureMessageSafe: string
  decidedAtUtc: string
}

export type DashboardPriorityQueues = {
  mostOverdueNotes?: DashboardOverdueNoteQueueItem[] | null
  criticalUnassignedNotes?: DashboardOverdueNoteQueueItem[] | null
  topOverdueLocations?: DashboardOverdueLocationQueueItem[] | null
  mostOverdueCorrectiveActions?: DashboardOverdueCorrectiveActionQueueItem[] | null
  recentRoutingFailures?: DashboardRoutingFailureQueueItem[] | null
  limit: number
}

export type NoteRoutingRuleFilters = {
  page?: number
  pageSize?: number
  noteTypeId?: string
  scopeType?: number
  isActive?: boolean
  processingTargetType?: number
}

export type Notification = {
  id: string
  targetType: number
  targetId: string
  targetReferenceNumber: string
  titleAr: string
  messageAr: string
  priority: number
  status: number
  createdAtUtc: string
  readAtUtc?: string | null
  archivedAtUtc?: string | null
  rowVersion: string
}

export type EscalationPolicyFilters = {
  page?: number
  pageSize?: number
  search?: string
  targetType?: number
  isEnabled?: boolean
}

export type NotificationFilters = {
  page?: number
  pageSize?: number
  status?: number
  targetType?: number
  priority?: number
}

export type EscalationOccurrenceFilters = {
  page?: number
  pageSize?: number
  targetType?: number
  status?: number
}

export type Attachment = {
  id: string
  entityType: string
  entityId: string
  originalFileName: string
  contentType: string
  sizeBytes: number
  sha256: string
  classification: number
  scanStatus: number
  uploadedAtUtc: string
  isSensitiveRedacted?: boolean
}

// Enums serialize as numbers (System.Text.Json default). Keep in sync with Baseera.Domain.Forms.

export type FormListItem = {
  id: string
  code: string
  nameAr: string
  nameEn?: string | null
  descriptionSnippet?: string | null
  status: number
  statusAr: string
  classification: number
  scopeType: number
  regionId?: string | null
  facilityId?: string | null
  facilityUnitId?: string | null
  ownerDepartmentId?: string | null
  createdAtUtc: string
  rowVersion: string
  isSensitiveRedacted: boolean
}

export type FormDetail = {
  id: string
  code: string
  nameAr: string
  nameEn?: string | null
  description: string
  status: number
  statusAr: string
  classification: number
  scopeType: number
  regionId?: string | null
  facilityId?: string | null
  facilityUnitId?: string | null
  ownerDepartmentId?: string | null
  createdByUserId: string
  createdByDisplayName?: string | null
  updatedByUserId?: string | null
  updatedByDisplayName?: string | null
  lastModifiedByUserId?: string | null
  lastModifiedByDisplayName?: string | null
  submittedForReviewAtUtc?: string | null
  approvedAtUtc?: string | null
  archivedAtUtc?: string | null
  archivedByUserId?: string | null
  archivedByDisplayName?: string | null
  createdAtUtc: string
  updatedAtUtc?: string | null
  rowVersion: string
  isSensitiveRedacted: boolean
  allowedActions: string[]
}

export type FormReviewDecision = {
  id: string
  decision: number
  decisionAr: string
  reason?: string | null
  reviewedByUserId: string
  reviewedByDisplayName?: string | null
  reviewedAtUtc: string
  fromStatus: number
  fromStatusAr: string
  toStatus: number
  toStatusAr: string
  isAdministrativeOverride: boolean
}

export type FormAccessGrant = {
  id: string
  principalType: number
  principalId: string
  principalDisplayName?: string | null
  capability: number
  capabilityAr: string
  effect: number
  scopeType?: number | null
  regionId?: string | null
  facilityId?: string | null
  validFromUtc?: string | null
  validToUtc?: string | null
  reason: string
  createdByUserId: string
  createdByDisplayName?: string | null
  createdAtUtc: string
  rowVersion: string
}

export type FormGovernancePolicy = {
  id: string
  requireReviewBeforeApproval: boolean
  requireSeparationOfDuties: boolean
  allowDesignerToReviewOwnForm: boolean
  allowReviewerToApproveOwnReview: boolean
  allowApproverToPublish: boolean
  defaultRetentionDays: number
  sensitiveRetentionDays: number
  minimumRetentionDays: number
  auditSensitiveViews: boolean
  auditExports: boolean
  requireReasonForArchive: boolean
  rowVersion: string
}


export type FormVersionStatus = 0 | 1 | 2 | 3 | 4

export type FormVersionListItem = {
  id: string
  formDefinitionId: string
  versionNumber: number
  status: number
  statusAr: string
  basedOnVersionId?: string | null
  draftSchemaHash?: string | null
  schemaFormatVersion: number
  createdAtUtc: string
  lastSavedAtUtc?: string | null
  approvedAtUtc?: string | null
  snapshotId?: string | null
  rowVersion: string
}

export type FormVersionDetail = FormVersionListItem & {
  draftSchemaJson: string
  createdByUserId: string
  updatedByUserId?: string | null
  submittedForReviewAtUtc?: string | null
  approvedByUserId?: string | null
  allowedActions: string[]
}

export type FormSchemaValidationIssue = {
  code: string
  path: string
  entityId?: string | null
  fieldKey?: string | null
  messageAr: string
  severity: number
}

export type FormVersionValidateResult = {
  isValid: boolean
  schemaHash?: string | null
  issues: FormSchemaValidationIssue[]
  pageCount: number
  sectionCount: number
  fieldCount: number
  calculatedFieldCount: number
  conditionCount: number
}

export type FormSchemaSnapshotDto = {
  id: string
  formVersionId: string
  schemaFormatVersion: number
  canonicalSchemaJson: string
  schemaHash: string
  schemaSizeBytes: number
  pageCount: number
  sectionCount: number
  fieldCount: number
  calculatedFieldCount: number
  conditionCount: number
  createdByUserId: string
  createdAtUtc: string
}

export type FormVersionReviewDecisionDto = {
  id: string
  decision: number
  decisionAr: string
  reason?: string | null
  reviewedByUserId: string
  reviewedAtUtc: string
  fromStatus: number
  toStatus: number
  isAdministrativeOverride: boolean
}

export type FormTemplateListItem = {
  id: string
  code: string
  nameAr: string
  nameEn?: string | null
  description: string
  category: string
  classification: number
  visibility: number
  ownerDepartmentId?: string | null
  schemaHash: string
  pageCount: number
  sectionCount: number
  fieldCount: number
  createdAtUtc: string
}

export type FormCampaignScheduleRequest = {
  recurrenceKind: number
  firstOpenAtLocal: string
  responseWindowMinutes: number
  gracePeriodMinutes: number
  closeAfterMinutes: number
  businessDayAdjustment: number
  intervalDays?: number | null
  intervalWeeks?: number | null
  weekDays?: number[] | null
  dayOfMonth?: number | null
  missingDayPolicy?: number | null
  untilLocal?: string | null
  maxOccurrences?: number | null
  customDatesLocal?: string[] | null
}

export type FormCampaignTargetRequest = {
  ruleType: number
  regionIds?: string[] | null
  facilityIds?: string[] | null
  dynamicCriteria?: { regionIds?: string[] | null; facilityTypes?: string[] | null; isActive?: boolean | null } | null
}

export type FormCampaignExclusionRequest = { facilityId: string; reason: string }


export type FormResponseReviewAction =
  | 'start'
  | 'return'
  | 'approve'
  | 'reject'
  | 'close'

export type QueryParameterValue =
  | string
  | number
  | boolean
  | undefined

export type FormCampaignResponsePolicy = {
  completionBasis: number
  reviewMode: number
  requiredApprovalLevels: number
  allowLateSubmission: boolean
  allowResubmissionAfterReturn: boolean
  requireSubmissionAcknowledgement: boolean
  requireSeparationOfDuties: boolean
}

export type FormResponseWorkspaceItem = {
  assignmentId: string
  campaignId: string
  campaignCode: string
  campaignNameAr: string
  cycleId: string
  occurrenceKey: string
  facilityId: string
  facilityNameAr: string
  regionId: string
  regionNameAr: string
  openAtUtc: string
  dueAtUtc: string
  graceEndsAtUtc: string
  closeAtUtc: string
  effectiveDueAtUtc: string
  responseId?: string | null
  responseStatus?: number | null
  workStatus: number
  isOverdue: boolean
  isCompleted: boolean
  draftVersion?: number | null
  lastSavedAtUtc?: string | null
  submittedAtUtc?: string | null
  currentReviewLevel: number
  requiredApprovalLevels: number
  allowedActions: string[]
  rowVersion?: string | null
}

export type FormResponseWorkspaceDetail = FormResponseWorkspaceItem & {
  cycleStatus: number
  assignmentAvailable: boolean
  unavailableReason?: string | null
  draftAnswersJson?: string | null
  schemaJson: string
  schemaHash: string
  formClassification: number
  policy: FormCampaignResponsePolicy
  latestSubmission?: { id: string; submissionNumber: number; canonicalAnswersJson: string; submittedAtUtc: string } | null
  visibleComments: Array<{ id: string; fieldKey?: string | null; body: string; createdAtUtc: string }>
  fieldVisibility: Record<string, boolean>
  fieldRedacted: Record<string, boolean>
}

export type FormResponseDraftSaveResult = {
  responseId: string
  draftVersion: number
  rowVersion: string
  lastSavedAtUtc: string
  validationIssues: Array<{ code: string; path: string; fieldKey?: string | null; messageAr: string; severity: string }>
  calculatedValues: Record<string, unknown>
  visibleFieldKeys: string[]
  requiredFieldKeys: string[]
}

export type FormResponseReviewDetail = {
  workspace: FormResponseWorkspaceDetail
  submissions: Array<{ id: string; submissionNumber: number; canonicalAnswersJson: string; submittedAtUtc: string }>
  decisions: Array<{ id: string; decision: number; reason?: string | null; reviewedAtUtc: string; fromStatus: number; toStatus: number; reviewLevel: number }>
  comments: Array<{ id: string; fieldKey?: string | null; body: string; isVisibleToRespondent: boolean }>
  history: Array<{ id: string; eventType: string; occurredAtUtc: string; reason?: string | null }>
}

export type CreateFormCampaignRequest = {
  formDefinitionId: string
  formVersionId: string
  code: string
  nameAr: string
  nameEn?: string | null
  description?: string | null
  priority: number
  timeZoneId?: string | null
  schedule: FormCampaignScheduleRequest
  targets: FormCampaignTargetRequest[]
  exclusions?: FormCampaignExclusionRequest[] | null
  responsePolicy?: FormCampaignResponsePolicy | null
}

export type UpdateFormCampaignRequest = {
  nameAr: string
  nameEn?: string | null
  description?: string | null
  priority: number
  timeZoneId?: string | null
  schedule: FormCampaignScheduleRequest
  targets: FormCampaignTargetRequest[]
  exclusions?: FormCampaignExclusionRequest[] | null
  responsePolicy?: FormCampaignResponsePolicy | null
  rowVersion: string
}

export type FormCampaignListItem = {
  id: string
  code: string
  nameAr: string
  nameEn?: string | null
  formDefinitionId: string
  formCode: string
  formNameAr: string
  formVersionId: string
  versionNumber: number
  status: number
  recurrenceKind: number
  firstOpenAtLocal: string
  nextOccurrenceUtc?: string | null
  cycleCount: number
  lastCycleAtUtc?: string | null
  allowedActions: string[]
  rowVersion: string
}

export type FormCampaignDetail = FormCampaignListItem & {
  organizationId: string
  formSchemaSnapshotId: string
  schemaHash: string
  description?: string | null
  priority: number
  timeZoneId: string
  schedule: FormCampaignScheduleRequest
  targets: FormCampaignTargetRequest[]
  exclusions: Array<{ facilityId: string; facilityCode: string; facilityNameAr: string; reason: string }>
  publishedAtUtc?: string | null
  pausedAtUtc?: string | null
  pauseReason?: string | null
  cancelledAtUtc?: string | null
  cancellationReason?: string | null
  closedAtUtc?: string | null
  createdAtUtc: string
  responsePolicy: FormCampaignResponsePolicy
}

export type FormTargetPreviewFacility = {
  facilityId: string
  code: string
  nameAr: string
  regionId: string
  regionNameAr: string
  facilityType?: string | null
}

export type FormTargetPreview = {
  asOfUtc: string
  totalMatched: number
  totalExcluded: number
  finalTargetCount: number
  breakdownByRegion: Record<string, number>
  breakdownByFacilityType: Record<string, number>
  includedFacilityIds: string[]
  exclusions: Array<{ facilityId: string; reason: string }>
  sample: FormTargetPreviewFacility[]
  targetingFingerprint: string
  warnings: string[]
  invalidTargets: string[]
  unavailableFacilities: string[]
}

export type FormCycleListItem = {
  id: string
  sequenceNumber: number
  occurrenceKey: string
  status: number
  scheduledOccurrenceLocal: string
  openAtUtc: string
  dueAtUtc: string
  closeAtUtc: string
  assignedFacilityCount: number
  targetSnapshotHash: string
}

export type FormCycleDetail = FormCycleListItem & {
  campaignId: string
  scheduledOccurrenceUtc: string
  graceEndsAtUtc: string
  timeZoneId: string
  formVersionId: string
  formSchemaSnapshotId: string
  schemaHash: string
  generatedAtUtc: string
  generatedBy: string
}

export type FacilityAssignment = {
  id: string
  facilityId: string
  regionIdAtAssignment: string
  facilityCodeAtAssignment: string
  facilityNameArAtAssignment: string
  regionNameArAtAssignment: string
  facilityTypeAtAssignment?: string | null
  targetRuleType: number
  assignedAtUtc: string
  isAvailable: boolean
  unavailableReason?: string | null
}

export type SaveFormSchemaRequest = {
  schemaJson: string
  rowVersion: string
}

export type FormVersionTransitionRequest = {
  reason?: string | null
  rowVersion: string
}

export type CreateFormTemplateRequest = {
  formDefinitionId: string
  formVersionId: string
  code: string
  nameAr: string
  nameEn?: string | null
  description: string
  category: string
  visibility: number
  ownerDepartmentId?: string | null
}

export type CreateFormFromTemplateRequest = {
  code: string
  nameAr: string
  nameEn?: string | null
  description: string
  classification: number
  scopeType: number
  regionId?: string | null
  facilityId?: string | null
  facilityUnitId?: string | null
  ownerDepartmentId?: string | null
}

export type FormRetentionStatus = {
  formDefinitionId: string
  isRetentionApplicable: boolean
  retentionAnchorUtc?: string | null
  retentionDays: number
  expiresAtUtc?: string | null
  isExpired: boolean
  isEligibleForArchive: boolean
}

export type FormListFilters = {
  page?: number
  pageSize?: number
  search?: string
  status?: number
  classification?: number
  regionId?: string
  facilityId?: string
  sortBy?: string
  sortDesc?: boolean
}

export type CreateFormRequest = {
  code: string
  nameAr: string
  nameEn?: string | null
  description: string
  classification: number
  scopeType: number
  regionId?: string | null
  facilityId?: string | null
  facilityUnitId?: string | null
  ownerDepartmentId?: string | null
}

export type UpdateFormRequest = {
  nameAr: string
  nameEn?: string | null
  description: string
  classification: number
  ownerDepartmentId?: string | null
  rowVersion: string
}

export type FormTransitionRequest = {
  reason: string
  rowVersion: string
}

export type CreateFormAccessGrantRequest = {
  principalType: number
  principalId: string
  capability: number
  effect: number
  scopeType?: number | null
  regionId?: string | null
  facilityId?: string | null
  validFromUtc?: string | null
  validToUtc?: string | null
  reason: string
}

export type UpdateFormGovernancePolicyRequest = {
  requireReviewBeforeApproval: boolean
  requireSeparationOfDuties: boolean
  allowDesignerToReviewOwnForm: boolean
  allowReviewerToApproveOwnReview: boolean
  allowApproverToPublish: boolean
  defaultRetentionDays: number
  sensitiveRetentionDays: number
  minimumRetentionDays: number
  auditSensitiveViews: boolean
  auditExports: boolean
  requireReasonForArchive: boolean
  rowVersion: string
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  if (isTestAuthAllowed()) {
    const subject = getTestSubject()
    if (subject) {
      headers.set('X-Test-User', subject)
      headers.set('X-Test-DisplayName', subject)
    }
  } else if (accessTokenProvider) {
    const token = await accessTokenProvider()
    if (token) headers.set('Authorization', `Bearer ${token}`)
  }

  const response = await fetch(`${API_BASE}${path}`, { ...init, headers })
  if (response.status === 401) {
    throw new ApiError(401, 'انتهت الجلسة أو غير مصرح. سجّل الدخول مجددًا.')
  }
  if (!response.ok) {
    let detail = 'تعذر إكمال الطلب.'
    try {
      const body = await response.json()
      detail = body.detail || body.title || detail
    } catch {
      /* ignore */
    }
    throw new ApiError(response.status, detail)
  }

  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

function jsonRequest<T>(path: string, method: 'POST' | 'PUT', body: unknown): Promise<T> {
  return request<T>(path, {
    method,
    body: JSON.stringify(body),
    headers: { 'Content-Type': 'application/json' },
  })
}

const postJson = <T>(path: string, body: unknown) => jsonRequest<T>(path, 'POST', body)
const putJson = <T>(path: string, body: unknown) => jsonRequest<T>(path, 'PUT', body)

function appendPagingParams(params: URLSearchParams, filters: NoteListFilters): void {
  params.set('page', String(filters.page ?? 1))
  params.set('pageSize', String(filters.pageSize ?? 20))
  if (filters.search) params.set('search', filters.search)
  if (filters.sortBy) params.set('sortBy', filters.sortBy)
  if (filters.sortDesc) params.set('sortDesc', 'true')
}

function appendEnumFilterParams(params: URLSearchParams, filters: NoteListFilters): void {
  if (filters.status !== undefined) params.set('status', String(filters.status))
  if (filters.severity !== undefined) params.set('severity', String(filters.severity))
  if (filters.noteTypeId) params.set('noteTypeId', filters.noteTypeId)
  if (filters.sourceType !== undefined) params.set('sourceType', String(filters.sourceType))
  if (filters.classification !== undefined) params.set('classification', String(filters.classification))
  if (filters.overdueOnly) params.set('overdueOnly', 'true')
  if (filters.dueSoonDays !== undefined) params.set('dueSoonDays', String(filters.dueSoonDays))
  if (filters.unassignedOnly) params.set('unassignedOnly', 'true')
  if (filters.requiresMyAction) params.set('requiresMyAction', 'true')
  if (filters.requiresRouting) params.set('requiresRouting', 'true')
}

function appendScopeFilterParams(params: URLSearchParams, filters: NoteListFilters): void {
  if (filters.regionId) params.set('regionId', filters.regionId)
  if (filters.facilityId) params.set('facilityId', filters.facilityId)
  if (filters.facilityUnitId) params.set('facilityUnitId', filters.facilityUnitId)
  if (filters.ownerDepartmentId) params.set('ownerDepartmentId', filters.ownerDepartmentId)
  if (filters.assignedToUserId) params.set('assignedToUserId', filters.assignedToUserId)
}

type DateRangeFilters = Pick<NoteListFilters, 'dueFrom' | 'dueTo' | 'createdFrom' | 'createdTo'>

function appendDateRangeParams(params: URLSearchParams, filters: DateRangeFilters): void {
  if (filters.dueFrom) params.set('dueFrom', filters.dueFrom)
  if (filters.dueTo) params.set('dueTo', filters.dueTo)
  if (filters.createdFrom) params.set('createdFrom', filters.createdFrom)
  if (filters.createdTo) params.set('createdTo', filters.createdTo)
}

function buildNoteQuery(filters: NoteListFilters): string {
  const params = new URLSearchParams()
  appendPagingParams(params, filters)
  appendEnumFilterParams(params, filters)
  appendScopeFilterParams(params, filters)
  appendDateRangeParams(params, filters)
  return params.toString()
}

function buildCorrectiveActionQuery(filters: CorrectiveActionListFilters): string {
  const params = new URLSearchParams()
  appendCorrectiveActionPaging(params, filters)
  appendCorrectiveActionEnumFilters(params, filters)
  appendCorrectiveActionScopeFilters(params, filters)
  appendDateRangeParams(params, filters)
  appendCorrectiveActionStateFilters(params, filters)
  return params.toString()
}

function buildDashboardQuery(filters: DashboardOperationsFilters): string {
  return buildSimpleQuery({
    periodDays: filters.periodDays,
    fromUtc: filters.fromUtc,
    toUtc: filters.toUtc,
    regionId: filters.regionId,
    facilityId: filters.facilityId,
    facilityUnitId: filters.facilityUnitId,
    noteTypeId: filters.noteTypeId,
    severity: filters.severity,
    status: filters.status,
    breakdownBy: filters.breakdownBy,
    queue: filters.queue,
  })
}

function buildFormComplianceQuery(filters: FormComplianceFilters): string {
  return buildSimpleQuery(filters as Record<string, QueryParameterValue>)
}

function buildSimpleQuery(filters: Record<string, QueryParameterValue>): string {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(filters)) {
    if (value !== undefined) params.set(key, String(value))
  }
  if (!params.has('page')) params.set('page', '1')
  if (!params.has('pageSize')) params.set('pageSize', '20')
  return params.toString()
}

function buildFormQuery(filters: FormListFilters): string {
  return buildSimpleQuery({
    page: filters.page,
    pageSize: filters.pageSize,
    search: filters.search,
    status: filters.status,
    classification: filters.classification,
    regionId: filters.regionId,
    facilityId: filters.facilityId,
    sortBy: filters.sortBy,
    sortDesc: filters.sortDesc,
  })
}

function appendCorrectiveActionPaging(params: URLSearchParams, filters: CorrectiveActionListFilters): void {
  params.set('page', String(filters.page ?? 1))
  params.set('pageSize', String(filters.pageSize ?? 20))
  if (filters.search) params.set('search', filters.search)
  if (filters.noteId) params.set('noteId', filters.noteId)
  if (filters.sortBy) params.set('sortBy', filters.sortBy)
  if (filters.sortDesc) params.set('sortDesc', 'true')
}

function appendCorrectiveActionEnumFilters(params: URLSearchParams, filters: CorrectiveActionListFilters): void {
  if (filters.status !== undefined) params.set('status', String(filters.status))
  if (filters.priority !== undefined) params.set('priority', String(filters.priority))
  if (filters.classification !== undefined) params.set('classification', String(filters.classification))
}

function appendCorrectiveActionScopeFilters(params: URLSearchParams, filters: CorrectiveActionListFilters): void {
  if (filters.ownerDepartmentId) params.set('ownerDepartmentId', filters.ownerDepartmentId)
  if (filters.assignedToUserId) params.set('assignedToUserId', filters.assignedToUserId)
  if (filters.regionId) params.set('regionId', filters.regionId)
  if (filters.facilityId) params.set('facilityId', filters.facilityId)
  if (filters.facilityUnitId) params.set('facilityUnitId', filters.facilityUnitId)
}

function appendCorrectiveActionStateFilters(params: URLSearchParams, filters: CorrectiveActionListFilters): void {
  if (filters.overdueOnly) params.set('overdueOnly', 'true')
  if (filters.dueSoonDays !== undefined) params.set('dueSoonDays', String(filters.dueSoonDays))
}

async function downloadFile(path: string): Promise<{ blob: Blob; fileName: string }> {
  const headers = new Headers()
  if (isTestAuthAllowed()) {
    const subject = getTestSubject()
    if (subject) {
      headers.set('X-Test-User', subject)
      headers.set('X-Test-DisplayName', subject)
    }
  } else if (accessTokenProvider) {
    const token = await accessTokenProvider()
    if (token) headers.set('Authorization', `Bearer ${token}`)
  }

  const response = await fetch(`${API_BASE}${path}`, { headers })
  if (response.status === 401) {
    throw new ApiError(401, 'انتهت الجلسة أو غير مصرح. سجّل الدخول مجددًا.')
  }
  if (!response.ok) {
    let detail = 'تعذر تنزيل الملف.'
    try {
      const body = await response.json()
      detail = body.detail || body.title || detail
    } catch {
      /* ignore */
    }
    throw new ApiError(response.status, detail)
  }

  const disposition = response.headers.get('content-disposition') ?? ''
  const match = /filename="?([^";]+)"?/i.exec(disposition)
  const fileName = match ? decodeURIComponent(match[1]) : 'download'
  const blob = await response.blob()
  return { blob, fileName }
}

export type RiskWorkspaceSummary = {
  openRisks: number
  criticalRisks: number
  highRisks: number
  increasingTrendRisks: number
  recurringRisks: number
  overdueReviewRisks: number
  risksWithoutOwner: number
  risksWithoutTreatment: number
  overdueTreatmentActions: number
  acceptedRisksNearingReview: number
  staleDataRisks: number
  averageOpenRiskAgeDays: number
  lastUpdatedAtUtc: string | null
}

export type RiskListItem = {
  id: string
  riskCode: string
  title: string
  categoryNameAr: string
  riskType: number
  riskTypeAr: string
  status: number
  statusAr: string
  inherentRatingCode: string | null
  inherentRatingLabelAr: string | null
  residualRatingCode: string | null
  residualRatingLabelAr: string | null
  currentScore: number | null
  trend: number
  trendAr: string
  ownerDisplayName: string | null
  treatmentStrategy: number | null
  treatmentStrategyAr: string | null
  firstIdentifiedAtUtc: string
  nextReviewDueAtUtc: string | null
  ageDays: number
  sourceCount: number
  isDataStale: boolean
  allowedPrimaryAction: string
}

export type RiskPagedResult<T> = {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

export type RiskImpactBreakdown = {
  dimensionNameAr: string
  impactLevelNameAr: string
  numericValue: number
  rationaleAr: string | null
}

export type RiskScoreExplanation = {
  matrixCode: string
  matrixVersion: number
  formulaAr: string
  likelihoodLabelAr: string
  likelihoodValue: number
  impactBreakdown: RiskImpactBreakdown[]
  calculatedScore: number
  ratingBandCode: string
  ratingBandLabelAr: string
}

export type RiskDetail = {
  id: string
  riskCode: string
  title: string
  description: string | null
  riskCategoryId: string
  categoryNameAr: string
  riskType: number
  riskTypeAr: string
  status: number
  statusAr: string
  treatmentStrategy: number | null
  treatmentStrategyAr: string | null
  confidentialityLevel: number
  facilityId: string | null
  facilityUnitId: string | null
  ownerWorkforceMemberId: string | null
  ownerDisplayName: string | null
  firstIdentifiedAtUtc: string
  lastReviewedAtUtc: string | null
  nextReviewDueAtUtc: string | null
  acceptedUntilUtc: string | null
  closedAtUtc: string | null
  closureReason: string | null
  reopenedCount: number
  inherentAssessment: RiskScoreExplanation | null
  currentAssessment: RiskScoreExplanation | null
  residualAssessment: RiskScoreExplanation | null
  trend: number
  trendAr: string
  trendReasonAr: string
  recurrencePattern: number
  sourceCount: number
  openControlCount: number
  openTreatmentPlanCount: number
  overdueTreatmentActionCount: number
  isDataStale: boolean
  allowedActions: string[]
  rowVersion: string
}

export type RiskInterventionItem = {
  interventionType: string
  severityAr: string
  priorityRank: number
  riskRecordId: string
  riskCode: string
  titleAr: string
  reasonAr: string
  dueAtUtc: string | null
  ownerAr: string | null
  primaryActionAr: string
}

export type RiskWorkspacePayload = {
  summary: RiskWorkspaceSummary
  interventions: RiskInterventionItem[]
}

export type RiskCreateRequest = {
  title: string
  description?: string
  riskCategoryId: string
  riskType: number
  confidentialityLevel?: number
  facilityUnitId?: string
  ownerWorkforceMemberId?: string
  sourceType?: number
  sourceReference?: string
}

export type RiskCommandBody = {
  command: string
  ownerWorkforceMemberId?: string
  ownerUserId?: string
  reason?: string
  rowVersion: string
}

export type RiskCategoryItem = {
  id: string
  code: string
  nameAr: string
  nameEn: string | null
  parentCategoryId: string | null
  isActive: boolean
  displayOrder: number
  rowVersion: string
}

export type RiskDataQualityIssue = {
  code: string
  severityAr: string
  count: number
  impactAr: string
  sourceEntity: string
  responsibleRoleAr: string
  correctiveActionAr: string
}

export const api = {
  me: () => request<Me>('/api/v1/me'),
  regions: (search = '') =>
    request<Paged<Region>>(`/api/v1/regions?page=1&pageSize=50&search=${encodeURIComponent(search)}`),
  facilities: (regionId?: string, search = '') => {
    const params = new URLSearchParams({ page: '1', pageSize: '50', search })
    if (regionId) params.set('regionId', regionId)
    return request<Paged<Facility>>(`/api/v1/facilities?${params}`)
  },
  facilityUnits: (facilityId: string, search = '') => {
    const params = new URLSearchParams({ facilityId, page: '1', pageSize: '100', search })
    return request<Paged<FacilityUnit>>(`/api/v1/facility-units?${params}`)
  },
  departments: (search = '') =>
    request<Paged<Department>>(`/api/v1/departments?page=1&pageSize=100&search=${encodeURIComponent(search)}`),
  users: (search = '') =>
    request<Paged<User>>(`/api/v1/users?page=1&pageSize=50&search=${encodeURIComponent(search)}`),
  noteTypes: (includeInactive = true) =>
    request<NoteType[]>(`/api/v1/note-types?includeInactive=${includeInactive}`),
  myNoteTypes: () => request<NoteType[]>('/api/v1/me/note-types'),
  myNoteIntakeContext: () => request<NoteIntakeContext>('/api/v1/me/note-intake-context'),
  myNoteIntakeFacilities: (regionId: string) =>
    request<Array<{ id: string; regionId: string; nameAr: string }>>(`/api/v1/me/note-intake-context/facilities?regionId=${encodeURIComponent(regionId)}`),
  auditLogs: (module = '') => {
    const params = new URLSearchParams({ page: '1', pageSize: '50' })
    if (module) params.set('module', module)
    return request<Paged<AuditLog>>(`/api/v1/audit-logs?${params}`)
  },
  uploadAttachment: async (file: File, entityType: string, entityId: string, reason: string) => {
    const form = new FormData()
    form.append('file', file)
    form.append('entityType', entityType)
    form.append('entityId', entityId)
    form.append('classification', 'Internal')
    form.append('reason', reason)
    return request<Attachment>('/api/v1/attachments', { method: 'POST', body: form })
  },
  downloadAttachment: (id: string) => downloadFile(`/api/v1/attachments/${id}/download`),

  notes: {
    workspace: (filters: NoteListFilters = {}) =>
      request<NoteWorkspaceList>(`/api/v1/notes/workspace?${buildNoteQuery(filters)}`),
    workspaceDetail: (id: string) => request<NoteWorkspaceDetail>(`/api/v1/notes/${id}/workspace`),
    list: (filters: NoteListFilters = {}) =>
      request<Paged<NoteListItem>>(`/api/v1/notes?${buildNoteQuery(filters)}`),
    get: (id: string) => request<NoteDetail>(`/api/v1/notes/${id}`),
    create: (body: CreateNoteRequest) =>
      postJson<NoteDetail>('/api/v1/notes', body),
    update: (id: string, body: UpdateNoteRequest) =>
      putJson<NoteDetail>(`/api/v1/notes/${id}`, body),
    submit: (id: string, body: TransitionNoteRequest) =>
      postJson<NoteDetail>(`/api/v1/notes/${id}/submit`, body),
    assign: (id: string, body: AssignNoteRequest) =>
      postJson<NoteDetail>(`/api/v1/notes/${id}/assign`, body),
    startWork: (id: string, body: WorkflowActionRequest) =>
      postJson<NoteDetail>(`/api/v1/notes/${id}/start-work`, body),
    submitForVerification: (id: string, body: WorkflowActionRequest) =>
      postJson<NoteDetail>(`/api/v1/notes/${id}/submit-for-verification`, body),
    returnForRework: (id: string, body: TransitionNoteRequest) =>
      postJson<NoteDetail>(`/api/v1/notes/${id}/return-for-rework`, body),
    verifyClosure: (id: string, body: CloseNoteRequest) =>
      postJson<NoteDetail>(`/api/v1/notes/${id}/verify-closure`, body),
    reopen: (id: string, body: ReopenNoteRequest) =>
      postJson<NoteDetail>(`/api/v1/notes/${id}/reopen`, body),
    cancel: (id: string, body: TransitionNoteRequest) =>
      postJson<NoteDetail>(`/api/v1/notes/${id}/cancel`, body),
    archive: (id: string, body: TransitionNoteRequest) =>
      postJson<void>(`/api/v1/notes/${id}/archive`, body),
    restore: (id: string, body: TransitionNoteRequest) =>
      postJson<void>(`/api/v1/notes/${id}/restore`, body),
    history: (id: string) => request<NoteStatusHistoryEntry[]>(`/api/v1/notes/${id}/history`),
    assignments: (id: string) => request<NoteAssignment[]>(`/api/v1/notes/${id}/assignments`),
    eligibleAssignees: (id: string) => request<EligibleUser[]>(`/api/v1/notes/${id}/eligible-assignees`),
    eligibleReviewers: (id: string) => request<EligibleUser[]>(`/api/v1/notes/${id}/eligible-reviewers`),
    attachments: (id: string) => request<Attachment[]>(`/api/v1/notes/${id}/attachments`),
    correctiveActions: (id: string, filters: CorrectiveActionListFilters = {}) =>
      request<Paged<CorrectiveActionListItem>>(`/api/v1/notes/${id}/corrective-actions?${buildCorrectiveActionQuery(filters)}`),
    createCorrectiveAction: (id: string, body: CreateCorrectiveActionRequest) =>
      postJson<CorrectiveActionDetail>(`/api/v1/notes/${id}/corrective-actions`, body),
  },

  correctiveActions: {
    list: (filters: CorrectiveActionListFilters = {}) =>
      request<Paged<CorrectiveActionListItem>>(`/api/v1/corrective-actions?${buildCorrectiveActionQuery(filters)}`),
    get: (id: string) => request<CorrectiveActionDetail>(`/api/v1/corrective-actions/${id}`),
    update: (id: string, body: UpdateCorrectiveActionRequest) =>
      putJson<CorrectiveActionDetail>(`/api/v1/corrective-actions/${id}`, body),
    submit: (id: string, body: TransitionNoteRequest) =>
      postJson<CorrectiveActionDetail>(`/api/v1/corrective-actions/${id}/submit`, body),
    assign: (id: string, body: AssignNoteRequest) =>
      postJson<CorrectiveActionDetail>(`/api/v1/corrective-actions/${id}/assign`, body),
    startWork: (id: string, body: TransitionNoteRequest) =>
      postJson<CorrectiveActionDetail>(`/api/v1/corrective-actions/${id}/start-work`, body),
    submitForVerification: (id: string, body: CompleteCorrectiveActionRequest) =>
      postJson<CorrectiveActionDetail>(`/api/v1/corrective-actions/${id}/submit-for-verification`, body),
    returnForRework: (id: string, body: TransitionNoteRequest) =>
      postJson<CorrectiveActionDetail>(`/api/v1/corrective-actions/${id}/return-for-rework`, body),
    verifyCompletion: (id: string, body: CompleteCorrectiveActionRequest) =>
      postJson<CorrectiveActionDetail>(`/api/v1/corrective-actions/${id}/verify-completion`, body),
    reopen: (id: string, body: ReopenNoteRequest) =>
      postJson<CorrectiveActionDetail>(`/api/v1/corrective-actions/${id}/reopen`, body),
    cancel: (id: string, body: TransitionNoteRequest) =>
      postJson<CorrectiveActionDetail>(`/api/v1/corrective-actions/${id}/cancel`, body),
    archive: (id: string, body: TransitionNoteRequest) =>
      postJson<void>(`/api/v1/corrective-actions/${id}/archive`, body),
    restore: (id: string, body: TransitionNoteRequest) =>
      postJson<void>(`/api/v1/corrective-actions/${id}/restore`, body),
    history: (id: string) => request<CorrectiveActionStatusHistoryEntry[]>(`/api/v1/corrective-actions/${id}/history`),
    assignments: (id: string) => request<CorrectiveActionAssignment[]>(`/api/v1/corrective-actions/${id}/assignments`),
    attachments: (id: string) => request<Attachment[]>(`/api/v1/corrective-actions/${id}/attachments`),
  },

  escalationPolicies: {
    list: (filters: EscalationPolicyFilters = {}) =>
      request<Paged<EscalationPolicy>>(`/api/v1/escalation-policies?${buildSimpleQuery(filters)}`),
    get: (id: string) => request<EscalationPolicy>(`/api/v1/escalation-policies/${id}`),
    create: (body: CreateEscalationPolicyRequest) =>
      postJson<EscalationPolicy>('/api/v1/escalation-policies', body),
    update: (id: string, body: UpdateEscalationPolicyRequest) =>
      putJson<EscalationPolicy>(`/api/v1/escalation-policies/${id}`, body),
    activate: (id: string, body: RowVersionRequest) =>
      postJson<EscalationPolicy>(`/api/v1/escalation-policies/${id}/activate`, body),
    deactivate: (id: string, body: RowVersionRequest) =>
      postJson<EscalationPolicy>(`/api/v1/escalation-policies/${id}/deactivate`, body),
    archive: (id: string, body: RowVersionRequest) =>
      postJson<void>(`/api/v1/escalation-policies/${id}/archive`, body),
    restore: (id: string, body: RowVersionRequest) =>
      postJson<void>(`/api/v1/escalation-policies/${id}/restore`, body),
    rules: (id: string) => request<EscalationRule[]>(`/api/v1/escalation-policies/${id}/rules`),
    createRule: (id: string, body: CreateEscalationRuleRequest) =>
      postJson<EscalationRule>(`/api/v1/escalation-policies/${id}/rules`, body),
    updateRule: (id: string, body: UpdateEscalationRuleRequest) =>
      putJson<EscalationRule>(`/api/v1/escalation-rules/${id}`, body),
    enableRule: (id: string, body: RowVersionRequest) =>
      postJson<EscalationRule>(`/api/v1/escalation-rules/${id}/enable`, body),
    disableRule: (id: string, body: RowVersionRequest) =>
      postJson<EscalationRule>(`/api/v1/escalation-rules/${id}/disable`, body),
  },

  escalations: {
    run: () => postJson<EscalationRunResult>('/api/v1/escalations/run', {}),
    occurrences: (filters: EscalationOccurrenceFilters = {}) =>
      request<Paged<EscalationOccurrence>>(`/api/v1/escalations/occurrences?${buildSimpleQuery(filters)}`),
    occurrence: (id: string) => request<EscalationOccurrence>(`/api/v1/escalations/occurrences/${id}`),
    retry: (id: string) => postJson<void>(`/api/v1/escalations/occurrences/${id}/retry`, {}),
  },

  noteRoutingRules: {
    list: (filters: NoteRoutingRuleFilters = {}) =>
      request<Paged<NoteRoutingRule>>(`/api/v1/note-routing-rules?${buildSimpleQuery(filters)}`),
    get: (id: string) => request<NoteRoutingRule>(`/api/v1/note-routing-rules/${id}`),
    create: (body: NoteRoutingRuleRequest) =>
      postJson<NoteRoutingRule>('/api/v1/note-routing-rules', body),
    update: (id: string, body: UpdateNoteRoutingRuleRequest) =>
      putJson<NoteRoutingRule>(`/api/v1/note-routing-rules/${id}`, body),
    activate: (id: string, body: TransitionNoteRequest) =>
      postJson<NoteRoutingRule>(`/api/v1/note-routing-rules/${id}/activate`, body),
    deactivate: (id: string, body: TransitionNoteRequest) =>
      postJson<NoteRoutingRule>(`/api/v1/note-routing-rules/${id}/deactivate`, body),
    archive: (id: string, body: TransitionNoteRequest) =>
      postJson<void>(`/api/v1/note-routing-rules/${id}/archive`, body),
    restore: (id: string, body: TransitionNoteRequest) =>
      postJson<void>(`/api/v1/note-routing-rules/${id}/restore`, body),
    effectiveness: () => request<NoteRoutingEffectiveness>('/api/v1/note-routing/effectiveness'),
  },

  noteRouting: {
    run: (noteId: string, body: { rowVersion: string; reason: string; replaceCurrentAssignment?: boolean; idempotencyKey: string }) =>
      postJson<NoteDetail>(`/api/v1/notes/${noteId}/routing/run`, body),
    preview: (noteId: string) =>
      postJson<NoteRoutingPreview>(`/api/v1/notes/${noteId}/routing/preview`, {}),
  },

  notifications: {
    list: (filters: NotificationFilters = {}) =>
      request<Paged<Notification>>(`/api/v1/notifications?${buildSimpleQuery(filters)}`),
    unreadCount: () => request<{ count: number }>('/api/v1/notifications/unread-count'),
    get: (id: string) => request<Notification>(`/api/v1/notifications/${id}`),
    markRead: (id: string, body: RowVersionRequest) =>
      postJson<Notification>(`/api/v1/notifications/${id}/read`, body),
    markAllRead: () => postJson<{ count: number }>('/api/v1/notifications/read-all', {}),
    archive: (id: string, body: RowVersionRequest) =>
      postJson<Notification>(`/api/v1/notifications/${id}/archive`, body),
  },

  dashboard: {
    operations: {
      summary: (filters: DashboardOperationsFilters = {}) =>
        request<DashboardOperationsSummary>(`/api/v1/dashboard/operations/summary?${buildDashboardQuery(filters)}`),
      trends: (filters: DashboardOperationsFilters = {}) =>
        request<DashboardOperationsTrends>(`/api/v1/dashboard/operations/trends?${buildDashboardQuery(filters)}`),
      breakdowns: (filters: DashboardOperationsFilters = {}) =>
        request<DashboardOperationsBreakdowns>(`/api/v1/dashboard/operations/breakdowns?${buildDashboardQuery(filters)}`),
      priorityQueues: (filters: DashboardOperationsFilters = {}) =>
        request<DashboardPriorityQueues>(`/api/v1/dashboard/operations/priority-queues?${buildDashboardQuery(filters)}`),
    },
  },

  workspaces: {
    get: (workspaceKey: string, filters: WorkspaceFilters = {}) =>
      request<WorkspaceShell>(`/api/v1/workspaces/${workspaceKey}?${buildSimpleQuery(filters)}`),
    widgets: (workspaceKey: string, filters: WorkspaceFilters = {}) =>
      request<WorkspaceWidgetDefinition[]>(`/api/v1/workspaces/${workspaceKey}/widgets?${buildSimpleQuery(filters)}`),
    widget: (workspaceKey: string, widgetKey: string, filters: WorkspaceFilters = {}) =>
      request<{ definition: WorkspaceWidgetDefinition; data: WorkspaceWidgetEnvelope }>(
        `/api/v1/workspaces/${workspaceKey}/widgets/${widgetKey}?${buildSimpleQuery(filters)}`),
  },

  occupancy: {
    summary: (facilityId: string, filters: Record<string, QueryParameterValue> = {}) =>
      request<OccupancySummaryPayload>(`/api/v1/facilities/${facilityId}/occupancy/summary?${buildSimpleQuery(filters)}`),
    units: (facilityId: string, filters: Record<string, QueryParameterValue> = {}) =>
      request<{ units: OccupancyUnitPayload[] }>(`/api/v1/facilities/${facilityId}/occupancy/units?${buildSimpleQuery(filters)}`),
    movementsSummary: (facilityId: string, filters: Record<string, QueryParameterValue> = {}) =>
      request<OccupancyMovementSummaryPayload>(`/api/v1/facilities/${facilityId}/occupancy/movements/summary?${buildSimpleQuery(filters)}`),
    recordCapacity: (facilityId: string, body: OccupancyCapacityRequest) =>
      postJson<{ id: string }>(`/api/v1/facilities/${facilityId}/occupancy/capacity`, body),
    recordSnapshot: (facilityId: string, body: OccupancySnapshotRequest) =>
      postJson<{ id: string }>(`/api/v1/facilities/${facilityId}/occupancy/snapshots`, body),
    importMovements: (facilityId: string, body: OccupancyMovementImportRequest) =>
      postJson<OccupancyImportResult>(`/api/v1/facilities/${facilityId}/occupancy/movements/import`, body),
  },

  resources: {
    summary: (facilityId: string) =>
      request<ResourceSummaryPayload>(`/api/v1/facilities/${facilityId}/resources/summary`),
    categories: (facilityId: string) =>
      request<ResourceCategoryReadinessPayload[]>(`/api/v1/facilities/${facilityId}/resources/categories`),
    exceptions: (facilityId: string, limit = 20) =>
      request<ResourceExceptionPayload[]>(`/api/v1/facilities/${facilityId}/resources/exceptions?limit=${limit}`),
    units: (facilityId: string) =>
      request<ResourceUnitDistributionPayload[]>(`/api/v1/facilities/${facilityId}/resources/units`),
    timeline: (facilityId: string, limit = 50) =>
      request<ResourceActivityPayload[]>(`/api/v1/facilities/${facilityId}/resources/timeline?limit=${limit}`),
    assets: (facilityId: string, filters: Record<string, QueryParameterValue> = {}) =>
      request<ResourceAssetListItem[]>(`/api/v1/facilities/${facilityId}/resources/assets?${buildSimpleQuery(filters)}`),
    createAsset: (facilityId: string, body: ResourceAssetCreateRequest) =>
      postJson<{ id: string }>(`/api/v1/facilities/${facilityId}/resources/assets`, body),
    importPreview: (facilityId: string, body: ResourceImportPreviewRequest) =>
      postJson<ResourceImportResult>(`/api/v1/facilities/${facilityId}/resources/import/preview`, body),
    importConfirm: (facilityId: string, body: ResourceImportPreviewRequest) =>
      postJson<ResourceImportResult>(`/api/v1/facilities/${facilityId}/resources/import/confirm`, body),
  },

  workforce: {
    summary: (facilityId: string) =>
      request<WorkforceSummaryPayload>(`/api/v1/facilities/${facilityId}/workforce/summary`),
    coverage: (facilityId: string) =>
      request<WorkforceCoverageRowPayload[]>(`/api/v1/facilities/${facilityId}/workforce/coverage`),
    units: (facilityId: string) =>
      request<WorkforceUnitCoveragePayload[]>(`/api/v1/facilities/${facilityId}/workforce/units`),
    roles: (facilityId: string) =>
      request<WorkforceRoleDefinitionPayload[]>(`/api/v1/facilities/${facilityId}/workforce/roles`),
    members: (facilityId: string, filters: Record<string, QueryParameterValue> = {}) =>
      request<WorkforceMemberListItem[]>(`/api/v1/facilities/${facilityId}/workforce/members?${buildSimpleQuery(filters)}`),
    member: (facilityId: string, memberId: string) =>
      request<WorkforceMemberDetail>(`/api/v1/facilities/${facilityId}/workforce/members/${memberId}`),
    createMember: (facilityId: string, body: WorkforceMemberCreateRequest) =>
      postJson<{ id: string }>(`/api/v1/facilities/${facilityId}/workforce/members`, body),
    updateMember: (facilityId: string, memberId: string, body: WorkforceMemberUpdateRequest) =>
      putJson<void>(`/api/v1/facilities/${facilityId}/workforce/members/${memberId}`, body),
    createAssignment: (facilityId: string, body: WorkforceAssignmentRequest) =>
      postJson<{ id: string }>(`/api/v1/facilities/${facilityId}/workforce/assignments`, body),
    createQualification: (facilityId: string, body: WorkforceQualificationRequest) =>
      postJson<{ id: string }>(`/api/v1/facilities/${facilityId}/workforce/qualifications`, body),
    qualifications: (facilityId: string, filters: Record<string, QueryParameterValue> = {}) =>
      request<WorkforceQualificationList>(`/api/v1/facilities/${facilityId}/workforce/qualifications?${buildSimpleQuery(filters)}`),
    requirements: (facilityId: string) =>
      request<StaffingRequirementPayload[]>(`/api/v1/facilities/${facilityId}/workforce/requirements`),
    createRequirement: (facilityId: string, body: StaffingRequirementRequest) =>
      postJson<{ id: string }>(`/api/v1/facilities/${facilityId}/workforce/requirements`, body),
    rosters: (facilityId: string) =>
      request<DutyRosterPayload[]>(`/api/v1/facilities/${facilityId}/workforce/rosters`),
    createRoster: (facilityId: string, body: DutyRosterCreateRequest) =>
      postJson<{ id: string }>(`/api/v1/facilities/${facilityId}/workforce/rosters`, body),
    addRosterAssignment: (facilityId: string, rosterId: string, body: DutyRosterAssignmentRequest) =>
      postJson<{ id: string }>(`/api/v1/facilities/${facilityId}/workforce/rosters/${rosterId}/assignments`, body),
    publishRoster: (facilityId: string, rosterId: string) =>
      request<void>(`/api/v1/facilities/${facilityId}/workforce/rosters/${rosterId}/publish`, { method: 'POST' }),
    createAvailability: (facilityId: string, body: WorkforceAvailabilityRequest) =>
      postJson<{ id: string }>(`/api/v1/facilities/${facilityId}/workforce/availability`, body),
    importPreview: (facilityId: string, body: WorkforceImportPreviewRequest) =>
      postJson<WorkforceImportResult>(`/api/v1/facilities/${facilityId}/workforce/import/preview`, body),
    importConfirm: (facilityId: string, body: WorkforceImportPreviewRequest) =>
      postJson<WorkforceImportResult>(`/api/v1/facilities/${facilityId}/workforce/import/confirm`, body),
    dataQuality: (facilityId: string) =>
      request<WorkforceDataQualityPayload>(`/api/v1/facilities/${facilityId}/workforce/data-quality`),
    criticalPositions: (facilityId: string) =>
      request<WorkforceCriticalPosition[]>(`/api/v1/facilities/${facilityId}/workforce/critical-positions`),
    reconciliation: (facilityId: string, filters: Record<string, QueryParameterValue> = {}) =>
      request<WorkforceReconciliationList>(`/api/v1/facilities/${facilityId}/workforce/reconciliation?${buildSimpleQuery(filters)}`),
    resolveReconciliation: (facilityId: string, itemId: string, body: WorkforceReconciliationResolveRequest) =>
      postJson<void>(`/api/v1/facilities/${facilityId}/workforce/reconciliation/${encodeURIComponent(itemId)}/resolve`, body),
    export: (facilityId: string, filters: Record<string, QueryParameterValue> = {}) =>
      downloadFile(`/api/v1/facilities/${facilityId}/workforce/export?${buildSimpleQuery(filters)}`),
  },

  risks: {
    summary: (facilityId: string) =>
      request<RiskWorkspaceSummary>(`/api/v1/facilities/${facilityId}/risks/summary`),
    list: (facilityId: string, filters: Record<string, QueryParameterValue> = {}) =>
      request<RiskPagedResult<RiskListItem>>(`/api/v1/facilities/${facilityId}/risks?${buildSimpleQuery(filters)}`),
    get: (facilityId: string, riskId: string) =>
      request<RiskDetail>(`/api/v1/facilities/${facilityId}/risks/${riskId}`),
    categories: (facilityId: string) =>
      request<RiskCategoryItem[]>(`/api/v1/facilities/${facilityId}/risks/categories`),
    create: (facilityId: string, body: RiskCreateRequest) =>
      postJson<{ id: string }>(`/api/v1/facilities/${facilityId}/risks`, body),
    executeCommand: (facilityId: string, riskId: string, body: RiskCommandBody) =>
      request<void>(`/api/v1/facilities/${facilityId}/risks/${riskId}/command`, {
        method: 'POST',
        body: JSON.stringify(body),
        headers: { 'Content-Type': 'application/json' },
      }),
    interventions: (facilityId: string, limit = 20) =>
      request<RiskInterventionItem[]>(`/api/v1/facilities/${facilityId}/risks/interventions?limit=${limit}`),
    dataQuality: (facilityId: string) =>
      request<{ issues: RiskDataQualityIssue[]; generatedAtUtc: string }>(`/api/v1/facilities/${facilityId}/risks/data-quality`),
  },

  formCompliance: {
    summary: (filters: FormComplianceFilters = {}) =>
      request<FormComplianceSummary>(`/api/v1/form-compliance/summary?${buildFormComplianceQuery(filters)}`),
    regions: (filters: FormComplianceFilters = {}) =>
      request<Paged<FormComplianceRegionRow>>(`/api/v1/form-compliance/regions?${buildFormComplianceQuery(filters)}`),
    facilities: (filters: FormComplianceFilters = {}) =>
      request<Paged<FormComplianceFacilityRow>>(`/api/v1/form-compliance/facilities?${buildFormComplianceQuery(filters)}`),
    cycles: (filters: FormComplianceFilters = {}) =>
      request<Paged<FormComplianceCycleRow>>(`/api/v1/form-compliance/cycles?${buildFormComplianceQuery(filters)}`),
    pending: (filters: FormComplianceFilters = {}) =>
      request<Paged<FormCompliancePendingItem>>(`/api/v1/form-compliance/pending?${buildFormComplianceQuery(filters)}`),
    trend: (filters: FormComplianceFilters = {}) =>
      request<FormComplianceTrendPoint[]>(`/api/v1/form-compliance/trend?${buildFormComplianceQuery(filters)}`),
    exportCsv: (filters: FormComplianceFilters = {}) =>
      downloadFile(`/api/v1/form-compliance/export.csv?${buildFormComplianceQuery(filters)}`),
  },

  forms: {
    list: (filters: FormListFilters = {}) =>
      request<Paged<FormListItem>>(`/api/v1/forms?${buildFormQuery(filters)}`),
    get: (id: string) => request<FormDetail>(`/api/v1/forms/${id}`),
    create: (body: CreateFormRequest) => postJson<FormDetail>('/api/v1/forms', body),
    update: (id: string, body: UpdateFormRequest) => putJson<FormDetail>(`/api/v1/forms/${id}`, body),
    submitReview: (id: string, body: FormTransitionRequest) =>
      postJson<FormDetail>(`/api/v1/forms/${id}/submit-review`, body),
    requestChanges: (id: string, body: FormTransitionRequest) =>
      postJson<FormDetail>(`/api/v1/forms/${id}/request-changes`, body),
    approve: (id: string, body: FormTransitionRequest) =>
      postJson<FormDetail>(`/api/v1/forms/${id}/approve`, body),
    reject: (id: string, body: FormTransitionRequest) =>
      postJson<FormDetail>(`/api/v1/forms/${id}/reject`, body),
    archive: (id: string, body: FormTransitionRequest) =>
      request<void>(`/api/v1/forms/${id}/archive`, { method: 'POST', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } }),
    restore: (id: string, body: FormTransitionRequest) =>
      request<void>(`/api/v1/forms/${id}/restore`, { method: 'POST', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } }),
    reviewDecisions: (id: string) => request<FormReviewDecision[]>(`/api/v1/forms/${id}/review-decisions`),
    retentionStatus: (id: string) => request<FormRetentionStatus>(`/api/v1/forms/${id}/retention-status`),
    accessGrants: (id: string) => request<FormAccessGrant[]>(`/api/v1/forms/${id}/access-grants`),
    createAccessGrant: (id: string, body: CreateFormAccessGrantRequest) =>
      postJson<FormAccessGrant>(`/api/v1/forms/${id}/access-grants`, body),
    revokeAccessGrant: (id: string, grantId: string, body: FormTransitionRequest) =>
      postJson<void>(`/api/v1/forms/${id}/access-grants/${grantId}/revoke`, body),
    listVersions: (formId: string) =>
      request<FormVersionListItem[]>(`/api/v1/forms/${formId}/versions`),
    getVersion: (formId: string, versionId: string) =>
      request<FormVersionDetail>(`/api/v1/forms/${formId}/versions/${versionId}`),
    createVersion: (formId: string, body: { basedOnVersionId?: string | null } = {}) =>
      postJson<FormVersionDetail>(`/api/v1/forms/${formId}/versions`, body),
    cloneVersion: (formId: string, versionId: string) =>
      postJson<FormVersionDetail>(`/api/v1/forms/${formId}/versions/${versionId}/clone`, {}),
    saveSchema: (formId: string, versionId: string, body: SaveFormSchemaRequest) =>
      putJson<FormVersionDetail>(`/api/v1/forms/${formId}/versions/${versionId}/schema`, body),
    autosaveSchema: (formId: string, versionId: string, body: SaveFormSchemaRequest) =>
      postJson<FormVersionDetail>(`/api/v1/forms/${formId}/versions/${versionId}/autosave`, body),
    validateVersion: (formId: string, versionId: string, body: { schemaJson?: string | null; rowVersion: string }) =>
      postJson<FormVersionValidateResult>(`/api/v1/forms/${formId}/versions/${versionId}/validate`, body),
    submitVersionReview: (formId: string, versionId: string, body: FormVersionTransitionRequest) =>
      postJson<FormVersionDetail>(`/api/v1/forms/${formId}/versions/${versionId}/submit-review`, body),
    requestVersionChanges: (formId: string, versionId: string, body: FormVersionTransitionRequest) =>
      postJson<FormVersionDetail>(`/api/v1/forms/${formId}/versions/${versionId}/request-changes`, body),
    rejectVersion: (formId: string, versionId: string, body: FormVersionTransitionRequest) =>
      postJson<FormVersionDetail>(`/api/v1/forms/${formId}/versions/${versionId}/reject`, body),
    reopenVersion: (formId: string, versionId: string, body: FormVersionTransitionRequest) =>
      postJson<FormVersionDetail>(`/api/v1/forms/${formId}/versions/${versionId}/reopen`, body),
    approveLockVersion: (formId: string, versionId: string, body: FormVersionTransitionRequest) =>
      postJson<FormVersionDetail>(`/api/v1/forms/${formId}/versions/${versionId}/approve-lock`, body),
    getVersionSnapshot: (formId: string, versionId: string) =>
      request<FormSchemaSnapshotDto>(`/api/v1/forms/${formId}/versions/${versionId}/snapshot`),
    getVersionReviewDecisions: (formId: string, versionId: string) =>
      request<FormVersionReviewDecisionDto[]>(`/api/v1/forms/${formId}/versions/${versionId}/review-decisions`),

  },

  formGovernance: {
    getPolicy: () => request<FormGovernancePolicy>('/api/v1/forms/governance-policy'),
    updatePolicy: (body: UpdateFormGovernancePolicyRequest) =>
      putJson<FormGovernancePolicy>('/api/v1/forms/governance-policy', body),
  },

  formTemplates: {
    list: () => request<FormTemplateListItem[]>('/api/v1/form-templates'),
    create: (body: CreateFormTemplateRequest) => postJson<FormTemplateListItem>('/api/v1/form-templates', body),
    createForm: (templateId: string, body: CreateFormFromTemplateRequest) =>
      postJson<FormDetail>(`/api/v1/form-templates/${templateId}/create-form`, body),
  },

  formCampaigns: {
    list: (filters: { page?: number; pageSize?: number; search?: string; status?: number; formDefinitionId?: string } = {}) =>
      request<Paged<FormCampaignListItem>>(`/api/v1/form-campaigns?${buildSimpleQuery(filters)}`),
    get: (id: string) => request<FormCampaignDetail>(`/api/v1/form-campaigns/${id}`),
    create: (body: CreateFormCampaignRequest) => postJson<FormCampaignDetail>('/api/v1/form-campaigns', body),
    update: (id: string, body: UpdateFormCampaignRequest) => putJson<FormCampaignDetail>(`/api/v1/form-campaigns/${id}`, body),
    clone: (id: string) => postJson<FormCampaignDetail>(`/api/v1/form-campaigns/${id}/clone`, {}),
    previewTargets: (id: string) => postJson<FormTargetPreview>(`/api/v1/form-campaigns/${id}/target-preview`, {}),
    publish: (id: string, body: { rowVersion: string }) =>
      postJson<FormCampaignDetail>(`/api/v1/form-campaigns/${id}/publish`, body),
    pause: (id: string, body: { rowVersion: string; reason?: string }) =>
      postJson<FormCampaignDetail>(`/api/v1/form-campaigns/${id}/pause`, body),
    resume: (id: string, body: { rowVersion: string; reason?: string }) =>
      postJson<FormCampaignDetail>(`/api/v1/form-campaigns/${id}/resume`, body),
    cancel: (id: string, body: { rowVersion: string; reason?: string }) =>
      postJson<FormCampaignDetail>(`/api/v1/form-campaigns/${id}/cancel`, body),
    complete: (id: string, body: { rowVersion: string; reason?: string }) =>
      postJson<FormCampaignDetail>(`/api/v1/form-campaigns/${id}/complete`, body),
    cycles: (campaignId: string, filters: { page?: number; pageSize?: number } = {}) =>
      request<Paged<FormCycleListItem>>(`/api/v1/form-campaigns/${campaignId}/cycles?${buildSimpleQuery(filters)}`),
    cycle: (campaignId: string, cycleId: string) =>
      request<FormCycleDetail>(`/api/v1/form-campaigns/${campaignId}/cycles/${cycleId}`),
    assignments: (campaignId: string, cycleId: string, filters: { page?: number; pageSize?: number } = {}) =>
      request<Paged<FacilityAssignment>>(`/api/v1/form-campaigns/${campaignId}/cycles/${cycleId}/assignments?${buildSimpleQuery(filters)}`),
    targetRegions: (filters: { page?: number; pageSize?: number; search?: string } = {}) =>
      request<Paged<FormTargetPreviewFacility>>(`/api/v1/form-campaigns/target-options/regions?${buildSimpleQuery(filters)}`),
    targetFacilities: (filters: { page?: number; pageSize?: number; search?: string; regionId?: string } = {}) =>
      request<Paged<FormTargetPreviewFacility>>(`/api/v1/form-campaigns/target-options/facilities?${buildSimpleQuery(filters)}`),
    schedulePreview: (body: FormCampaignScheduleRequest, timeZoneId?: string) =>
      postJson<string[]>(`/api/v1/form-campaigns/schedule-preview?${buildSimpleQuery({ timeZoneId })}`, body),
  },

  formResponses: {
    workspace: (filters: Record<string, QueryParameterValue> = {}) =>
      request<{ items: FormResponseWorkspaceItem[]; page: number; pageSize: number; totalCount: number }>(
        `/api/v1/form-response-workspace?${buildSimpleQuery(filters)}`),
    getAssignmentResponse: (assignmentId: string) =>
      request<FormResponseWorkspaceDetail>(`/api/v1/form-assignments/${assignmentId}/response`),
    saveDraft: (assignmentId: string, body: {
      answers: Record<string, unknown>
      clientMutationId: string
      expectedDraftVersion: number
      rowVersion?: string | null
    }) => putJson<FormResponseDraftSaveResult>(`/api/v1/form-assignments/${assignmentId}/response/draft`, body),
    validate: (assignmentId: string, body: { answers: Record<string, unknown> }) =>
      postJson<FormResponseDraftSaveResult>(`/api/v1/form-assignments/${assignmentId}/response/validate`, body),
    submit: (assignmentId: string, body: {
      answers: Record<string, unknown>
      clientMutationId: string
      expectedDraftVersion: number
      rowVersion: string
      acknowledged: boolean
      acknowledgementText?: string | null
    }) => postJson<{ responseId: string; submissionId: string; submissionNumber: number; status: number; rowVersion: string }>(
      `/api/v1/form-assignments/${assignmentId}/response/submit`, body),
    reviews: (filters: Record<string, QueryParameterValue> = {}) =>
      request<{ items: FormResponseWorkspaceItem[]; page: number; pageSize: number; totalCount: number }>(
        `/api/v1/form-response-reviews?${buildSimpleQuery(filters)}`),
    getReview: (responseId: string) => request<FormResponseReviewDetail>(`/api/v1/form-responses/${responseId}/review`),
    startReview: (responseId: string, body: { rowVersion: string }) =>
      postJson<void>(`/api/v1/form-responses/${responseId}/review/start`, body),
    returnResponse: (responseId: string, body: { reason: string; newDueAtUtc?: string | null; comments?: Array<{ fieldKey?: string | null; body: string; isVisibleToRespondent: boolean }>; rowVersion: string }) =>
      postJson<void>(`/api/v1/form-responses/${responseId}/return`, body),
    approve: (responseId: string, body: { reason?: string | null; rowVersion: string }) =>
      postJson<void>(`/api/v1/form-responses/${responseId}/approve`, body),
    reject: (responseId: string, body: { reason: string; rowVersion: string }) =>
      postJson<void>(`/api/v1/form-responses/${responseId}/reject`, body),
    close: (responseId: string, body: { reason?: string | null; rowVersion: string }) =>
      postJson<void>(`/api/v1/form-responses/${responseId}/close`, body),
  },
}
