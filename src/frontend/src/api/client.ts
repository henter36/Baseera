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
  category: number
  categoryAr: string
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
  category: number
  categoryAr: string
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
  category?: number
  sourceType?: number
  classification?: number
  regionId?: string
  facilityId?: string
  facilityUnitId?: string
  ownerDepartmentId?: string
  assignedToUserId?: string
  overdueOnly?: boolean
  dueFrom?: string
  dueTo?: string
  createdFrom?: string
  createdTo?: string
  sortBy?: string
  sortDesc?: boolean
}

export type CreateNoteRequest = {
  title: string
  description: string
  category: number
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
  category: number
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
  if (filters.category !== undefined) params.set('category', String(filters.category))
  if (filters.sourceType !== undefined) params.set('sourceType', String(filters.sourceType))
  if (filters.classification !== undefined) params.set('classification', String(filters.classification))
  if (filters.overdueOnly) params.set('overdueOnly', 'true')
}

function appendScopeFilterParams(params: URLSearchParams, filters: NoteListFilters): void {
  if (filters.regionId) params.set('regionId', filters.regionId)
  if (filters.facilityId) params.set('facilityId', filters.facilityId)
  if (filters.facilityUnitId) params.set('facilityUnitId', filters.facilityUnitId)
  if (filters.ownerDepartmentId) params.set('ownerDepartmentId', filters.ownerDepartmentId)
  if (filters.assignedToUserId) params.set('assignedToUserId', filters.assignedToUserId)
}

function appendDateRangeParams(params: URLSearchParams, filters: NoteListFilters): void {
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
      request<NoteDetail>('/api/v1/notes', { method: 'POST', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } }),
    update: (id: string, body: UpdateNoteRequest) =>
      request<NoteDetail>(`/api/v1/notes/${id}`, { method: 'PUT', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } }),
    submit: (id: string, body: TransitionNoteRequest) =>
      request<NoteDetail>(`/api/v1/notes/${id}/submit`, { method: 'POST', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } }),
    assign: (id: string, body: AssignNoteRequest) =>
      request<NoteDetail>(`/api/v1/notes/${id}/assign`, { method: 'POST', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } }),
    startWork: (id: string, body: WorkflowActionRequest) =>
      request<NoteDetail>(`/api/v1/notes/${id}/start-work`, { method: 'POST', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } }),
    submitForVerification: (id: string, body: WorkflowActionRequest) =>
      request<NoteDetail>(`/api/v1/notes/${id}/submit-for-verification`, { method: 'POST', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } }),
    returnForRework: (id: string, body: TransitionNoteRequest) =>
      request<NoteDetail>(`/api/v1/notes/${id}/return-for-rework`, { method: 'POST', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } }),
    verifyClosure: (id: string, body: CloseNoteRequest) =>
      request<NoteDetail>(`/api/v1/notes/${id}/verify-closure`, { method: 'POST', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } }),
    reopen: (id: string, body: ReopenNoteRequest) =>
      request<NoteDetail>(`/api/v1/notes/${id}/reopen`, { method: 'POST', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } }),
    cancel: (id: string, body: TransitionNoteRequest) =>
      request<NoteDetail>(`/api/v1/notes/${id}/cancel`, { method: 'POST', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } }),
    archive: (id: string, body: TransitionNoteRequest) =>
      request<void>(`/api/v1/notes/${id}/archive`, { method: 'POST', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } }),
    restore: (id: string, body: TransitionNoteRequest) =>
      request<void>(`/api/v1/notes/${id}/restore`, { method: 'POST', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } }),
    history: (id: string) => request<NoteStatusHistoryEntry[]>(`/api/v1/notes/${id}/history`),
    assignments: (id: string) => request<NoteAssignment[]>(`/api/v1/notes/${id}/assignments`),
    attachments: (id: string) => request<Attachment[]>(`/api/v1/notes/${id}/attachments`),
  },
}
