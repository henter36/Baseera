import { useQuery } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import {
  api,
  ApiError,
  type FormComplianceFilters,
  type FormComplianceSummary,
} from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import { FormCycleStatusLabelsAr } from '../../formCampaigns/campaignLabels'
import { FormAssignmentWorkStatusLabelsAr, FormResponseStatusLabelsAr } from '../form-responses/responseLabels'

const completionBasisLabels: Record<number, string> = { 0: 'عند الإرسال', 1: 'عند الاعتماد' }

function numberAr(value: number | null | undefined): string {
  if (value === null || value === undefined) return '—'
  return new Intl.NumberFormat('ar-SA').format(value)
}

function percentAr(value: number | null | undefined): string {
  if (value === null || value === undefined) return '—'
  return `${new Intl.NumberFormat('ar-SA', { maximumFractionDigits: 2 }).format(value)}%`
}

function dateAr(value: string | null | undefined): string {
  if (!value) return '—'
  return new Intl.DateTimeFormat('ar-SA', {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: 'Asia/Riyadh',
  }).format(new Date(value))
}

function minutesAr(value: number | null | undefined): string {
  if (value === null || value === undefined) return '—'
  return `${numberAr(Math.round(value))} دقيقة`
}

function errorMessage(error: unknown): string | null {
  if (error instanceof ApiError) return error.message
  if (error) return 'تعذر تحميل لوحة التزام النماذج.'
  return null
}

function useUrlFilters(defaults: FormComplianceFilters = {}) {
  const [params, setParams] = useSearchParams()
  const filters = useMemo<FormComplianceFilters>(() => {
    const readBool = (key: string) => params.has(key) ? params.get(key) === 'true' : undefined
    const readNum = (key: string) => params.has(key) ? Number(params.get(key)) : undefined
    return {
      ...defaults,
      fromUtc: params.get('fromUtc') ?? undefined,
      toUtc: params.get('toUtc') ?? undefined,
      formDefinitionId: params.get('formDefinitionId') ?? undefined,
      campaignId: params.get('campaignId') ?? undefined,
      cycleId: params.get('cycleId') ?? defaults.cycleId,
      regionId: params.get('regionId') ?? defaults.regionId,
      facilityId: params.get('facilityId') ?? defaults.facilityId,
      cycleStatus: readNum('cycleStatus'),
      completionBasis: readNum('completionBasis'),
      responseStatus: readNum('responseStatus'),
      isCompleted: readBool('isCompleted'),
      isOverdue: readBool('isOverdue'),
      isAvailable: readBool('isAvailable'),
      search: params.get('search') ?? undefined,
      page: readNum('page') ?? 1,
      pageSize: readNum('pageSize') ?? 20,
    }
  }, [defaults, params])
  const update = (patch: FormComplianceFilters) => {
    const next = new URLSearchParams(params)
    for (const [key, value] of Object.entries(patch)) {
      if (value === undefined || value === '' || value === null) next.delete(key)
      else next.set(key, String(value))
    }
    if (!('page' in patch)) next.set('page', '1')
    setParams(next)
  }
  const reset = () => setParams(new URLSearchParams())
  return { filters, update, reset }
}

function MetricCard({ label, value, help }: { label: string; value: string; help?: string }) {
  return (
    <div className="metric-card" title={help}>
      <strong aria-label={`${label}: ${value}`}>{value}</strong>
      <span>{label}</span>
    </div>
  )
}

