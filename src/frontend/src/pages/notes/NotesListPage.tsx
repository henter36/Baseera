import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { api, type NoteListFilters } from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import { buildNotesListSearchParams } from './notesListSearchParams'
import { formatListDate, listQueryErrorMessage, listSortIndicator, nextListSortState } from '../../shared/listPageUtils'
import {
  ClassificationLevelLabelsAr,
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

export function NotesListPage() {
  const canView = usePermission('Notes.View')
  const canCreate = usePermission('Notes.Create')
  const canRestore = usePermission('Notes.Restore')
  const [searchParams, setSearchParams] = useSearchParams()

  const [search, setSearch] = useState(searchParams.get('search') ?? '')
  const [status, setStatus] = useState(searchParams.get('status') ?? '')
  const [severity, setSeverity] = useState(searchParams.get('severity') ?? '')
  const [noteTypeId, setNoteTypeId] = useState(searchParams.get('noteTypeId') ?? '')
  const [requiresMyAction, setRequiresMyAction] = useState(searchParams.get('requiresMyAction') === 'true')
  const [requiresRouting, setRequiresRouting] = useState(searchParams.get('requiresRouting') === 'true')
  const [classification, setClassification] = useState(searchParams.get('classification') ?? '')
  const [regionId, setRegionId] = useState(searchParams.get('regionId') ?? '')
  const [facilityId, setFacilityId] = useState(searchParams.get('facilityId') ?? '')
  const [facilityUnitId, setFacilityUnitId] = useState(searchParams.get('facilityUnitId') ?? '')
  const [ownerDepartmentId, setOwnerDepartmentId] = useState(searchParams.get('ownerDepartmentId') ?? '')
  const [overdueOnly, setOverdueOnly] = useState(searchParams.get('overdueOnly') === 'true')
  const dueSoonDays = searchParams.get('dueSoonDays') ?? ''
  const unassignedOnly = searchParams.get('unassignedOnly') === 'true'
  const [page, setPage] = useState(Number(searchParams.get('page') ?? '1') || 1)
  const [sortBy, setSortBy] = useState(searchParams.get('sortBy') ?? 'createdAtUtc')
  const [sortDesc, setSortDesc] = useState(searchParams.get('sortDesc') !== 'false')

  const [restoreId, setRestoreId] = useState('')
  const [restoreRowVersion, setRestoreRowVersion] = useState('')
  const [restoreReason, setRestoreReason] = useState('')
  const [restoreMessage, setRestoreMessage] = useState<string | null>(null)

  const regionsQuery = useQuery({ queryKey: ['notes-filter-regions'], queryFn: () => api.regions(), enabled: canView })
  const noteTypesQuery = useQuery({ queryKey: ['notes-filter-note-types'], queryFn: () => api.myNoteTypes(), enabled: canView })
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
      noteTypeId: noteTypeId || undefined,
      requiresMyAction: requiresMyAction || undefined,
      requiresRouting: requiresRouting || undefined,
      classification: classification === '' ? undefined : Number(classification),
      regionId: regionId || undefined,
      facilityId: facilityId || undefined,
      facilityUnitId: facilityUnitId || undefined,
      ownerDepartmentId: ownerDepartmentId || undefined,
      overdueOnly: overdueOnly || undefined,
      dueSoonDays: dueSoonDays === '' ? undefined : Number(dueSoonDays),
      unassignedOnly: unassignedOnly || undefined,
      sortBy,
      sortDesc,
    }),
    [page, search, status, severity, noteTypeId, requiresMyAction, requiresRouting, classification, regionId, facilityId, facilityUnitId, ownerDepartmentId, overdueOnly, dueSoonDays, unassignedOnly, sortBy, sortDesc],
  )

  useEffect(() => {
    setSearchParams(buildNotesListSearchParams(filters), { replace: true })
  }, [filters, setSearchParams])

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
    const next = nextListSortState(sortBy, sortDesc, key)
    setSortBy(next.sortBy)
    setSortDesc(next.sortDesc)
  }

  const errorMessage = listQueryErrorMessage(
    query.error,
    'ليست لديك صلاحية عرض الملاحظات.',
    'تعذر تحميل الملاحظات.',
  )

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

      {noteTypesQuery.data && (
        <div className="tabs" role="tablist" aria-label="أنواع الملاحظات">
          {noteTypesQuery.data.length > 1 && (
            <button
              type="button"
              role="tab"
              aria-selected={!noteTypeId && !requiresMyAction && !requiresRouting}
              className={!noteTypeId && !requiresMyAction && !requiresRouting ? 'active' : undefined}
              onClick={() => { setPage(1); setNoteTypeId(''); setRequiresMyAction(false); setRequiresRouting(false) }}
            >
              الكل
            </button>
          )}
          {noteTypesQuery.data.map((type) => (
            <button
              type="button"
              role="tab"
              key={type.id}
              aria-selected={noteTypeId === type.id}
              className={noteTypeId === type.id ? 'active' : undefined}
              onClick={() => { setPage(1); setNoteTypeId(type.id); setRequiresMyAction(false); setRequiresRouting(false) }}
              title={type.descriptionAr || undefined}
            >
              {type.nameAr}{!type.isActive ? ' (غير فعال)' : ''}
            </button>
          ))}
          <button
            type="button"
            role="tab"
            aria-selected={requiresMyAction}
            className={requiresMyAction ? 'active' : undefined}
            onClick={() => { setPage(1); setRequiresMyAction(true); setRequiresRouting(false); setNoteTypeId('') }}
          >
            تتطلب إجراء مني
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={requiresRouting}
            className={requiresRouting ? 'active' : undefined}
            onClick={() => { setPage(1); setRequiresRouting(true); setRequiresMyAction(false); setNoteTypeId('') }}
          >
            تتطلب توجيهًا
          </button>
        </div>
      )}

      {query.isLoading && <div className="loading">جاري التحميل…</div>}

      {query.isError && (
        <div className="error" role="alert">
          <span>{errorMessage}</span>
          <button type="button" className="secondary" onClick={() => query.refetch()}>
            إعادة المحاولة
          </button>
        </div>
      )}

      {query.data?.items.length === 0 && (
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
                      {col.labelAr} {listSortIndicator(col.key, sortBy, sortDesc)}
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
                  <td>{note.noteTypeNameAr}{!note.noteTypeIsActive ? ' (غير فعال)' : ''}</td>
                  <td>
                    {formatListDate(note.dueAtUtc)}
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
          {restoreMessage && <output className="muted">{restoreMessage}</output>}
        </div>
      )}
    </div>
  )
}
