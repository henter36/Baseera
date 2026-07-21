import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { api, ApiError, type CorrectiveActionListFilters } from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import {
  CorrectiveActionPriorityLabelsAr,
  CorrectiveActionStatusLabelsAr,
  correctiveActionPriorityTone,
  correctiveActionStatusTone,
  enumOptions,
} from '../../correctiveActions/correctiveActionEnums'
import { ClassificationLevelLabelsAr } from '../../notes/noteEnums'

const PAGE_SIZE = 20

const SORT_COLUMNS = [
  { key: 'createdAtUtc', labelAr: 'تاريخ الإنشاء' },
  { key: 'dueAtUtc', labelAr: 'تاريخ الاستحقاق' },
  { key: 'priority', labelAr: 'الأولوية' },
  { key: 'status', labelAr: 'الحالة' },
  { key: 'referenceNumber', labelAr: 'الرقم المرجعي' },
  { key: 'title', labelAr: 'العنوان' },
]

function formatDate(value?: string | null): string {
  if (!value) return '—'
  return new Date(value).toLocaleString('ar-SA', { timeZone: 'Asia/Riyadh' })
}

function sortIndicator(columnKey: string, sortBy: string, sortDesc: boolean): string {
  if (sortBy !== columnKey) return ''
  return sortDesc ? '↓' : '↑'
}

