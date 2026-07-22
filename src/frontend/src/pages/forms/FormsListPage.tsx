import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { api, type FormListFilters } from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import {
  ClassificationLevelLabelsAr,
  FormDefinitionStatusLabelsAr,
  classificationTone,
  enumOptions,
  formStatusTone,
} from '../../forms/formEnums'
import { buildFormsListSearchParams } from './formsListSearchParams'
import {
  formatListDate,
  listQueryErrorMessage,
  listSortIndicator,
  nextListSortState,
} from '../../shared/listPageUtils'

const PAGE_SIZE = 20

const SORT_COLUMNS: Array<{ key: string; labelAr: string }> = [
  { key: 'createdAtUtc', labelAr: 'تاريخ الإنشاء' },
  { key: 'nameAr', labelAr: 'الاسم' },
  { key: 'code', labelAr: 'الرمز' },
  { key: 'status', labelAr: 'الحالة' },
]

export function FormsListPage() {
  const canView = usePermission('Forms.View')
  const canCreate = usePermission('Forms.Create')
  const [searchParams, setSearchParams] = useSearchParams()

  const [search, setSearch] = useState(searchParams.get('search') ?? '')
  const [status, setStatus] = useState(searchParams.get('status') ?? '')
  const [classification, setClassification] = useState(searchParams.get('classification') ?? '')
  const [regionId, setRegionId] = useState(searchParams.get('regionId') ?? '')
  const [facilityId, setFacilityId] = useState(searchParams.get('facilityId') ?? '')
  const [page, setPage] = useState(Number(searchParams.get('page') ?? '1') || 1)
  const [sortBy, setSortBy] = useState(searchParams.get('sortBy') ?? 'createdAtUtc')
  const [sortDesc, setSortDesc] = useState(searchParams.get('sortDesc') !== 'false')

  const regionsQuery = useQuery({ queryKey: ['forms-filter-regions'], queryFn: () => api.regions(), enabled: canView })
  const facilitiesQuery = useQuery({
    queryKey: ['forms-filter-facilities', regionId],
    queryFn: () => api.facilities(regionId || undefined),
    enabled: canView && !!regionId,
  })

  const filters = useMemo<FormListFilters>(
    () => ({
      page,
      pageSize: PAGE_SIZE,
      search: search || undefined,
      status: status === '' ? undefined : Number(status),
      classification: classification === '' ? undefined : Number(classification),
      regionId: regionId || undefined,
      facilityId: facilityId || undefined,
      sortBy,
      sortDesc,
    }),
    [page, search, status, classification, regionId, facilityId, sortBy, sortDesc],
  )

  useEffect(() => {
    setSearchParams(buildFormsListSearchParams(filters), { replace: true })
  }, [filters, setSearchParams])

  const query = useQuery({
    queryKey: ['forms', filters],
    queryFn: () => api.forms.list(filters),
    enabled: canView,
    placeholderData: keepPreviousData,
  })

  if (!canView) {
    return <div className="error" role="alert">ليست لديك صلاحية عرض النماذج.</div>
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
    'ليست لديك صلاحية عرض النماذج.',
    'تعذر تحميل النماذج.',
  )

  return (
    <div className="panel" dir="rtl">
      <div className="page-header">
        <div>
          <h1 className="page-title">النماذج</h1>
          <p className="muted">عرض تعريفات النماذج ضمن نطاقك مع الفرز والتصفية.</p>
        </div>
        {canCreate && (
          <Link to="/forms/new">
            <button type="button">نموذج جديد</button>
          </Link>
        )}
      </div>

      <div className="toolbar" role="search">
        <input
          aria-label="بحث النماذج"
          value={search}
          onChange={(e) => { setPage(1); setSearch(e.target.value) }}
          placeholder="بحث بالرمز أو الاسم"
        />
        <select aria-label="الحالة" value={status} onChange={(e) => { setPage(1); setStatus(e.target.value) }}>
          <option value="">كل الحالات</option>
          {enumOptions(FormDefinitionStatusLabelsAr).map((o) => (
            <option key={o.value} value={o.value}>{o.labelAr}</option>
          ))}
        </select>
        <select
          aria-label="مستوى التصنيف الأمني"
          value={classification}
          onChange={(e) => { setPage(1); setClassification(e.target.value) }}
        >
          <option value="">كل مستويات التصنيف</option>
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
      </div>

      {query.isLoading && <div className="loading">جاري التحميل…</div>}

      {query.isError && (
        <div className="error" role="alert">
          <span>{errorMessage}</span>
          <button type="button" className="secondary" onClick={() => query.refetch()}>إعادة المحاولة</button>
        </div>
      )}

      {query.data?.items.length === 0 && !query.isLoading && !query.isError && (
        <div className="empty">لا توجد نماذج مطابقة ضمن نطاقك.</div>
      )}

      {query.data && query.data.items.length > 0 && (
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
                <th>التصنيف</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {query.data.items.map((form) => (
                <tr key={form.id}>
                  <td>{form.code}</td>
                  <td>{form.isSensitiveRedacted ? '[محتوى مقيّد]' : form.nameAr}</td>
                  <td>
                    <span className="badge" data-tone={formStatusTone(form.status)}>{form.statusAr}</span>
                  </td>
                  <td>{formatListDate(form.createdAtUtc)}</td>
                  <td>
                    <span className="badge" data-tone={classificationTone(form.classification)}>
                      {ClassificationLevelLabelsAr[form.classification] ?? form.classification}
                    </span>
                  </td>
                  <td><Link to={`/forms/${form.id}`}>عرض</Link></td>
                </tr>
              ))}
            </tbody>
          </table>

          <div className="pagination">
            <button type="button" className="secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>السابق</button>
            <span className="muted">صفحة {page} من {totalPages} ({query.data.totalCount} نتيجة)</span>
            <button type="button" className="secondary" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>التالي</button>
          </div>
        </>
      )}
    </div>
  )
}
