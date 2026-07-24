import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useMemo } from 'react'
import { useSearchParams } from 'react-router'
import {
  ApiError,
  api,
  type ReferenceCorrectiveActionsPayload,
  type ReferenceOperationalSummaryPayload,
  type WorkspaceFilters,
  type WorkspaceWidgetDefinition,
  type WorkspaceWidgetEnvelope,
} from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import {
  WorkspaceEmpty,
  WorkspaceError,
  WorkspaceFilterBar,
  WorkspaceLoading,
  WorkspaceShell,
  WorkspaceUnauthorized,
  WorkspaceWidgetContainer,
} from '../../workspaces/WorkspaceShell'

const DEFAULT_DAYS = 30

function isReferenceWorkspaceEnabled() {
  return import.meta.env.DEV || import.meta.env.VITE_ENABLE_REFERENCE_WORKSPACE === 'true'
}

export function ReferenceWorkspacePage() {
  const canView = usePermission('Workspaces.View')
  const [searchParams, setSearchParams] = useSearchParams()
  const filters = useMemo<WorkspaceFilters>(() => {
    const now = new Date()
    const defaultTo = now.toISOString()
    const defaultFrom = new Date(now.getTime() - DEFAULT_DAYS * 24 * 60 * 60 * 1000).toISOString()
    return {
      level: 4,
      fromUtc: searchParams.get('fromUtc') ?? defaultFrom,
      toUtc: searchParams.get('toUtc') ?? defaultTo,
      locale: 'ar-SA',
      timeZone: 'Asia/Riyadh',
    }
  }, [searchParams])

  const query = useQuery({
    queryKey: ['workspace-reference', filters],
    queryFn: () => api.workspaces.get('reference', filters),
    enabled: canView && isReferenceWorkspaceEnabled(),
    placeholderData: keepPreviousData,
  })

  if (!isReferenceWorkspaceEnabled()) {
    return <WorkspaceEmpty message="مساحة العمل المرجعية معطلة خارج بيئة التطوير." />
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
        message={query.error instanceof ApiError ? query.error.message : 'تعذر تحميل مساحة العمل المرجعية.'}
        onRetry={() => query.refetch()}
      />
    )
  }

  if (!query.data) {
    return <WorkspaceEmpty message="لا توجد مساحة عمل مرجعية متاحة." />
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
      <div className="workspace-widget-grid">
        {query.data.widgets.map((widget) => {
          const definition = query.data.widgetDefinitions.find((item) => item.key === widget.widgetKey)
          if (!definition) {
            return null
          }

          return <ReferenceWidget key={widget.widgetKey} definition={definition} widget={widget} />
        })}
      </div>
    </WorkspaceShell>
  )
}

function ReferenceWidget({ definition, widget }: Readonly<{ definition: WorkspaceWidgetDefinition; widget: WorkspaceWidgetEnvelope }>) {
  return (
    <WorkspaceWidgetContainer definition={definition} data={widget}>
      {widget.widgetKey === 'dashboard.operational-summary' && <OperationalSummary payload={widget.payload as ReferenceOperationalSummaryPayload} />}
      {widget.widgetKey === 'dashboard.corrective-actions-summary' && <CorrectiveActionsSummary payload={widget.payload as ReferenceCorrectiveActionsPayload} />}
    </WorkspaceWidgetContainer>
  )
}

function OperationalSummary({ payload }: Readonly<{ payload: ReferenceOperationalSummaryPayload }>) {
  return (
    <div className="workspace-metric-grid">
      <Metric label="الملاحظات المفتوحة" value={payload.openNotes} />
      <Metric label="قيد المعالجة" value={payload.inProgressNotes} />
      <Metric label="بانتظار التحقق" value={payload.pendingVerificationNotes} />
      <Metric label="بلا تكليف" value={payload.unassignedNotes} />
      <Metric label="تحتاج توجيه" value={payload.requiresRouting} />
      <Metric label="متأخرة" value={payload.overdueNotes} tone="danger" />
      <Metric label="قريبة الاستحقاق" value={payload.dueSoonNotes} />
      <Metric label="حرجة/عالية" value={payload.criticalOrHighNotes} />
    </div>
  )
}

function CorrectiveActionsSummary({ payload }: Readonly<{ payload: ReferenceCorrectiveActionsPayload }>) {
  return (
    <div className="workspace-metric-grid">
      <Metric label="إجراءات نشطة" value={payload.activeActions} />
      <Metric label="إجراءات متأخرة" value={payload.overdueActions} tone="danger" />
      <Metric label="بانتظار تحقق" value={payload.pendingVerificationActions} />
      <Metric label="معاد فتحها" value={payload.reopenedActions} />
      <Metric label="ملاحظات متعثرة" value={payload.notesWithStalledActions} />
    </div>
  )
}

function Metric({ label, value, tone = 'muted' }: Readonly<{ label: string; value: number; tone?: string }>) {
  return (
    <div className="workspace-metric" data-tone={tone}>
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  )
}
