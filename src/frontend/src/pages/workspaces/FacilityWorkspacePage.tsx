import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useMemo } from 'react'
import { useParams, useSearchParams } from 'react-router-dom'
import {
  ApiError,
  api,
  type FacilityAlertsEscalationsPayload,
  type FacilityCorrectiveActionsPayload,
  type FacilityExecutiveSummaryPayload,
  type FacilityFormCompliancePayload,
  type FacilityHeaderPayload,
  type FacilityNotesOverviewPayload,
  type FacilityPriorityQueuePayload,
  type FacilityRecentActivityPayload,
  type WorkspaceFilters,
  type WorkspaceVisualTone,
  type WorkspaceWidgetDefinition,
  type WorkspaceWidgetEnvelope,
} from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import {
  WorkspaceDrillDownLink,
  WorkspaceEmpty,
  WorkspaceError,
  WorkspaceFilterBar,
  WorkspaceLoading,
  WorkspaceShell,
  WorkspaceUnauthorized,
  WorkspaceWidgetContainer,
} from '../../workspaces/WorkspaceShell'

const WORKSPACE_KEY = 'facility-operations'
const DEFAULT_DAYS = 30
const DATE_FORMAT = new Intl.DateTimeFormat('ar-SA', {
  timeZone: 'Asia/Riyadh',
  dateStyle: 'medium',
  timeStyle: 'short',
})

export function FacilityWorkspacePage() {
  const { facilityId } = useParams()
  const canViewWorkspace = usePermission('Workspaces.View')
  const canViewFacility = usePermission('Workspaces.ViewFacility')
  const canView = canViewWorkspace && canViewFacility
  const [searchParams, setSearchParams] = useSearchParams()
  const filters = useMemo<WorkspaceFilters>(() => {
    const now = new Date()
    const defaultTo = now.toISOString()
    const defaultFrom = new Date(now.getTime() - DEFAULT_DAYS * 24 * 60 * 60 * 1000).toISOString()
    return {
      level: 1,
      facilityId: facilityId ?? '',
      fromUtc: searchParams.get('fromUtc') ?? defaultFrom,
      toUtc: searchParams.get('toUtc') ?? defaultTo,
      locale: 'ar-SA',
      timeZone: 'Asia/Riyadh',
    }
  }, [facilityId, searchParams])

  const query = useQuery({
    queryKey: ['workspace', WORKSPACE_KEY, facilityId, filters],
    queryFn: () => api.workspaces.get(WORKSPACE_KEY, filters),
    enabled: canView && Boolean(facilityId),
    placeholderData: keepPreviousData,
  })

  if (!facilityId) {
    return <WorkspaceEmpty message="معرّف السجن مطلوب." />
  }

  if (!canView) {
    return <WorkspaceUnauthorized />
  }

  if (query.isLoading) {
    return <WorkspaceLoading />
  }

  if (query.isError) {
    return (
      <WorkspaceError
        message={query.error instanceof ApiError ? query.error.message : 'تعذر تحميل مركز قرار السجن.'}
        onRetry={() => query.refetch()}
      />
    )
  }

  if (!query.data) {
    return <WorkspaceEmpty message="لا توجد مساحة عمل متاحة لهذا السجن." />
  }

  const updateFilters = (next: { fromUtc: string; toUtc: string }) => {
    const params = new URLSearchParams(searchParams)
    params.set('fromUtc', next.fromUtc)
    params.set('toUtc', next.toUtc)
    setSearchParams(params, { replace: true })
  }

  return (
    <WorkspaceShell
      definition={query.data.definition}
      context={query.data.context}
      freshness={query.data.freshness}
      confidence={query.data.confidence}
      generatedAtUtc={query.data.generatedAtUtc}
      allowedActions={query.data.allowedActions}
      widgetFailures={query.data.widgetFailures}
    >
      <WorkspaceFilterBar
        fromUtc={filters.fromUtc ?? ''}
        toUtc={filters.toUtc ?? ''}
        timeZone={query.data.context.timeZone}
        onChange={updateFilters}
        onReset={() => setSearchParams(new URLSearchParams(), { replace: true })}
      />
      <div className="workspace-widget-grid facility-workspace-grid">
        {query.data.widgets.map((widget) => {
          const definition = query.data.widgetDefinitions.find((item) => item.key === widget.widgetKey)
          return definition ? <FacilityWidget key={widget.widgetKey} definition={definition} widget={widget} /> : null
        })}
      </div>
    </WorkspaceShell>
  )
}

