import { useQuery } from '@tanstack/react-query'
import { Link, useParams } from 'react-router'
import { api } from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import { formatCycleStatusAr, formatRiyadh } from '../../formCampaigns/campaignLabels'
import { listQueryErrorMessage } from '../../shared/listPageUtils'

export function FormCampaignCyclesPage() {
  const canView = usePermission('Forms.View')
  const { campaignId = '' } = useParams()
  const query = useQuery({
    queryKey: ['form-campaign-cycles', campaignId],
    queryFn: () => api.formCampaigns.cycles(campaignId),
    enabled: canView && !!campaignId,
  })

  if (!canView) return <div className="error" role="alert">ليست لديك صلاحية.</div>
  if (query.isLoading) return <div className="loading">جاري التحميل…</div>
  if (query.isError) return <div className="error" role="alert">{listQueryErrorMessage(query.error, 'ليست لديك صلاحية.', 'تعذر إكمال العملية.')}</div>

  return (
    <div className="panel" dir="rtl">
      <div className="page-header">
        <h1>دورات الحملة</h1>
        <Link to={`/form-campaigns/${campaignId}`}>رجوع</Link>
      </div>
      <table>
        <thead>
          <tr>
            <th>#</th>
            <th>Occurrence</th>
            <th>الحالة</th>
            <th>الفتح</th>
            <th>الاستحقاق</th>
            <th>الإغلاق</th>
            <th>المواقع</th>
            <th>Target hash</th>
            <th>الإجراءات</th>
          </tr>
        </thead>
        <tbody>
          {(query.data?.items ?? []).map((c) => (
            <tr key={c.id}>
              <td>{c.sequenceNumber}</td>
              <td>{c.occurrenceKey}</td>
              <td>{formatCycleStatusAr(c.status)}</td>
              <td>{formatRiyadh(c.openAtUtc)}</td>
              <td>{formatRiyadh(c.dueAtUtc)}</td>
              <td>{formatRiyadh(c.closeAtUtc)}</td>
              <td>{c.assignedFacilityCount}</td>
              <td>{c.targetSnapshotHash.slice(0, 10)}…</td>
              <td>
                <Link
                  to={`/form-campaigns/${campaignId}/cycles/${c.id}`}
                  aria-label={`عرض تعيينات الدورة ${c.sequenceNumber}`}
                >
                  التعيينات
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

export function FormCampaignCycleDetailPage() {
  const canView = usePermission('Forms.View')
  const canAssignments = usePermission('Forms.ViewCampaignAssignments')
  const { campaignId = '', cycleId = '' } = useParams()
  const cycle = useQuery({
    queryKey: ['form-cycle', campaignId, cycleId],
    queryFn: () => api.formCampaigns.cycle(campaignId, cycleId),
    enabled: canView && !!campaignId && !!cycleId,
  })
  const assignments = useQuery({
    queryKey: ['form-cycle-assignments', campaignId, cycleId],
    queryFn: () => api.formCampaigns.assignments(campaignId, cycleId),
    enabled: canAssignments && !!campaignId && !!cycleId,
  })

  if (!canView) return <div className="error" role="alert">ليست لديك صلاحية.</div>
  if (cycle.isLoading) return <div className="loading">جاري التحميل…</div>
  if (cycle.isError) return <div className="error" role="alert">{listQueryErrorMessage(cycle.error, 'ليست لديك صلاحية.', 'تعذر إكمال العملية.')}</div>
  if (!cycle.data) return <div className="empty">الدورة غير موجودة.</div>

  return (
    <div className="panel" dir="rtl">
      <div className="page-header">
        <h1>دورة #{cycle.data.sequenceNumber}</h1>
        <Link to={`/form-campaigns/${campaignId}/cycles`}>رجوع</Link>
      </div>
      <div className="detail-grid">
        <div><strong>Occurrence</strong><div>{cycle.data.occurrenceKey}</div></div>
        <div><strong>الحالة</strong><div>{formatCycleStatusAr(cycle.data.status)}</div></div>
        <div><strong>Target snapshot</strong><div>{cycle.data.targetSnapshotHash}</div></div>
        <div><strong>Schema hash</strong><div>{cycle.data.schemaHash}</div></div>
        <div><strong>عدد المواقع المجمدة</strong><div>{cycle.data.assignedFacilityCount}</div></div>
      </div>
      <h2>التعيينات المجمدة</h2>
      {!canAssignments && <div className="muted">لا صلاحية لعرض التعيينات.</div>}
      {canAssignments && assignments.isLoading && <div className="loading">جاري تحميل التعيينات…</div>}
      {canAssignments && assignments.isError && (
        <div className="error" role="alert">{listQueryErrorMessage(assignments.error, 'ليست لديك صلاحية.', 'تعذر إكمال العملية.')}</div>
      )}
      {canAssignments && !assignments.isLoading && !assignments.isError && (assignments.data?.items.length ?? 0) === 0 && (
        <div className="empty">لا توجد تعيينات.</div>
      )}
      {canAssignments && !assignments.isLoading && !assignments.isError && (assignments.data?.items.length ?? 0) > 0 && (
        <table>
          <thead>
            <tr><th>الرمز</th><th>الاسم</th><th>المنطقة</th><th>النوع</th><th>متاح</th></tr>
          </thead>
          <tbody>
            {assignments.data!.items.map((a) => (
              <tr key={a.id}>
                <td>{a.facilityCodeAtAssignment}</td>
                <td>{a.facilityNameArAtAssignment}</td>
                <td>{a.regionNameArAtAssignment}</td>
                <td>{a.facilityTypeAtAssignment ?? '—'}</td>
                <td>{a.isAvailable ? 'نعم' : 'لا'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}

export function FormCampaignPreviewPage() {
  const canPreviewTargets = usePermission('Forms.PreviewTargets')
  const canPublish = usePermission('Forms.Publish')
  const canManage = usePermission('Forms.ManageCampaigns')
  const canPreview = canPreviewTargets || canPublish || canManage
  const { campaignId = '' } = useParams()
  const query = useQuery({
    queryKey: ['form-campaign-preview', campaignId],
    queryFn: () => api.formCampaigns.previewTargets(campaignId),
    enabled: canPreview && !!campaignId,
  })

  if (!canPreview) return <div className="error" role="alert">ليست لديك صلاحية المعاينة.</div>
  if (query.isLoading) return <div className="loading">جاري المعاينة…</div>
  if (query.isError) return <div className="error" role="alert">{listQueryErrorMessage(query.error, 'ليست لديك صلاحية.', 'تعذر إكمال العملية.')}</div>
  if (!query.data) return null

  const p = query.data
  return (
    <div className="panel" dir="rtl">
      <div className="page-header">
        <h1>معاينة الاستهداف</h1>
        <Link to={`/form-campaigns/${campaignId}`}>رجوع</Link>
      </div>
      <div className="detail-grid">
        <div><strong>المطابق</strong><div>{p.totalMatched}</div></div>
        <div><strong>المستثنى</strong><div>{p.totalExcluded}</div></div>
        <div><strong>النهائي</strong><div>{p.finalTargetCount}</div></div>
        <div><strong>البصمة</strong><div>{p.targetingFingerprint.slice(0, 16)}…</div></div>
      </div>
      <h2>حسب المنطقة</h2>
      <ul>{Object.entries(p.breakdownByRegion).map(([k, v]) => <li key={k}>{k}: {v}</li>)}</ul>
      <h2>عينة</h2>
      <ul>{p.sample.map((f) => <li key={f.facilityId}>{f.code} — {f.nameAr}</li>)}</ul>
    </div>
  )
}
