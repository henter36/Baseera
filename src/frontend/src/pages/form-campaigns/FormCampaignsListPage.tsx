import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api } from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import { FormCampaignStatusLabelsAr, FormRecurrenceKindLabelsAr, formatCycleStatusAr, formatRiyadh } from '../../formCampaigns/campaignLabels'
import { listQueryErrorMessage } from '../../shared/listPageUtils'

export function FormCampaignsListPage() {
  const canView = usePermission('Forms.View')
  const canManage = usePermission('Forms.ManageCampaigns')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)

  const query = useQuery({
    queryKey: ['form-campaigns', search, page],
    queryFn: () => api.formCampaigns.list({ page, pageSize: 20, search: search || undefined }),
    enabled: canView,
    placeholderData: keepPreviousData,
  })

  if (!canView) {
    return <div className="error" role="alert">ليست لديك صلاحية عرض حملات النماذج.</div>
  }

  const items = query.data?.items ?? []
  const showEmpty = query.isSuccess && items.length === 0
  const showTable = query.isSuccess && items.length > 0
  const totalCount = query.data?.totalCount ?? 0
  const pageSize = query.data?.pageSize ?? 20

  return (
    <div className="panel" dir="rtl">
      <div className="page-header">
        <div>
          <h1>حملات نشر النماذج</h1>
          <p className="muted">إدارة النشر والاستهداف والجدولة دون تعبئة الردود.</p>
        </div>
        {canManage && <Link className="button" to="/form-campaigns/new">حملة جديدة</Link>}
      </div>

      <div className="toolbar">
        <div>
          <label htmlFor="form-campaign-search">
            <span>بحث</span>
          </label>
          <input
            id="form-campaign-search"
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1) }}
          />
        </div>
      </div>

      {query.isLoading && <div className="loading">جاري التحميل…</div>}
      {query.isError && <div className="error" role="alert">{listQueryErrorMessage(query.error, 'ليست لديك صلاحية.', 'تعذر إكمال العملية.')}</div>}
      {showEmpty && <div className="empty">لا توجد حملات.</div>}

      {showTable && (
        <table>
          <thead>
            <tr>
              <th>الاسم</th>
              <th>النموذج</th>
              <th>الإصدار</th>
              <th>الحالة</th>
              <th>التكرار</th>
              <th>أول موعد</th>
              <th>التالي</th>
              <th>الدورات</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={item.id}>
                <td><Link to={`/form-campaigns/${item.id}`}>{item.nameAr}</Link></td>
                <td>{item.formNameAr}</td>
                <td>v{item.versionNumber}</td>
                <td>{FormCampaignStatusLabelsAr[item.status] ?? item.status}</td>
                <td>{FormRecurrenceKindLabelsAr[item.recurrenceKind] ?? item.recurrenceKind}</td>
                <td>{formatRiyadh(item.firstOpenAtLocal)}</td>
                <td>{formatRiyadh(item.nextOccurrenceUtc)}</td>
                <td>{item.cycleCount}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {query.isSuccess && totalCount > pageSize && (
        <div className="toolbar">
          <button type="button" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>السابق</button>
          <span>صفحة {page}</span>
          <button type="button" disabled={page * pageSize >= totalCount} onClick={() => setPage((p) => p + 1)}>التالي</button>
        </div>
      )}
    </div>
  )
}