function FacilityWidget({ definition, widget }: Readonly<{ definition: WorkspaceWidgetDefinition; widget: WorkspaceWidgetEnvelope }>) {
  return (
    <WorkspaceWidgetContainer definition={definition} data={widget}>
      {widget.widgetKey === 'facility.header' && <FacilityHeader payload={widget.payload as FacilityHeaderPayload} />}
      {widget.widgetKey === 'facility.executive-summary' && <ExecutiveSummary payload={widget.payload as FacilityExecutiveSummaryPayload} />}
      {widget.widgetKey === 'facility.notes-overview' && <NotesOverview payload={widget.payload as FacilityNotesOverviewPayload} />}
      {widget.widgetKey === 'facility.corrective-actions' && <CorrectiveActions payload={widget.payload as FacilityCorrectiveActionsPayload} />}
      {widget.widgetKey === 'facility.alerts-escalations' && <AlertsEscalations payload={widget.payload as FacilityAlertsEscalationsPayload} />}
      {widget.widgetKey === 'facility.form-compliance' && <FormCompliance payload={widget.payload as FacilityFormCompliancePayload} />}
      {widget.widgetKey === 'facility.priority-queue' && <PriorityQueue payload={widget.payload as FacilityPriorityQueuePayload} />}
      {widget.widgetKey === 'facility.recent-activity' && <RecentActivity payload={widget.payload as FacilityRecentActivityPayload} />}
    </WorkspaceWidgetContainer>
  )
}

function FacilityHeader({ payload }: Readonly<{ payload: FacilityHeaderPayload }>) {
  return (
    <div className="facility-context-card">
      <strong>{payload.facilityNameAr}</strong>
      <span>{payload.regionNameAr}</span>
      {payload.facilityType && <span>{payload.facilityType}</span>}
      <span>الفترة: {formatDate(payload.fromUtc)} - {formatDate(payload.toUtc)}</span>
    </div>
  )
}

function ExecutiveSummary({ payload }: Readonly<{ payload: FacilityExecutiveSummaryPayload }>) {
  return (
    <div className="facility-executive-summary" data-status={payload.statusCode}>
      <div>
        <span>الحالة العامة</span>
        <strong>{payload.statusAr}</strong>
      </div>
      <Metric label="قضايا أولوية" value={payload.priorityIssues} tone={payload.priorityIssues > 0 ? 'danger' : 'ok'} />
      <p>{payload.topDriverAr}</p>
      <p>{payload.changeSummaryAr}</p>
      <p>{payload.topPendingActionAr}</p>
      {payload.confidenceReasons.length > 0 && (
        <ul className="compact-list">
          {payload.confidenceReasons.map((reason) => <li key={reason}>{reason}</li>)}
        </ul>
      )}
    </div>
  )
}

function NotesOverview({ payload }: Readonly<{ payload: FacilityNotesOverviewPayload }>) {
  return (
    <>
      <div className="workspace-metric-grid">
        <Metric label="مفتوحة" value={payload.openNotes} />
        <Metric label="حرجة" value={payload.criticalNotes} tone="danger" />
        <Metric label="متأخرة" value={payload.overdueNotes} tone="danger" />
        <Metric label="غير مسندة" value={payload.unassignedNotes} />
        <Metric label="تتطلب إجراء مني" value={payload.requiresMyAction} />
        <Metric label="جديدة" value={payload.newInPeriod} />
      </div>
      <TopBuckets title="أعلى الأنواع" rows={payload.topNoteTypes} />
    </>
  )
}

function CorrectiveActions({ payload }: Readonly<{ payload: FacilityCorrectiveActionsPayload }>) {
  return (
    <div className="workspace-metric-grid">
      <Metric label="مفتوحة" value={payload.openActions} />
      <Metric label="متأخرة" value={payload.overdueActions} tone="danger" />
      <Metric label="قيد التنفيذ" value={payload.inProgressActions} />
      <Metric label="بانتظار التحقق" value={payload.pendingVerificationActions} />
      <Metric label="معاد فتحها" value={payload.reopenedActions} />
      <Metric label="حرجة" value={payload.criticalActions} tone="danger" />
      <Metric label="متوسط الإغلاق/ساعة" value={payload.averageClosureHours == null ? '-' : Math.round(payload.averageClosureHours)} />
    </div>
  )
}

