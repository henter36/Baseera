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

const API_BASE = import.meta.env.VITE_API_BASE ?? ''

export type AuthMode = 'test' | 'entra'

export function getAuthMode(): AuthMode {
  return (import.meta.env.VITE_AUTH_MODE as AuthMode) || 'test'
}

let accessTokenProvider: (() => Promise<string | null>) | null = null
let testSubject = localStorage.getItem('baseera.testSubject') ?? 'dev-admin'

export function setAccessTokenProvider(provider: (() => Promise<string | null>) | null) {
  accessTokenProvider = provider
}

export function setTestSubject(subject: string) {
  testSubject = subject
  localStorage.setItem('baseera.testSubject', subject)
}

export function getTestSubject() {
  return testSubject
}

export class ApiError extends Error {
  status: number
  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  if (getAuthMode() === 'test') {
    headers.set('X-Test-User', testSubject)
    headers.set('X-Test-DisplayName', testSubject)
  } else if (accessTokenProvider) {
    const token = await accessTokenProvider()
    if (token) headers.set('Authorization', `Bearer ${token}`)
  }

  const response = await fetch(`${API_BASE}${path}`, { ...init, headers })
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