function SummaryCards({ summary }: { summary: FormComplianceSummary }) {
  return (
    <section className="panel-section" aria-labelledby="summary-heading">
      <h2 id="summary-heading" className="section-title">ملخص الالتزام</h2>
      {!summary.statusReconciliationValid && (
        <div className="error" role="alert">تحذير: مجموع فئات الحالة لا يطابق عدد التكليفات المؤهلة.</div>
      )}
      <div className="cards-grid">
        <MetricCard label="المواقع المستهدفة" value={numberAr(summary.targetedAssignmentCount)} help="عدد أزواج الدورة والموقع المستهدفة." />
        <MetricCard label="المواقع المؤهلة" value={numberAr(summary.eligibleAssignmentCount)} help="المستهدف ناقص غير المتاح." />
        <MetricCard label="المكتملة" value={numberAr(summary.completedCount)} />
        <MetricCard label="المتبقية" value={numberAr(summary.remainingCount)} />
        <MetricCard label="نسبة الالتزام" value={percentAr(summary.completionRate)} />
        <MetricCard label="لم يبدأ" value={numberAr(summary.notStartedCount)} />
        <MetricCard label="مسودة" value={numberAr(summary.draftCount)} />
        <MetricCard label="مرسل وقيد المراجعة" value={numberAr(summary.submittedCount + summary.underReviewCount)} />
        <MetricCard label="معاد" value={numberAr(summary.returnedCount)} />
        <MetricCard label="معتمد" value={numberAr(summary.approvedCount)} />
        <MetricCard label="متأخر" value={numberAr(summary.overdueCount)} help="مؤشر متداخل مع الحالات وليس جزءًا من مجموعها." />
        <MetricCard label="متوسط زمن الإكمال" value={minutesAr(summary.averageCompletionMinutes)} />
      </div>
      <p className="muted">آخر تحديث: {dateAr(summary.generatedAtUtc)}. غير المتاح: {numberAr(summary.unavailableAssignmentCount)}. المواقع المختلفة: {numberAr(summary.distinctFacilityCount)}.</p>
    </section>
  )
}