function AlertsEscalations({ payload }: Readonly<{ payload: FacilityAlertsEscalationsPayload }>) {
  return (
    <div className="workspace-metric-grid">
      <Metric label="تنبيهات غير مقروءة" value={payload.personalUnreadNotifications} />
      <Metric label="تصعيدات مفتوحة" value={payload.openEscalations} />
      <Metric label="تصعيدات حرجة" value={payload.criticalEscalations} tone="danger" />
      <Metric label="تنبيهات متأخرة" value={payload.overdueAlerts} tone="danger" />
      <Metric label="آخر معالجة" value={payload.lastEscalationProcessedAtUtc ? formatDate(payload.lastEscalationProcessedAtUtc) : '-'} />
    </div>
  )
}

function FormCompliance({ payload }: Readonly<{ payload: FacilityFormCompliancePayload }>) {
  return (
    <div className="workspace-metric-grid">
      <Metric label="مستهدفة" value={payload.targetedForms} />
      <Metric label="مكتملة" value={payload.completedForms} tone="ok" />
      <Metric label="متبقية" value={payload.remainingForms} />
      <Metric label="متأخرة" value={payload.overdueForms} tone="danger" />
      <Metric label="نسبة الإكمال" value={payload.completionRate == null ? '-' : `${Math.round(payload.completionRate * 100)}%`} />
      <Metric label="أقرب استحقاق" value={payload.nearestDueAtUtc ? formatDate(payload.nearestDueAtUtc) : '-'} />
      <Metric label="لم تبدأ" value={payload.notStartedForms} />
      <Metric label="بانتظار مراجعة" value={payload.pendingReviewForms} />
    </div>
  )
}

function PriorityQueue({ payload }: Readonly<{ payload: FacilityPriorityQueuePayload }>) {
  if (payload.items.length === 0) {
    return <WorkspaceEmpty message="لا توجد عناصر أولوية ضمن الفترة الحالية." />
  }

  return (
    <ol className="facility-priority-list">
      {payload.items.map((item) => (
        <li key={`${item.type}-${item.reference}`}>
          <div>
            <strong>{item.reference}</strong>
            <span className="badge" data-tone={item.priorityRank >= 80 ? 'danger' : 'warn'}>{item.severityAr}</span>
          </div>
          <p>{item.titleAr}</p>
          <span>{item.reasonAr}{item.overdueDays != null ? ` · متأخر ${item.overdueDays} يوم` : ''}</span>
          {item.ownerAr && <span>{item.ownerAr}</span>}
          <WorkspaceDrillDownLink target={item.drillDownTarget} />
        </li>
      ))}
    </ol>
  )
}

function RecentActivity({ payload }: Readonly<{ payload: FacilityRecentActivityPayload }>) {
  if (payload.items.length === 0) {
    return <WorkspaceEmpty message="لا توجد أحداث حديثة ضمن السجن." />
  }

  return (
    <ul className="workspace-timeline">
      {payload.items.map((item) => (
        <li key={`${item.eventType}-${item.entityReference}-${item.occurredAtUtc}`} data-tone={item.tone}>
          <strong>{item.titleAr}</strong>
          {item.descriptionAr && <p>{item.descriptionAr}</p>}
          <span>{formatDate(item.occurredAtUtc)}{item.actorDisplayName ? ` · ${item.actorDisplayName}` : ''}</span>
          <WorkspaceDrillDownLink target={item.drillDownTarget} />
        </li>
      ))}
    </ul>
  )
}

function TopBuckets({ title, rows }: Readonly<{ title: string; rows: Array<{ labelAr: string; count: number }> }>) {
  if (rows.length === 0) {
    return null
  }

  return (
    <div className="facility-top-buckets">
      <strong>{title}</strong>
      {rows.map((row) => <span key={row.labelAr}>{row.labelAr}: {row.count}</span>)}
    </div>
  )
}

function Metric({ label, value, tone = 'muted' }: Readonly<{ label: string; value: number | string; tone?: WorkspaceVisualTone }>) {
  return (
    <div className="workspace-metric" data-tone={tone}>
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  )
}

function formatDate(value: string) {
  return DATE_FORMAT.format(new Date(value))
}