export function FormCampaignDetailPage() {
  const canView = usePermission('Forms.View')
  const { campaignId = '' } = useParams()
  const id = campaignId
  const queryClient = useQueryClient()
  const query = useQuery({
    queryKey: ['form-campaign', id],
    queryFn: () => api.formCampaigns.get(id),
    enabled: canView && !!id,
  })
  const cycles = useQuery({
    queryKey: ['form-campaign-cycles', id],
    queryFn: () => api.formCampaigns.cycles(id),
    enabled: canView && !!id,
  })

  const transition = useMutation({
    mutationFn: async (action: 'pause' | 'resume' | 'cancel' | 'publish' | 'complete' | 'clone') => {
      const rowVersion = query.data?.rowVersion ?? ''
      if (action === 'publish') return api.formCampaigns.publish(id, { rowVersion })
      if (action === 'pause') return api.formCampaigns.pause(id, { rowVersion, reason: 'إيقاف مؤقت' })
      if (action === 'resume') return api.formCampaigns.resume(id, { rowVersion })
      if (action === 'cancel') return api.formCampaigns.cancel(id, { rowVersion, reason: 'إلغاء الحملة' })
      if (action === 'complete') return api.formCampaigns.complete(id, { rowVersion })
      return api.formCampaigns.clone(id)
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['form-campaign', id] })
      void queryClient.invalidateQueries({ queryKey: ['form-campaigns'] })
    },
  })

  if (!canView) return <div className="error" role="alert">ليست لديك صلاحية عرض الحملة.</div>
  if (query.isLoading) return <div className="loading">جاري التحميل…</div>
  if (query.isError) return <div className="error" role="alert">{listQueryErrorMessage(query.error, 'ليست لديك صلاحية.', 'تعذر إكمال العملية.')}</div>
  if (!query.data) return <div className="empty">الحملة غير موجودة.</div>

  const c = query.data
  return (
    <div className="panel" dir="rtl">
      <div className="page-header">
        <div>
          <h1>{c.nameAr}</h1>
          <p className="muted">{c.code} — {FormCampaignStatusLabelsAr[c.status]}</p>
        </div>
        <div className="form-actions">
          {c.allowedActions.includes('edit') && <Link to={`/form-campaigns/${c.id}/edit`}>تعديل</Link>}
          {c.allowedActions.includes('preview') && <Link to={`/form-campaigns/${c.id}/preview`}>معاينة الاستهداف</Link>}
          {c.allowedActions.includes('publish') && (
            <button type="button" onClick={() => { if (confirm('تأكيد نشر الحملة؟')) transition.mutate('publish') }}>نشر</button>
          )}
          {c.allowedActions.includes('pause') && <button type="button" onClick={() => transition.mutate('pause')}>إيقاف</button>}
          {c.allowedActions.includes('resume') && <button type="button" onClick={() => transition.mutate('resume')}>استئناف</button>}
          {c.allowedActions.includes('cancel') && (
            <button type="button" onClick={() => { if (confirm('تأكيد إلغاء الحملة؟')) transition.mutate('cancel') }}>إلغاء</button>
          )}
          {c.allowedActions.includes('clone') && <button type="button" onClick={() => transition.mutate('clone')}>نسخ</button>}
          <Link to={`/form-campaigns/${c.id}/cycles`}>الدورات</Link>
        </div>
      </div>

      {transition.isError && <div className="error" role="alert">{listQueryErrorMessage(transition.error, 'ليست لديك صلاحية.', 'تعذر إكمال العملية.')}</div>}

      <div className="detail-grid">
        <div><strong>النموذج</strong><div>{c.formNameAr} (v{c.versionNumber})</div></div>
        <div><strong>تجزئة المخطط</strong><div>{c.schemaHash.slice(0, 12)}…</div></div>
        <div><strong>التكرار</strong><div>{FormRecurrenceKindLabelsAr[c.recurrenceKind]}</div></div>
        <div><strong>المنطقة الزمنية</strong><div>{c.timeZoneId}</div></div>
        <div><strong>أول موعد</strong><div>{formatRiyadh(c.firstOpenAtLocal)}</div></div>
        <div><strong>الموعد التالي</strong><div>{formatRiyadh(c.nextOccurrenceUtc)}</div></div>
        <div><strong>عدد الدورات</strong><div>{c.cycleCount}</div></div>
        <div><strong>قواعد الاستهداف</strong><div>{c.targets.length}</div></div>
        <div><strong>الاستثناءات</strong><div>{c.exclusions.length}</div></div>
      </div>

      <h2>آخر الدورات</h2>
      {cycles.data?.items.length ? (
        <table>
          <thead>
            <tr><th>#</th><th>الحالة</th><th>الفتح</th><th>المواقع</th><th></th></tr>
          </thead>
          <tbody>
            {cycles.data.items.slice(0, 5).map((cycle) => (
              <tr key={cycle.id}>
                <td>{cycle.sequenceNumber}</td>
                <td>{formatCycleStatusAr(cycle.status)}</td>
                <td>{formatRiyadh(cycle.openAtUtc)}</td>
                <td>{cycle.assignedFacilityCount}</td>
                <td><Link to={`/form-campaigns/${c.id}/cycles/${cycle.id}`}>التفاصيل</Link></td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : <div className="empty">لا دورات بعد.</div>}
    </div>
  )
}