function FiltersPanel({ filters, update, reset }: {
  filters: FormComplianceFilters
  update: (patch: FormComplianceFilters) => void
  reset: () => void
}) {
  const regionsQuery = useQuery({ queryKey: ['form-compliance-regions-options'], queryFn: () => api.regions() })
  const facilitiesQuery = useQuery({
    queryKey: ['form-compliance-facilities-options', filters.regionId],
    queryFn: () => api.facilities(filters.regionId),
  })
  return (
    <section className="panel-section filters" aria-label="فلاتر لوحة الالتزام">
      <div className="toolbar">
        <label><span>الفترة من</span><input aria-label="الفترة من" type="datetime-local" onChange={(e) => update({ fromUtc: e.target.value ? new Date(e.target.value).toISOString() : undefined })} /></label>
        <label><span>الفترة إلى</span><input aria-label="الفترة إلى" type="datetime-local" onChange={(e) => update({ toUtc: e.target.value ? new Date(e.target.value).toISOString() : undefined })} /></label>
        <label><span>المنطقة</span><select value={filters.regionId ?? ''} onChange={(e) => update({ regionId: e.target.value || undefined, facilityId: undefined })} aria-label="المنطقة"><option value="">الكل</option>{(regionsQuery.data?.items ?? []).map((r) => <option key={r.id} value={r.id}>{r.nameAr}</option>)}</select></label>
        <label><span>الموقع</span><select value={filters.facilityId ?? ''} onChange={(e) => update({ facilityId: e.target.value || undefined })} aria-label="الموقع"><option value="">الكل</option>{(facilitiesQuery.data?.items ?? []).map((f) => <option key={f.id} value={f.id}>{f.nameAr}</option>)}</select></label>
        <label><span>حالة الدورة</span><select value={filters.cycleStatus ?? ''} onChange={(e) => update({ cycleStatus: e.target.value === '' ? undefined : Number(e.target.value) })} aria-label="حالة الدورة"><option value="">كل غير الملغاة</option><option value="0">مجدولة</option><option value="1">مفتوحة</option><option value="2">فترة سماح</option><option value="3">مغلقة</option><option value="4">ملغاة</option></select></label>
        <label><span>سياسة الإكمال</span><select value={filters.completionBasis ?? ''} onChange={(e) => update({ completionBasis: e.target.value === '' ? undefined : Number(e.target.value) })} aria-label="سياسة الإكمال"><option value="">الكل</option><option value="0">عند الإرسال</option><option value="1">عند الاعتماد</option></select></label>
        <label><span>حالة الرد</span><select value={filters.responseStatus ?? ''} onChange={(e) => update({ responseStatus: e.target.value === '' ? undefined : Number(e.target.value) })} aria-label="حالة الرد"><option value="">الكل</option>{Object.entries(FormResponseStatusLabelsAr).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select></label>
        <label><span>مكتمل/متبقٍ</span><select value={filters.isCompleted === undefined ? '' : String(filters.isCompleted)} onChange={(e) => update({ isCompleted: e.target.value === '' ? undefined : e.target.value === 'true' })} aria-label="مكتمل أو متبق"><option value="">الكل</option><option value="true">مكتمل</option><option value="false">متبقٍ</option></select></label>
        <label><span>متأخر</span><select value={filters.isOverdue === undefined ? '' : String(filters.isOverdue)} onChange={(e) => update({ isOverdue: e.target.value === '' ? undefined : e.target.value === 'true' })} aria-label="متأخر"><option value="">الكل</option><option value="true">متأخر</option><option value="false">غير متأخر</option></select></label>
        <label><span>متاح/غير متاح</span><select value={filters.isAvailable === undefined ? '' : String(filters.isAvailable)} onChange={(e) => update({ isAvailable: e.target.value === '' ? undefined : e.target.value === 'true' })} aria-label="متاح أو غير متاح"><option value="">الكل</option><option value="true">متاح</option><option value="false">غير متاح</option></select></label>
        <label><span>بحث</span><input value={filters.search ?? ''} onChange={(e) => update({ search: e.target.value || undefined })} aria-label="بحث" /></label>
        <button type="button" className="secondary" onClick={reset}>إعادة ضبط الفلاتر</button>
      </div>
    </section>
  )
}

export function FormCompliancePage() {
  const canView = usePermission('Forms.ViewComplianceDashboard')
  const canExport = usePermission('Forms.ExportComplianceDashboard')
  const params = useParams()
  const defaults = useMemo<FormComplianceFilters>(() => ({
    regionId: params.regionId,
    facilityId: params.facilityId,
    cycleId: params.cycleId,
  }), [params.cycleId, params.facilityId, params.regionId])
  const { filters, update, reset } = useUrlFilters(defaults)
  const [trendGroupBy, setTrendGroupBy] = useState(0)
  const summaryQuery = useQuery({ queryKey: ['form-compliance-summary', filters], queryFn: () => api.formCompliance.summary(filters), enabled: canView })
  const regionsQuery = useQuery({ queryKey: ['form-compliance-regions', filters], queryFn: () => api.formCompliance.regions(filters), enabled: canView })
  const facilitiesQuery = useQuery({ queryKey: ['form-compliance-facilities', filters], queryFn: () => api.formCompliance.facilities(filters), enabled: canView })
  const cyclesQuery = useQuery({ queryKey: ['form-compliance-cycles', filters], queryFn: () => api.formCompliance.cycles(filters), enabled: canView })
  const pendingQuery = useQuery({ queryKey: ['form-compliance-pending', filters], queryFn: () => api.formCompliance.pending(filters), enabled: canView })
  const trendQuery = useQuery({ queryKey: ['form-compliance-trend', filters, trendGroupBy], queryFn: () => api.formCompliance.trend({ ...filters, groupBy: trendGroupBy }), enabled: canView })
  const exportCsv = async (view: number) => {
    const file = await api.formCompliance.exportCsv({ ...filters, view })
    const url = URL.createObjectURL(file.blob)
    const a = document.createElement('a')
    a.href = url
    a.download = file.fileName
    a.click()
    URL.revokeObjectURL(url)
  }
  if (!canView) return <div className="error" role="alert">ليست لديك صلاحية عرض لوحة التزام النماذج.</div>
  const loading = summaryQuery.isLoading || regionsQuery.isLoading || facilitiesQuery.isLoading || cyclesQuery.isLoading || pendingQuery.isLoading
  const error = [summaryQuery, regionsQuery, facilitiesQuery, cyclesQuery, pendingQuery, trendQuery].find((q) => q.isError)?.error
  return (
    <div className="panel" dir="rtl">
      <div className="page-header">
        <div>
          <h1 className="page-title">لوحة التزام النماذج</h1>
          <p className="muted">مؤشرات مبنية على تكليفات الدورات وردود النماذج ضمن نطاق المستخدم الحالي.</p>
        </div>
        {canExport && <button type="button" onClick={() => void exportCsv(0)}>تصدير CSV</button>}
      </div>
      <FiltersPanel filters={filters} update={update} reset={reset} />
      {loading && <div className="loading">جاري تحميل لوحة الالتزام…</div>}
      {errorMessage(error) && <div className="error" role="alert">{errorMessage(error)}</div>}
      {!loading && !error && summaryQuery.data && <SummaryCards summary={summaryQuery.data} />}
      <section className="panel-section" aria-labelledby="regions-heading">
        <h2 id="regions-heading" className="section-title">المناطق</h2>
        <table><thead><tr><th>الترتيب</th><th>المنطقة</th><th>الالتزام</th><th>المكتمل/المقام</th><th>متأخر</th><th>لم يبدأ</th><th>معاد</th><th>متوسط الزمن</th><th>تفصيل</th></tr></thead><tbody>
          {(regionsQuery.data?.items ?? []).map((row) => <tr key={row.regionIdAtAssignment}><td>{numberAr(row.rank)}</td><td>{row.regionNameAtAssignment}</td><td><progress value={row.completedCount} max={Math.max(row.eligibleAssignmentCount, 1)} /> {percentAr(row.completionRate)}</td><td>{numberAr(row.completedCount)} / {numberAr(row.eligibleAssignmentCount)}</td><td>{numberAr(row.overdueCount)}</td><td>{numberAr(row.notStartedCount)}</td><td>{numberAr(row.returnedCount)}</td><td>{minutesAr(row.averageCompletionMinutes)}</td><td><Link to={`/form-compliance/regions/${row.regionIdAtAssignment}`}>فتح</Link></td></tr>)}
        </tbody></table>
        {(regionsQuery.data?.items ?? []).length === 0 && <div className="empty">لا توجد بيانات مناطق ضمن الفلاتر الحالية.</div>}
      </section>
      <section className="panel-section" aria-labelledby="facilities-heading">
        <h2 id="facilities-heading" className="section-title">المواقع</h2>
        <table><thead><tr><th>الموقع</th><th>المنطقة</th><th>الدورات</th><th>الالتزام</th><th>متأخر</th><th>آخر موعد</th><th>المسؤول</th></tr></thead><tbody>
          {(facilitiesQuery.data?.items ?? []).map((row) => <tr key={row.facilityId}><td><Link to={`/form-compliance/facilities/${row.facilityId}`}>{row.facilityNameAtAssignment}</Link></td><td>{row.regionNameAtAssignment}</td><td>{numberAr(row.cycleCount)}</td><td>{numberAr(row.completedCount)} / {numberAr(row.eligibleAssignmentCount)} ({percentAr(row.completionRate)})</td><td>{numberAr(row.overdueCount)}</td><td>{dateAr(row.latestEffectiveDueAtUtc)}</td><td>{row.responsibleUserName ?? 'غير محدد'}</td></tr>)}
        </tbody></table>
      </section>
      <section className="panel-section" aria-labelledby="cycles-heading">
        <h2 id="cycles-heading" className="section-title">الدورات</h2>
        <table><thead><tr><th>الحملة</th><th>الدورة</th><th>الحالة</th><th>السياسة</th><th>الالتزام</th><th>متأخر</th><th>فرق الدورة السابقة</th></tr></thead><tbody>
          {(cyclesQuery.data?.items ?? []).map((row) => <tr key={row.cycleId}><td>{row.campaignNameAr}</td><td><Link to={`/form-compliance/cycles/${row.cycleId}`}>{row.occurrenceKey}</Link></td><td>{FormCycleStatusLabelsAr[row.cycleStatus] ?? row.cycleStatus}</td><td>{completionBasisLabels[row.completionBasis] ?? row.completionBasis}</td><td>{percentAr(row.completionRate)}</td><td>{numberAr(row.overdueCount)}</td><td>{percentAr(row.completionRateDelta)}</td></tr>)}
        </tbody></table>
      </section>
      <section className="panel-section" aria-labelledby="trend-heading">
        <h2 id="trend-heading" className="section-title">الاتجاه</h2>
        <select value={trendGroupBy} onChange={(e) => setTrendGroupBy(Number(e.target.value))} aria-label="تجميع الاتجاه"><option value="0">حسب الدورة</option><option value="1">حسب اليوم</option></select>
        <table><thead><tr><th>الفترة</th><th>المكتمل</th><th>المقام</th><th>النسبة</th><th>متأخر</th></tr></thead><tbody>
          {(trendQuery.data ?? []).map((row, index) => <tr key={`${row.occurrenceUtc ?? row.dateLocal}-${index}`}><td>{row.dateLocal ?? dateAr(row.occurrenceUtc)}</td><td>{numberAr(row.completedThatDay ?? row.completedCount)}</td><td>{numberAr(row.eligibleAssignmentCount)}</td><td>{percentAr(row.cumulativeCompletionRate ?? row.completionRate)}</td><td>{numberAr(row.overdueCount)}</td></tr>)}
        </tbody></table>
      </section>
      <section className="panel-section" aria-labelledby="pending-heading">
        <h2 id="pending-heading" className="section-title">قائمة المتبقي</h2>
        <table><thead><tr><th>الموقع</th><th>المنطقة</th><th>الحملة</th><th>الدورة</th><th>الحالة</th><th>الموعد</th><th>أيام التأخر</th><th>آخر حفظ</th><th>المسؤول</th><th>الإجراء</th></tr></thead><tbody>
          {(pendingQuery.data?.items ?? []).map((row) => <tr key={row.assignmentId}><td>{row.facilityNameAtAssignment}</td><td>{row.regionNameAtAssignment}</td><td>{row.campaignNameAr}</td><td>{row.occurrenceKey}</td><td>{FormAssignmentWorkStatusLabelsAr[row.workStatus] ?? row.workStatus}{row.isOverdue ? ' - متأخر' : ''}</td><td>{dateAr(row.effectiveDueAtUtc)}</td><td>{numberAr(row.daysOverdue)}</td><td>{dateAr(row.lastSavedAtUtc)}</td><td>{row.responsibleUserName ?? 'غير محدد'}</td><td>{row.allowedActions.includes('open-response') && <Link to={`/form-assignments/${row.assignmentId}/respond`}>فتح الاستجابة</Link>} {row.responseId && row.allowedActions.includes('open-review') && <Link to={`/form-responses/${row.responseId}/review`}>فتح المراجعة</Link>} {row.responseId && row.allowedActions.includes('view-history') && <Link to={`/form-responses/${row.responseId}/review`}>عرض السجل</Link>}</td></tr>)}
        </tbody></table>
      </section>
      <section className="panel-section" aria-labelledby="help-heading">
        <h2 id="help-heading" className="section-title">كيف تُحسب المؤشرات؟</h2>
        <p className="muted">المقام هو التكليفات المؤهلة: أزواج الدورة والموقع المستهدفة ناقص غير المتاح. الإكمال يتبع سياسة الحملة: عند الإرسال تحتسب الحالات مرسل وقيد المراجعة ومعتمد ومغلق، وعند الاعتماد تحتسب معتمد ومغلق فقط. المتأخر مؤشر متداخل للسجلات غير المكتملة بعد الموعد الفعال. القيم المفقودة لتاريخ الإكمال لا تعامل كصفر ولا تدخل في متوسط زمن الإكمال.</p>
      </section>
    </div>
  )
}
