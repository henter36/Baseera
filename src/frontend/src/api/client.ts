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

export const api = {
  me: () => request<Me>('/api/v1/me'),
  regions: (search = '') =>
    request<Paged<Region>>(`/api/v1/regions?page=1&pageSize=50&search=${encodeURIComponent(search)}`),
  facilities: (regionId?: string, search = '') => {
    const params = new URLSearchParams({ page: '1', pageSize: '50', search })
    if (regionId) params.set('regionId', regionId)
    return request<Paged<Facility>>(`/api/v1/facilities?${params}`)
  },
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
    return request('/api/v1/attachments', { method: 'POST', body: form })
  },
}
