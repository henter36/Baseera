import { useQuery } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  api,
  ApiError,
  type DashboardOperationsFilters,
} from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import { severityTone, statusTone } from '../../notes/noteEnums'
import { buildCorrectiveActionsDrillDown, buildNotesDrillDown } from './dashboardDrillDown'

const PERIOD_OPTIONS = [
  { value: 7, label: '7 أيام' },
  { value: 30, label: '30 يومًا' },
  { value: 90, label: '90 يومًا' },
]

function MetricCard({
  label,
  value,
  to,
  tone,
}: {
  label: string
  value: number
  to?: string
  tone?: string
}) {
  const content = (
    <div className={`metric-card${tone ? ` metric-card--${tone}` : ''}`}>
      <strong aria-label={`${label}: ${value}`}>{value}</strong>
      <span>{label}</span>
    </div>
  )
  return to ? <Link to={to} className="metric-card-link">{content}</Link> : content
}

export function OperationalDashboardPage() {
  const canOperational = usePermission('Dashboard.ViewOperational')
  const canRisk = usePermission('Dashboard.ViewRisk')
  const canRouting = usePermission('Dashboard.ViewRouting')
  const canCorrectiveActions = usePermission('Dashboard.ViewCorrectiveActions')
  const canView = canOperational || canRisk || canRouting || canCorrectiveActions

  const [periodDays, setPeriodDays] = useState(30)
  const [regionId, setRegionId] = useState('')
  const [facilityId, setFacilityId] = useState('')
  const [noteTypeId, setNoteTypeId] = useState('')
  const [severity, setSeverity] = useState('')
  const [status, setStatus] = useState('')
  const [breakdownBy, setBreakdownBy] = useState('1')

  const filters = useMemo<DashboardOperationsFilters>(() => ({
    periodDays,
    regionId: regionId || undefined,
    facilityId: facilityId || undefined,
    noteTypeId: noteTypeId || undefined,
    severity: severity === '' ? undefined : Number(severity),
    status: status === '' ? undefined : Number(status),
    breakdownBy: Number(breakdownBy),
  }), [periodDays, regionId, facilityId, noteTypeId, severity, status, breakdownBy])

  const regionsQuery = useQuery({ queryKey: ['dashboard-regions'], queryFn: () => api.regions(), enabled: canView })
  const noteTypesQuery = useQuery({ queryKey: ['dashboard-note-types'], queryFn: () => api.myNoteTypes(), enabled: canView })
  const facilitiesQuery = useQuery({
    queryKey: ['dashboard-facilities', regionId],
    queryFn: () => api.facilities(regionId || undefined),
    enabled: canView,
  })

  const summaryQuery = useQuery({
    queryKey: ['dashboard-summary', filters],
    queryFn: () => api.dashboard.operations.summary(filters),
    enabled: canView,
  })

  const trendsQuery = useQuery({
    queryKey: ['dashboard-trends', filters],
    queryFn: () => api.dashboard.operations.trends(filters),
    enabled: canView && canOperational,
  })

  const breakdownsQuery = useQuery({
    queryKey: ['dashboard-breakdowns', filters],
    queryFn: () => api.dashboard.operations.breakdowns(filters),
    enabled: canView && canOperational,
  })

  const queuesQuery = useQuery({
    queryKey: ['dashboard-queues', filters],
    queryFn: () => api.dashboard.operations.priorityQueues(filters),
    enabled: canView,
  })

  if (!canView) {
    return <div className="error" role="alert">ليست لديك صلاحية عرض لوحة المتابعة التشغيلية.</div>
  }

  const loading = summaryQuery.isLoading || trendsQuery.isLoading || breakdownsQuery.isLoading || queuesQuery.isLoading
  const error = [summaryQuery, trendsQuery, breakdownsQuery, queuesQuery].find((q) => q.isError)?.error
  const errorMessage = error instanceof ApiError ? error.message : error ? 'تعذر تحميل لوحة المتابعة.' : null
  const summary = summaryQuery.data
  const trends = trendsQuery.data
  const breakdowns = breakdownsQuery.data
  const queues = queuesQuery.data
  const maxTrend = Math.max(1, ...(trends?.points.map((p) => Math.max(p.notesCreated, p.notesCompleted, p.routingFailure)) ?? [1]))

  const drillBase = filters

  return (
    <div className="panel" dir="rtl">
      <div className="page-header">
        <div>
          <h1 className="page-title">لوحة المتابعة التشغيلية</h1>
          <p className="muted">مؤشرات حقيقية مبنية على بيانات الملاحظات والإجراءات والتوجيه ضمن نطاقك وصلاحياتك.</p>
        </div>
      </div>

      <section className="panel-section filters" aria-label="فلاتر اللوحة">
        <div className="toolbar">
          <label>
            الفترة
            <select value={periodDays} onChange={(e) => setPeriodDays(Number(e.target.value))} aria-label="الفترة">
              {PERIOD_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </label>
          <label>
            المنطقة
            <select value={regionId} onChange={(e) => { setRegionId(e.target.value); setFacilityId('') }} aria-label="المنطقة">
              <option value="">الكل</option>
              {(regionsQuery.data?.items ?? []).map((region) => (
                <option key={region.id} value={region.id}>{region.nameAr}</option>
              ))}
            </select>
          </label>
          <label>
            الموقع
            <select value={facilityId} onChange={(e) => setFacilityId(e.target.value)} aria-label="الموقع">
              <option value="">الكل</option>
              {(facilitiesQuery.data?.items ?? []).map((facility) => (
                <option key={facility.id} value={facility.id}>{facility.nameAr}</option>
              ))}
            </select>
          </label>
          <label>
            نوع الملاحظة
            <select value={noteTypeId} onChange={(e) => setNoteTypeId(e.target.value)} aria-label="نوع الملاحظة">
              <option value="">الكل</option>
              {(noteTypesQuery.data ?? []).map((type) => (
                <option key={type.id} value={type.id}>{type.nameAr}</option>
              ))}
            </select>
          </label>
          <label>
            الخطورة
            <select value={severity} onChange={(e) => setSeverity(e.target.value)} aria-label="الخطورة">
              <option value="">الكل</option>
              <option value="3">حرجة</option>
              <option value="2">عالية</option>
              <option value="1">متوسطة</option>
              <option value="0">منخفضة</option>
            </select>
          </label>
          <label>
            الحالة
            <select value={status} onChange={(e) => setStatus(e.target.value)} aria-label="الحالة">
              <option value="">الكل</option>
              <option value="1">مفتوحة</option>
              <option value="2">مسندة</option>
              <option value="3">قيد المعالجة</option>
              <option value="4">بانتظار التحقق</option>
              <option value="6">معاد فتحها</option>
            </select>
          </label>
          <label>
            التقسيم
            <select value={breakdownBy} onChange={(e) => setBreakdownBy(e.target.value)} aria-label="التقسيم">
              <option value="0">حسب المنطقة</option>
              <option value="1">حسب الموقع</option>
              <option value="2">حسب نوع الملاحظة</option>
              <option value="3">حسب الخطورة</option>
              <option value="4">حسب الحالة</option>
            </select>
          </label>
        </div>
      </section>

      {loading && <div className="loading">جاري تحميل لوحة المتابعة…</div>}
      {errorMessage && (
        <div className="error" role="alert">
          <span>{errorMessage}</span>
          <button type="button" className="secondary" onClick={() => {
            void summaryQuery.refetch()
            void trendsQuery.refetch()
            void breakdownsQuery.refetch()
            void queuesQuery.refetch()
          }}>إعادة المحاولة</button>
        </div>
      )}

      {!loading && !errorMessage && summary && (
        <>
          {canOperational && summary.workload && (
            <section className="panel-section" aria-labelledby="workload-heading">
              <h2 id="workload-heading" className="section-title">العبء التشغيلي</h2>
              <div className="cards-grid">
                <MetricCard label="إجمالي المفتوحة" value={summary.workload.openTotal} to={buildNotesDrillDown(drillBase)} />
                <MetricCard label="مسندة" value={summary.workload.assigned} to={buildNotesDrillDown({ ...drillBase, status: 2 })} />
                <MetricCard label="قيد المعالجة" value={summary.workload.inProgress} to={buildNotesDrillDown({ ...drillBase, status: 3 })} />
                <MetricCard label="بانتظار التحقق" value={summary.workload.pendingVerification} to={buildNotesDrillDown({ ...drillBase, status: 4 })} />
                <MetricCard label="معاد فتحها" value={summary.workload.reopened} to={buildNotesDrillDown({ ...drillBase, status: 6 })} />
                <MetricCard label="غير مكلفة" value={summary.workload.unassigned} to={buildNotesDrillDown({ ...drillBase, unassignedOnly: true })} />
                <MetricCard label="تتطلب توجيهًا" value={summary.workload.requiresRouting} to={buildNotesDrillDown({ ...drillBase, requiresRouting: true })} tone="warn" />
              </div>
            </section>
          )}

          {canRisk && summary.risk && (
            <section className="panel-section" aria-labelledby="risk-heading">
              <h2 id="risk-heading" className="section-title">المخاطر والتأخر</h2>
              <div className="cards-grid">
                <MetricCard label="متأخرة" value={summary.risk.overdue} to={buildNotesDrillDown({ ...drillBase, overdueOnly: true, sortBy: 'dueAtUtc' })} tone="danger" />
                <MetricCard label="قريبة الاستحقاق" value={summary.risk.dueSoon} to={buildNotesDrillDown({ ...drillBase, dueSoonDays: summary.dueSoonDays })} tone="warn" />
                <MetricCard label="حرجة/عالية" value={summary.risk.criticalOrHigh} to={buildNotesDrillDown({ ...drillBase, severity: 3 })} tone="danger" />
                <MetricCard label="متأخرة بلا تكليف" value={summary.risk.overdueUnassigned} to={buildNotesDrillDown({ ...drillBase, overdueOnly: true, unassignedOnly: true })} tone="danger" />
                <MetricCard label="تصعيدات نشطة" value={summary.risk.activeEscalations} />
                <MetricCard label="فشل: لا قاعدة" value={summary.risk.routingFailureNoRule} to={buildNotesDrillDown({ ...drillBase, requiresRouting: true })} />
                <MetricCard label="فشل: لا مستخدم" value={summary.risk.routingFailureNoEligibleUser} to={buildNotesDrillDown({ ...drillBase, requiresRouting: true })} />
                <MetricCard label="فشل: هدف غير صالح" value={summary.risk.routingFailureInvalidTarget} to={buildNotesDrillDown({ ...drillBase, requiresRouting: true })} />
              </div>
            </section>
          )}

          {canCorrectiveActions && summary.correctiveActions && (
            <section className="panel-section" aria-labelledby="ca-heading">
              <h2 id="ca-heading" className="section-title">الإجراءات التصحيحية</h2>
              <div className="cards-grid">
                <MetricCard label="نشطة" value={summary.correctiveActions.active} to={buildCorrectiveActionsDrillDown(drillBase)} />
                <MetricCard label="متأخرة" value={summary.correctiveActions.overdue} to={buildCorrectiveActionsDrillDown({ ...drillBase, overdueOnly: true, sortBy: 'dueAtUtc' })} tone="danger" />
                <MetricCard label="بانتظار التحقق" value={summary.correctiveActions.pendingVerification} />
                <MetricCard label="معاد للعمل" value={summary.correctiveActions.reopened} />
                <MetricCard label="ملاحظات بإجراءات متعثرة" value={summary.correctiveActions.notesWithStalledActions} tone="warn" />
              </div>
            </section>
          )}

          {canRouting && summary.routing && (
            <section className="panel-section" aria-labelledby="routing-heading">
              <h2 id="routing-heading" className="section-title">التوجيه الآلي</h2>
              <div className="cards-grid">
                <MetricCard label="تتطلب توجيهًا" value={summary.routing.requiresRouting} to={buildNotesDrillDown({ ...drillBase, requiresRouting: true })} />
                <MetricCard label="لا قاعدة" value={summary.routing.failureNoRule} />
                <MetricCard label="لا مستخدم مؤهل" value={summary.routing.failureNoEligibleUser} />
                <MetricCard label="هدف غير صالح" value={summary.routing.failureInvalidTarget} />
              </div>
            </section>
          )}

          {canOperational && trends && trends.points.length > 0 && (
            <section className="panel-section" aria-labelledby="trends-heading">
              <h2 id="trends-heading" className="section-title">الاتجاهات الزمنية</h2>
              <div className="trend-grid" role="img" aria-label="مخطط اتجاهات الملاحظات والتوجيه">
                {trends.points.map((point) => (
                  <div key={point.bucketStartUtc} className="trend-item">
                    <div className="trend-label">{point.labelAr}</div>
                    <div className="trend-bars">
                      <div className="trend-bar trend-bar--created" style={{ height: `${(point.notesCreated / maxTrend) * 100}%` }} title={`منشأة: ${point.notesCreated}`} />
                      <div className="trend-bar trend-bar--completed" style={{ height: `${(point.notesCompleted / maxTrend) * 100}%` }} title={`مكتملة: ${point.notesCompleted}`} />
                      <div className="trend-bar trend-bar--failure" style={{ height: `${(point.routingFailure / maxTrend) * 100}%` }} title={`فشل توجيه: ${point.routingFailure}`} />
                    </div>
                  </div>
                ))}
              </div>
            </section>
          )}

          {canOperational && breakdowns && breakdowns.rows.length > 0 && (
            <section className="panel-section" aria-labelledby="breakdown-heading">
              <h2 id="breakdown-heading" className="section-title">التقسيمات</h2>
              <table>
                <thead>
                  <tr>
                    <th scope="col">البعد</th>
                    <th scope="col">العبء المفتوح</th>
                    <th scope="col">المتأخر</th>
                    <th scope="col">الحرج</th>
                    <th scope="col">غير مكلف</th>
                    <th scope="col">إجراءات متأخرة</th>
                    <th scope="col">نسبة الإغلاق</th>
                  </tr>
                </thead>
                <tbody>
                  {breakdowns.rows.map((row) => (
                    <tr key={row.key}>
                      <td>{row.labelAr}</td>
                      <td>{row.openBurden}</td>
                      <td>{row.overdue}</td>
                      <td>{row.critical}</td>
                      <td>{row.unassigned}</td>
                      <td>{row.correctiveActionsOverdue}</td>
                      <td>{row.closureRateWithinDue == null ? '—' : `${Math.round(row.closureRateWithinDue * 100)}%`}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </section>
          )}

          {queues && (
            <section className="panel-section" aria-labelledby="queues-heading">
              <h2 id="queues-heading" className="section-title">قوائم الأولوية</h2>
              {(queues.mostOverdueNotes?.length ?? 0) === 0 &&
                (queues.criticalUnassignedNotes?.length ?? 0) === 0 &&
                (queues.mostOverdueCorrectiveActions?.length ?? 0) === 0 &&
                (queues.recentRoutingFailures?.length ?? 0) === 0 && (
                <div className="empty">لا توجد عناصر في قوائم الأولوية للفلاتر الحالية.</div>
              )}

              {(queues.mostOverdueNotes?.length ?? 0) > 0 && (
                <>
                  <h3 className="section-title">أكثر الملاحظات تأخرًا</h3>
                  <table>
                    <thead><tr><th scope="col">المرجع</th><th scope="col">العنوان</th><th scope="col">الخطورة</th><th scope="col">الحالة</th><th scope="col">الاستحقاق</th></tr></thead>
                    <tbody>
                      {queues.mostOverdueNotes!.map((item) => (
                        <tr key={item.id}>
                          <td><Link to={`/notes/${item.id}`}>{item.referenceNumber}</Link></td>
                          <td>{item.title}</td>
                          <td><span className="badge" data-tone={severityTone(item.severity)}>{item.severityAr}</span></td>
                          <td><span className="badge" data-tone={statusTone(item.status)}>{item.statusAr}</span></td>
                          <td>{item.dueAtUtc ? new Date(item.dueAtUtc).toLocaleString('ar-SA', { timeZone: 'Asia/Riyadh' }) : '—'}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </>
              )}

              {(queues.criticalUnassignedNotes?.length ?? 0) > 0 && (
                <>
                  <h3 className="section-title">حرجة غير مكلفة</h3>
                  <table>
                    <thead><tr><th scope="col">المرجع</th><th scope="col">العنوان</th><th scope="col">الخطورة</th><th scope="col">الاستحقاق</th></tr></thead>
                    <tbody>
                      {queues.criticalUnassignedNotes!.map((item) => (
                        <tr key={item.id}>
                          <td><Link to={`/notes/${item.id}`}>{item.referenceNumber}</Link></td>
                          <td>{item.title}</td>
                          <td><span className="badge" data-tone={severityTone(item.severity)}>{item.severityAr}</span></td>
                          <td>{item.dueAtUtc ? new Date(item.dueAtUtc).toLocaleString('ar-SA', { timeZone: 'Asia/Riyadh' }) : '—'}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </>
              )}

              {(queues.topOverdueLocations?.length ?? 0) > 0 && (
                <>
                  <h3 className="section-title">المواقع الأعلى تأخرًا</h3>
                  <table>
                    <thead><tr><th scope="col">الموقع</th><th scope="col">عدد المتأخر</th><th scope="col">عرض</th></tr></thead>
                    <tbody>
                      {queues.topOverdueLocations!.map((item) => (
                        <tr key={item.facilityId}>
                          <td>{item.facilityNameAr}</td>
                          <td>{item.overdueCount}</td>
                          <td><Link to={buildNotesDrillDown({ ...drillBase, facilityId: item.facilityId, overdueOnly: true })}>القائمة</Link></td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </>
              )}

              {(queues.mostOverdueCorrectiveActions?.length ?? 0) > 0 && (
                <>
                  <h3 className="section-title">الإجراءات الأكثر تأخرًا</h3>
                  <table>
                    <thead><tr><th scope="col">المرجع</th><th scope="col">الملاحظة</th><th scope="col">العنوان</th><th scope="col">الاستحقاق</th></tr></thead>
                    <tbody>
                      {queues.mostOverdueCorrectiveActions!.map((item) => (
                        <tr key={item.id}>
                          <td><Link to={`/corrective-actions/${item.id}`}>{item.referenceNumber}</Link></td>
                          <td><Link to={`/notes/${item.operationalNoteId}`}>{item.noteReferenceNumber}</Link></td>
                          <td>{item.title}</td>
                          <td>{item.dueAtUtc ? new Date(item.dueAtUtc).toLocaleString('ar-SA', { timeZone: 'Asia/Riyadh' }) : '—'}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </>
              )}

              {(queues.recentRoutingFailures?.length ?? 0) > 0 && (
                <>
                  <h3 className="section-title">أحدث حالات فشل التوجيه</h3>
                  <table>
                    <thead><tr><th scope="col">المرجع</th><th scope="col">السبب</th><th scope="col">التاريخ</th></tr></thead>
                    <tbody>
                      {queues.recentRoutingFailures!.map((item) => (
                        <tr key={`${item.noteId}-${item.decidedAtUtc}`}>
                          <td><Link to={`/notes/${item.noteId}`}>{item.referenceNumber}</Link></td>
                          <td>{item.failureMessageSafe}</td>
                          <td>{new Date(item.decidedAtUtc).toLocaleString('ar-SA', { timeZone: 'Asia/Riyadh' })}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </>
              )}
            </section>
          )}
        </>
      )}
    </div>
  )
}