export function CorrectiveActionsListPage() {
  const canView = usePermission('CorrectiveActions.View')
  const [searchParams, setSearchParams] = useSearchParams()
  const noteId = searchParams.get('noteId') ?? ''
  const [search, setSearch] = useState(searchParams.get('search') ?? '')
  const [status, setStatus] = useState(searchParams.get('status') ?? '')
  const [priority, setPriority] = useState(searchParams.get('priority') ?? '')
  const [classification, setClassification] = useState(searchParams.get('classification') ?? '')
  const [regionId, setRegionId] = useState(searchParams.get('regionId') ?? '')
  const [facilityId, setFacilityId] = useState(searchParams.get('facilityId') ?? '')
  const [facilityUnitId, setFacilityUnitId] = useState(searchParams.get('facilityUnitId') ?? '')
  const [ownerDepartmentId, setOwnerDepartmentId] = useState(searchParams.get('ownerDepartmentId') ?? '')
  const [assignedToUserId, setAssignedToUserId] = useState(searchParams.get('assignedToUserId') ?? '')
  const [overdueOnly, setOverdueOnly] = useState(searchParams.get('overdueOnly') === 'true')
  const [dueSoonDays, setDueSoonDays] = useState(searchParams.get('dueSoonDays') ?? '')
  const [page, setPage] = useState(Number(searchParams.get('page') ?? '1') || 1)
  const [sortBy, setSortBy] = useState(searchParams.get('sortBy') ?? 'createdAtUtc')
  const [sortDesc, setSortDesc] = useState(searchParams.get('sortDesc') !== 'false')

  const regionsQuery = useQuery({ queryKey: ['ca-filter-regions'], queryFn: () => api.regions(), enabled: canView })
  const facilitiesQuery = useQuery({
    queryKey: ['ca-filter-facilities', regionId],
    queryFn: () => api.facilities(regionId || undefined),
    enabled: canView,
  })

  const filters = useMemo<CorrectiveActionListFilters>(
    () => ({
      page,
      pageSize: PAGE_SIZE,
      search: search || undefined,
      noteId: noteId || undefined,
      status: status === '' ? undefined : Number(status),
      priority: priority === '' ? undefined : Number(priority),
      classification: classification === '' ? undefined : Number(classification),
      regionId: regionId || undefined,
      facilityId: facilityId || undefined,
      facilityUnitId: facilityUnitId || undefined,
      ownerDepartmentId: ownerDepartmentId || undefined,
      assignedToUserId: assignedToUserId || undefined,
      overdueOnly: overdueOnly || undefined,
      dueSoonDays: dueSoonDays === '' ? undefined : Number(dueSoonDays),
      sortBy,
      sortDesc,
    }),
    [assignedToUserId, classification, dueSoonDays, facilityId, facilityUnitId, noteId, ownerDepartmentId, overdueOnly, page, priority, regionId, search, sortBy, sortDesc, status],
  )

  useEffect(() => {
    const params = new URLSearchParams()
    if (search) params.set('search', search)
    if (status !== '') params.set('status', status)
    if (priority !== '') params.set('priority', priority)
    if (classification !== '') params.set('classification', classification)
    if (regionId) params.set('regionId', regionId)
    if (facilityId) params.set('facilityId', facilityId)
    if (facilityUnitId) params.set('facilityUnitId', facilityUnitId)
    if (ownerDepartmentId) params.set('ownerDepartmentId', ownerDepartmentId)
    if (assignedToUserId) params.set('assignedToUserId', assignedToUserId)
    if (noteId) params.set('noteId', noteId)
    if (overdueOnly) params.set('overdueOnly', 'true')
    if (dueSoonDays !== '') params.set('dueSoonDays', dueSoonDays)
    if (page > 1) params.set('page', String(page))
    if (sortBy !== 'createdAtUtc') params.set('sortBy', sortBy)
    if (!sortDesc) params.set('sortDesc', 'false')
    setSearchParams(params, { replace: true })
  }, [assignedToUserId, classification, dueSoonDays, facilityId, facilityUnitId, noteId, overdueOnly, ownerDepartmentId, page, priority, regionId, search, setSearchParams, sortBy, sortDesc, status])

  const query = useQuery({
    queryKey: ['corrective-actions', filters],
    queryFn: () => api.correctiveActions.list(filters),
    enabled: canView,
    placeholderData: keepPreviousData,
  })

  if (!canView) return <div className="error" role="alert">ليست لديك صلاحية عرض الإجراءات التصحيحية.</div>

  const totalPages = query.data ? Math.max(1, Math.ceil(query.data.totalCount / PAGE_SIZE)) : 1
  const visibleItems = query.data?.items ?? []
  const errorMessage = (() => {
    if (!query.error) return null
    const err = query.error as ApiError
    if (err.status === 403) return 'ليست لديك صلاحية عرض الإجراءات التصحيحية.'
    return err.message || 'تعذر تحميل الإجراءات التصحيحية.'
  })()

  const toggleSort = (key: string) => {
    setPage(1)
    if (sortBy === key) setSortDesc((d) => !d)
    else {
      setSortBy(key)
      setSortDesc(true)
    }
  }

  return (
    <div className="panel">
      <div className="page-header">
        <div>
          <h1 className="page-title">الإجراءات التصحيحية</h1>
          <p className="muted">قائمة تشغيلية مرتبطة بالملاحظات ومرشحة من الخادم ضمن نطاقك.</p>
          {noteId && <p className="muted">معروضة لملاحظة محددة: {noteId}</p>}
        </div>
      </div>

      <div className="toolbar" role="search">
        <input aria-label="بحث الإجراءات" value={search} onChange={(e) => { setPage(1); setSearch(e.target.value) }} placeholder="بحث بالرقم أو العنوان" />
        <select aria-label="حالة الإجراء" value={status} onChange={(e) => { setPage(1); setStatus(e.target.value) }}>
          <option value="">كل الحالات</option>
          {enumOptions(CorrectiveActionStatusLabelsAr).map((o) => <option key={o.value} value={o.value}>{o.labelAr}</option>)}
        </select>
        <select aria-label="أولوية الإجراء" value={priority} onChange={(e) => { setPage(1); setPriority(e.target.value) }}>
          <option value="">كل الأولويات</option>
          {enumOptions(CorrectiveActionPriorityLabelsAr).map((o) => <option key={o.value} value={o.value}>{o.labelAr}</option>)}
        </select>
        <select aria-label="تصنيف الإجراء" value={classification} onChange={(e) => { setPage(1); setClassification(e.target.value) }}>
          <option value="">كل مستويات التصنيف</option>
          {enumOptions(ClassificationLevelLabelsAr).map((o) => <option key={o.value} value={o.value}>{o.labelAr}</option>)}
        </select>
        <select aria-label="منطقة الإجراء" value={regionId} onChange={(e) => { setPage(1); setRegionId(e.target.value); setFacilityId('') }}>
          <option value="">كل المناطق</option>
          {regionsQuery.data?.items.map((r) => <option key={r.id} value={r.id}>{r.nameAr}</option>)}
        </select>
        <select aria-label="سجن الإجراء" value={facilityId} onChange={(e) => { setPage(1); setFacilityId(e.target.value) }}>
          <option value="">كل السجون</option>
          {facilitiesQuery.data?.items.map((f) => <option key={f.id} value={f.id}>{f.nameAr}</option>)}
        </select>
        <input aria-label="وحدة الإجراء" value={facilityUnitId} onChange={(e) => { setPage(1); setFacilityUnitId(e.target.value) }} placeholder="معرف الوحدة" />
        <input aria-label="إدارة الإجراء" value={ownerDepartmentId} onChange={(e) => { setPage(1); setOwnerDepartmentId(e.target.value) }} placeholder="معرف الإدارة" />
        <input aria-label="مستخدم مكلف" value={assignedToUserId} onChange={(e) => { setPage(1); setAssignedToUserId(e.target.value) }} placeholder="معرف المستخدم المكلّف" />
        <input aria-label="مستحق قريبًا" type="number" min="1" value={dueSoonDays} onChange={(e) => { setPage(1); setDueSoonDays(e.target.value) }} placeholder="مستحق خلال أيام" />
        <label className="checkbox-field">
          <input type="checkbox" checked={overdueOnly} onChange={(e) => { setPage(1); setOverdueOnly(e.target.checked) }} />
          <span>المتأخرة فقط</span>
        </label>
      </div>

      {query.isLoading && <div className="loading">جاري التحميل…</div>}
      {query.isError && (
        <div className="error" role="alert">
          <span>{errorMessage}</span>
          <button type="button" className="secondary" onClick={() => query.refetch()}>إعادة المحاولة</button>
        </div>
      )}
      {query.data?.items.length === 0 && <div className="empty">لا توجد إجراءات تصحيحية مطابقة ضمن نطاقك.</div>}

      {query.data && visibleItems.length > 0 && (
        <>
          <table>
            <thead>
              <tr>
                {SORT_COLUMNS.map((col) => (
                  <th key={col.key}>
                    <button type="button" className="sort-header" onClick={() => toggleSort(col.key)}>
                      {col.labelAr} {sortIndicator(col.key, sortBy, sortDesc)}
                    </button>
                  </th>
                ))}
                <th>الملاحظة</th>
                <th>المكلّف الحالي</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {visibleItems.map((item) => (
                <tr key={item.id}>
                  <td>{item.referenceNumber}</td>
                  <td>{item.title}</td>
                  <td><span className="badge" data-tone={correctiveActionStatusTone(item.status)}>{item.statusAr}</span></td>
                  <td><span className="badge" data-tone={correctiveActionPriorityTone(item.priority)}>{item.priorityAr}</span></td>
                  <td>
                    {formatDate(item.dueAtUtc)}
                    {item.isOverdue && <span className="badge" data-tone="danger" style={{ marginRight: '0.35rem' }}>{item.overdueDays ?? 0} يوم</span>}
                  </td>
                  <td>{ClassificationLevelLabelsAr[item.classification] ?? item.classification}</td>
                  <td><Link to={`/notes/${item.operationalNoteId}`}>{item.operationalNoteReferenceNumber || 'الملاحظة'}</Link></td>
                  <td>{item.currentAssigneeDisplay || '—'}</td>
                  <td><Link to={`/corrective-actions/${item.id}`}>عرض</Link></td>
                </tr>
              ))}
            </tbody>
          </table>

          <div className="pagination">
            <button type="button" className="secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>السابق</button>
            <span>صفحة {page} من {totalPages}</span>
            <button type="button" className="secondary" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>التالي</button>
          </div>
        </>
      )}
    </div>
  )
}
