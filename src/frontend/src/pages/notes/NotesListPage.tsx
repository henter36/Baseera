import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { api, ApiError, type NoteListFilters } from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import {
  ClassificationLevelLabelsAr,
  NoteCategoryLabelsAr,
  NoteSeverityLabelsAr,
  NoteStatusLabelsAr,
  enumOptions,
  severityTone,
  statusTone,
} from '../../notes/noteEnums'

const PAGE_SIZE = 20

const SORT_COLUMNS: Array<{ key: string; labelAr: string }> = [
  { key: 'createdAtUtc', labelAr: 'تاريخ الإنشاء' },
  { key: 'dueAtUtc', labelAr: 'تاريخ الاستحقاق' },
  { key: 'severity', labelAr: 'الخطورة' },
  { key: 'status', labelAr: 'الحالة' },
  { key: 'referenceNumber', labelAr: 'الرقم المرجعي' },
  { key: 'title', labelAr: 'العنوان' },
]

function formatDate(value?: string | null): string {
  if (!value) return '—'
  return new Date(value).toLocaleString('ar-SA')
}

export function NotesListPage() {
  const canView = usePermission('Notes.View')
  const canCreate = usePermission('Notes.Create')
  const canRestore = usePermission('Notes.Restore')

  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [severity, setSeverity] = useState('')
  const [category, setCategory] = useState('')
  const [classification, setClassification] = useState('')
  const [regionId, setRegionId] = useState('')
  const [facilityId, setFacilityId] = useState('')
  const [facilityUnitId, setFacilityUnitId] = useState('')
  const [ownerDepartmentId, setOwnerDepartmentId] = useState('')
  const [overdueOnly, setOverdueOnly] = useState(false)
  const [page, setPage] = useState(1)
  const [sortBy, setSortBy] = useState('createdAtUtc')
  const [sortDesc, setSortDesc] = useState(true)

  const [restoreId, setRestoreId] = useState('')
  const [restoreRowVersion, setRestoreRowVersion] = useState('')
  const [restoreReason, setRestoreReason] = useState('')
  const [restoreMessage, setRestoreMessage] = useState<string | null>(null)

  const regionsQuery = useQuery({ queryKey: ['notes-filter-regions'], queryFn: () => api.regions(), enabled: canView })
  const facilitiesQuery = useQuery({
    queryKey: ['notes-filter-facilities', regionId],
    queryFn: () => api.facilities(regionId || undefined),
    enabled: canView,
  })

  const filters = useMemo<NoteListFilters>(
    () => ({
      page,
      pageSize: PAGE_SIZE,
      search: search || undefined,
      status: status === '' ? undefined : Number(status),
      severity: severity === '' ? undefined : Number(severity),
      category: category === '' ? undefined : Number(category),
      classification: classification === '' ? undefined : Number(classification),
      regionId: regionId || undefined,
      facilityId: facilityId || undefined,
      facilityUnitId: facilityUnitId || undefined,
      ownerDepartmentId: ownerDepartmentId || undefined,
      overdueOnly: overdueOnly || undefined,
      sortBy,
      sortDesc,
    }),
    [page, search, status, severity, category, classification, regionId, facilityId, facilityUnitId, ownerDepartmentId, overdueOnly, sortBy, sortDesc],
  )

  const query = useQuery({
    queryKey: ['notes', filters],
    queryFn: () => api.notes.list(filters),
    enabled: canView,
    placeholderData: keepPreviousData,
  })

  const visibleItems = query.data?.items ?? []

  if (!canView) {
    return <div className="error" role="alert">ليست لديك صلاحية عرض الملاحظات.</div>
  }

  const totalPages = query.data ? Math.max(1, Math.ceil(query.data.totalCount / PAGE_SIZE)) : 1

  const toggleSort = (key: string) => {
    setPage(1)
    if (sortBy === key) {
      setSortDesc((d) => !d)
    } else {
      setSortBy(key)
      setSortDesc(true)
    }
  }

  const errorMessage = (() => {
    if (!query.error) return null
    const err = query.error as ApiError
    if (err.status === 403) return 'ليست لديك صلاحية عرض الملاحظات.'
    return err.message || 'تعذر تحميل الملاحظات.'
  })()

  return (
    <div className="panel">
      <div className="page-header">
        <div>
          <h1 className="page-title">الملاحظات</h1>
          <p className="muted">عرض الملاحظات التشغيلية ضمن نطاقك مع الفرز والتصفية.</p>
        </div>
        {canCreate && (
          <Link to="/notes/new">
            <button type="button">ملاحظة جديدة</button>
          </Link>
        )}
      </div>

      <div className="toolbar" role="search">
        <input
          aria-label="بحث الملاحظات"
          value={search}
          onChange={(e) => {
            setPage(1)
            setSearch(e.target.value)
          }}
          placeholder="بحث بالرقم المرجعي أو العنوان"
        />
        <select aria-label="الحالة" value={status} onChange={(e) => { setPage(1); setStatus(e.target.value) }}>
          <option value="">كل الحالات</option>
          {enumOptions(NoteStatusLabelsAr).map((o) => (
            <option key={o.value} value={o.value}>{o.labelAr}</option>
          ))}
        </select>
        <select aria-label="مستوى الخطورة" value={severity} onChange={(e) => { setPage(1); setSeverity(e.target.value) }}>
          <option value="">كل درجات الخطورة</option>
          {enumOptions(NoteSeverityLabelsAr).map((o) => (
            <option key={o.value} value={o.value}>{o.labelAr}</option>
          ))}
        </select>
        <select aria-label="التصنيف" value={category} onChange={(e) => { setPage(1); setCategory(e.target.value) }}>
          <option value="">كل التصنيفات</option>
          {enumOptions(NoteCategoryLabelsAr).map((o) => (
            <option key={o.value} value={o.value}>{o.labelAr}</option>
          ))}
        </select>
        <select
          aria-label="مستوى التصنيف الأمني"
          value={classification}
          onChange={(e) => { setPage(1); setClassification(e.target.value) }}
        >
          <option value="">كل مستويات التصنيف الأمني</option>
          {enumOptions(ClassificationLevelLabelsAr).map((o) => (
            <option key={o.value} value={o.value}>{o.labelAr}</option>
          ))}
        </select>
        <select aria-label="المنطقة" value={regionId} onChange={(e) => { setPage(1); setRegionId(e.target.value); setFacilityId('') }}>
          <option value="">كل المناطق</option>
          {regionsQuery.data?.items.map((r) => (
            <option key={r.id} value={r.id}>{r.nameAr}</option>
          ))}
        </select>
        <select aria-label="السجن" value={facilityId} onChange={(e) => { setPage(1); setFacilityId(e.target.value) }}>
          <option value="">كل السجون</option>
          {facilitiesQuery.data?.items.map((f) => (
            <option key={f.id} value={f.id}>{f.nameAr}</option>
          ))}
        </select>
        <input
          aria-label="معرف الوحدة"
          value={facilityUnitId}
          onChange={(e) => { setPage(1); setFacilityUnitId(e.target.value) }}
          placeholder="معرف الوحدة (UUID)"
        />
        <input
          aria-label="معرف الإدارة المسؤولة"
          value={ownerDepartmentId}
          onChange={(e) => { setPage(1); setOwnerDepartmentId(e.target.value) }}
          placeholder="معرف الإدارة المسؤولة (UUID)"
        />
        <label className="checkbox-field">
          <input
            type="checkbox"
            checked={overdueOnly}
            onChange={(e) => { setPage(1); setOverdueOnly(e.target.checked) }}
          />
          <span>المتأخرة فقط</span>
        </label>
      </div>

      {query.isLoading && <div className="loading">جاري التحميل…</div>}

      {query.isError && (
        <div className="error" role="alert">
          <span>{errorMessage}</span>
          <button type="button" className="secondary" onClick={() => query.refetch()}>
            إعادة المحاولة
          </button>
        </div>
      )}

      {query.data && query.data.items.length === 0 && (
        <div className="empty">لا توجد ملاحظات مطابقة ضمن نطاقك.</div>
      )}

      {query.data && visibleItems.length > 0 && (
        <>
          <table>
            <thead>
              <tr>
                {SORT_COLUMNS.map((col) => (
                  <th key={col.key}>
                    <button type="button" className="sort-header" onClick={() => toggleSort(col.key)}>
                      {col.labelAr} {sortBy === col.key ? (sortDesc ? '↓' : '↑') : ''}
                    </button>
                  </th>
                ))}
                <th>الاستحقاق</th>
                <th>المكلَّف</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {visibleItems.map((note) => (
                <tr key={note.id}>
                  <td>{note.referenceNumber}</td>
                  <td>{note.title}</td>
                  <td>
                    <span className="badge" data-tone={statusTone(note.status)}>{note.statusAr}</span>
                  </td>
                  <td>
                    <span className="badge" data-tone={severityTone(note.severity)}>{note.severityAr}</span>
                  </td>
                  <td>{note.categoryAr}</td>
                  <td>
                    {formatDate(note.dueAtUtc)}
                    {note.isOverdue && <span className="badge" data-tone="danger" style={{ marginRight: '0.35rem' }}>متأخرة</span>}
                  </td>
                  <td>{note.currentAssigneeDisplay || '—'}</td>
                  <td>
                    <Link to={`/notes/${note.id}`}>عرض</Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          <div className="pagination">
            <button type="button" className="secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
              السابق
            </button>
            <span className="muted">صفحة {page} من {totalPages} ({query.data.totalCount} نتيجة)</span>
            <button type="button" className="secondary" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
              التالي
            </button>
          </div>
        </>
      )}

      {canRestore && (
        <div className="panel-section">
          <h2 className="section-title">استعادة ملاحظة مؤرشفة</h2>
          <p className="muted">
            الملاحظات المؤرشفة لا تظهر في القائمة أعلاه ولا يمكن الوصول إليها عبر البحث (لا توجد نقطة نهاية لعرضها).
            أدخل معرفها ونسخة السجل (RowVersion) المسجّلة عند أرشفتها لاستعادتها.
          </p>
          <div className="toolbar">
            <input aria-label="معرف الملاحظة للاستعادة" value={restoreId} onChange={(e) => setRestoreId(e.target.value)} placeholder="معرف الملاحظة (UUID)" />
            <input aria-label="نسخة السجل" value={restoreRowVersion} onChange={(e) => setRestoreRowVersion(e.target.value)} placeholder="RowVersion" />
            <input aria-label="سبب الاستعادة" value={restoreReason} onChange={(e) => setRestoreReason(e.target.value)} placeholder="سبب الاستعادة" />
            <button
              type="button"
              disabled={!restoreId || !restoreRowVersion || !restoreReason}
              onClick={async () => {
                setRestoreMessage(null)
                try {
                  await api.notes.restore(restoreId, { reason: restoreReason, rowVersion: restoreRowVersion })
                  setRestoreMessage('تمت الاستعادة بنجاح.')
                  setRestoreId('')
                  setRestoreRowVersion('')
                  setRestoreReason('')
                  query.refetch()
                } catch (err) {
                  setRestoreMessage(err instanceof Error ? err.message : 'تعذرت الاستعادة.')
                }
              }}
            >
              استعادة
            </button>
          </div>
          {restoreMessage && <div className="muted" role="status">{restoreMessage}</div>}
        </div>
      )}
    </div>
  )
}
