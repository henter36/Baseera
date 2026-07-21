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

function buildSimpleQuery(filters: Record<string, string | number | boolean | undefined>): string {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(filters)) {
    if (value !== undefined) params.set(key, String(value))
  }
  if (!params.has('page')) params.set('page', '1')
  if (!params.has('pageSize')) params.set('pageSize', '20')
  return params.toString()
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
}
