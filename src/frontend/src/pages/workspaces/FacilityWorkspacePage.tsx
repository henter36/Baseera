import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import {
  ApiError,
  api,
  type CorrectiveActionDetail,
  type CorrectiveActionStatusHistoryEntry,
  type FacilityAlertsEscalationsPayload,
  type FacilityCorrectiveActionsPayload,
  type FacilityExecutiveSummaryPayload,
  type FacilityFormCompliancePayload,
  type FacilityHeaderPayload,
  type FacilityNotesOverviewPayload,
  type FacilityPriorityQueuePayload,
  type FacilityPriorityQueuePayload as FacilityPriorityPayload,
  type FacilityRecentActivityPayload,
  type NoteWorkspaceAllowedAction,
  type NoteWorkspaceDetail,
  type WorkspaceConfidence,
  type WorkspaceFilters,
  type WorkspaceShell as WorkspaceShellDto,
  type WorkspaceVisualTone,
  type WorkspaceWidgetEnvelope,
} from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import {
  WorkspaceEmpty,
  WorkspaceError,
  WorkspaceFilterBar,
  WorkspaceLoading,
  WorkspaceUnauthorized,
} from '../../workspaces/WorkspaceShell'

const WORKSPACE_KEY = 'facility-operations'
const DEFAULT_DAYS = 30
const INLINE_NOTE_ACTIONS = new Set<NoteWorkspaceAllowedAction>([
  'SUBMIT',
  'START_WORK',
  'REQUEST_VERIFICATION',
  'REJECT_VERIFICATION',
  'REOPEN',
  'CANCEL',
])
const DATE_FORMAT = new Intl.DateTimeFormat('ar-SA', {
  timeZone: 'Asia/Riyadh',
  dateStyle: 'medium',
  timeStyle: 'short',
})
const SHORT_DATE_FORMAT = new Intl.DateTimeFormat('ar-SA', {
  timeZone: 'Asia/Riyadh',
  month: 'short',
  day: 'numeric',
})

type PanelType = 'note' | 'corrective-action' | 'escalation' | 'form' | 'activity'

type PanelState = Readonly<{
  type: PanelType
  entityId: string
}>

type PriorityItem = FacilityPriorityPayload['items'][number]
type ActivityItem = FacilityRecentActivityPayload['items'][number]

type CommandData = Readonly<{
  header?: FacilityHeaderPayload
  executive?: FacilityExecutiveSummaryPayload
  notes?: FacilityNotesOverviewPayload
  actions?: FacilityCorrectiveActionsPayload
  alerts?: FacilityAlertsEscalationsPayload
  forms?: FacilityFormCompliancePayload
  priority?: FacilityPriorityQueuePayload
  activity?: FacilityRecentActivityPayload
}>

export function FacilityWorkspacePage() {
  const { facilityId } = useParams()
  const canViewWorkspace = usePermission('Workspaces.View')
  const canViewFacility = usePermission('Workspaces.ViewFacility')
  const canView = canViewWorkspace && canViewFacility
  const [searchParams, setSearchParams] = useSearchParams()
  const queryClient = useQueryClient()
  const [activeSection, setActiveSection] = useState('overview')
  const [isActionCenterOpen, setIsActionCenterOpen] = useState(false)
  const selectedRowRef = useRef<HTMLButtonElement | null>(null)

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

  const panel = panelFromSearch(searchParams)
  const query = useQuery({
    queryKey: ['workspace', WORKSPACE_KEY, facilityId, filters],
    queryFn: () => api.workspaces.get(WORKSPACE_KEY, filters),
    enabled: canView && Boolean(facilityId),
    placeholderData: keepPreviousData,
  })

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && panel) {
        closePanel(searchParams, setSearchParams, selectedRowRef)
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [panel, searchParams, setSearchParams])

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

  const data = extractCommandData(query.data)
  const updateFilters = (next: { fromUtc: string; toUtc: string }) => {
    const params = new URLSearchParams(searchParams)
    params.set('fromUtc', next.fromUtc)
    params.set('toUtc', next.toUtc)
    setSearchParams(params, { replace: true })
  }
  const openPanel = (next: PanelState) => {
    const params = new URLSearchParams(searchParams)
    params.set('panel', next.type)
    params.set('entityId', next.entityId)
    setSearchParams(params, { replace: false })
  }

  return (
    <main className={`facility-command-center ${panel ? 'has-context-panel' : ''}`} dir="rtl">
      <CommandHeader
        shell={query.data}
        data={data}
        filters={filters}
        onRefresh={() => query.refetch()}
        onOpenActions={() => setIsActionCenterOpen(true)}
      />

      <nav className="command-section-nav" aria-label="تنقل مركز القرار">
        {[
          ['overview', 'نظرة عامة'],
          ['priorities', 'الأولويات'],
          ['notes', 'الملاحظات'],
          ['actions', 'الإجراءات'],
          ['compliance', 'الالتزام'],
          ['activity', 'النشاط'],
        ].map(([key, label]) => (
          <button key={key} type="button" aria-pressed={activeSection === key} onClick={() => setActiveSection(key)}>
            {label}
          </button>
        ))}
      </nav>

      <WorkspaceFilterBar
        fromUtc={filters.fromUtc ?? ''}
        toUtc={filters.toUtc ?? ''}
        timeZone={query.data.context.timeZone}
        onChange={updateFilters}
        onReset={() => setSearchParams(new URLSearchParams(), { replace: true })}
      />

      <div className="command-workspace-grid">
        <section className="command-main" aria-label="المشهد التشغيلي">
          <SituationOverview data={data} confidence={query.data.confidence} activeSection={activeSection} />
          <SectionDeck data={data} activeSection={activeSection} openPanel={openPanel} selectedPanel={panel} selectedRowRef={selectedRowRef} />
        </section>

        <InterventionQueue
          payload={data.priority}
          selectedPanel={panel}
          openPanel={openPanel}
          selectedRowRef={selectedRowRef}
        />
      </div>

      {panel && (
        <CommandContextPanel
          panel={panel}
          shell={query.data}
          queue={data.priority}
          activity={data.activity}
          onClose={() => closePanel(searchParams, setSearchParams, selectedRowRef)}
          onChanged={() => {
            query.refetch()
            queryClient.invalidateQueries({ queryKey: ['workspace-panel'] })
          }}
        />
      )}

      {isActionCenterOpen && (
        <ActionCenter data={data} onClose={() => setIsActionCenterOpen(false)} openPanel={openPanel} />
      )}
    </main>
  )
}

function CommandHeader({
  shell,
  data,
  filters,
  onRefresh,
  onOpenActions,
}: Readonly<{
  shell: WorkspaceShellDto
  data: CommandData
  filters: WorkspaceFilters
  onRefresh: () => void
  onOpenActions: () => void
}>) {
  const statusTone = statusToneFor(data.executive?.statusCode)
  return (
    <header className="command-header">
      <div className="command-header-identity">
        <span className="command-eyebrow">مركز قيادة السجن</span>
        <h1>{data.header?.facilityNameAr ?? shell.definition.titleAr}</h1>
        <p>{data.header?.regionNameAr ?? shell.context.scopeLabelAr}{data.header?.facilityType ? ` · ${data.header.facilityType}` : ''}</p>
      </div>
      <div className="command-header-status" data-tone={statusTone}>
        <span>الحالة العامة</span>
        <strong>{data.executive?.statusAr ?? 'غير معروفة'}</strong>
      </div>
      <div className="command-header-metrics">
        <CommandMetric label="تحتاج تدخلاً" value={data.executive?.priorityIssues ?? 0} tone={statusTone} />
        <CommandMetric label="الثقة" value={shell.confidence.labelAr} tone={confidenceTone(shell.confidence.level)} />
        <CommandMetric label="آخر تحديث" value={formatDate(shell.generatedAtUtc)} tone="info" />
      </div>
      <div className="command-header-actions">
        <span className="command-period">{formatShortDate(filters.fromUtc)} - {formatShortDate(filters.toUtc)}</span>
        <button type="button" className="command-button" onClick={onRefresh}>تحديث</button>
        <button type="button" className="command-button primary" onClick={onOpenActions}>مركز الإجراءات</button>
      </div>
      {shell.widgetFailures.length > 0 && (
        <output className="command-partial-warning" aria-live="polite" aria-atomic="true">
          بيانات جزئية: {shell.widgetFailures.map((failure) => failure.messageAr).join('، ')}
        </output>
      )}
    </header>
  )
}

function SituationOverview({
  data,
  confidence,
  activeSection,
}: Readonly<{ data: CommandData; confidence: WorkspaceConfidence; activeSection: string }>) {
  if (activeSection !== 'overview') {
    return null
  }

  return (
    <section className="situation-overview" aria-labelledby="situation-title">
      <div className="situation-status" data-status={data.executive?.statusCode ?? 'unknown'}>
        <div>
          <span className="command-eyebrow">المشهد الآن</span>
          <h2 id="situation-title">{data.executive?.statusAr ?? 'لا توجد حالة محسوبة'}</h2>
          <p>{data.executive?.topDriverAr ?? 'لا توجد أسباب بارزة ضمن الفترة الحالية.'}</p>
        </div>
        <div className="situation-explain">
          <strong>{data.executive?.topPendingActionAr ?? 'لا يوجد إجراء عاجل.'}</strong>
          <span>{data.executive?.changeSummaryAr ?? 'لم يتم رصد تغيرات مهمة.'}</span>
          <ConfidenceIndicator confidence={confidence} reasons={data.executive?.confidenceReasons ?? []} />
        </div>
      </div>
      <OperationalPulse data={data} />
    </section>
  )
}

function OperationalPulse({ data }: Readonly<{ data: CommandData }>) {
  const completion = data.forms?.completionRate == null ? null : Math.round(data.forms.completionRate * 100)
  return (
    <div className="operational-pulse" aria-label="نبض التشغيل">
      <OperationalPulseItem
        label="الملاحظات"
        value={data.notes?.openNotes ?? 0}
        detail={`${data.notes?.criticalNotes ?? 0} حرجة · ${data.notes?.overdueNotes ?? 0} متأخرة`}
        tone={notesPulseTone(data.notes)}
      />
      <OperationalPulseItem
        label="الإجراءات"
        value={data.actions?.openActions ?? 0}
        detail={`${data.actions?.overdueActions ?? 0} متأخرة · ${data.actions?.pendingVerificationActions ?? 0} تحقق`}
        tone={(data.actions?.overdueActions ?? 0) > 0 ? 'warn' : 'info'}
      />
      <OperationalPulseItem
        label="التصعيدات"
        value={data.alerts?.openEscalations ?? 0}
        detail={`${data.alerts?.criticalEscalations ?? 0} حرجة · ${data.alerts?.personalUnreadNotifications ?? 0} غير مقروءة`}
        tone={alertsPulseTone(data.alerts)}
      />
      <OperationalPulseItem
        label="الالتزام"
        value={completion == null ? '-' : `${completion}%`}
        detail={`${data.forms?.overdueForms ?? 0} متأخرة · ${data.forms?.remainingForms ?? 0} متبقية`}
        tone={(data.forms?.overdueForms ?? 0) > 0 ? 'warn' : 'ok'}
      />
    </div>
  )
}

function SectionDeck({
  data,
  activeSection,
  openPanel,
  selectedPanel,
  selectedRowRef,
}: Readonly<{
  data: CommandData
  activeSection: string
  openPanel: (panel: PanelState) => void
  selectedPanel: PanelState | null
  selectedRowRef: React.MutableRefObject<HTMLButtonElement | null>
}>) {
  if (activeSection === 'priorities') {
    return (
      <InterventionQueue
        payload={data.priority}
        selectedPanel={selectedPanel}
        openPanel={openPanel}
        selectedRowRef={selectedRowRef}
        embedded
      />
    )
  }

  if (activeSection === 'notes') {
    return <CommandSection title="الملاحظات التشغيلية"><NotesOverview payload={data.notes} /></CommandSection>
  }

  if (activeSection === 'actions') {
    return <CommandSection title="الإجراءات التصحيحية"><CorrectiveActions payload={data.actions} /></CommandSection>
  }

  if (activeSection === 'compliance') {
    return <CommandSection title="الالتزام بالنماذج"><FormCompliance payload={data.forms} /></CommandSection>
  }

  return (
    <CommandSection title="آخر التغيرات التشغيلية">
      <RecentActivity payload={data.activity} openPanel={openPanel} selectedPanel={selectedPanel} selectedRowRef={selectedRowRef} />
    </CommandSection>
  )
}

function InterventionQueue({
  payload,
  selectedPanel,
  openPanel,
  selectedRowRef,
  embedded = false,
}: Readonly<{
  payload?: FacilityPriorityQueuePayload
  selectedPanel: PanelState | null
  openPanel: (panel: PanelState) => void
  selectedRowRef: React.MutableRefObject<HTMLButtonElement | null>
  embedded?: boolean
}>) {
  const items = payload?.items ?? []
  return (
    <aside className={`intervention-queue ${embedded ? 'embedded' : ''}`} aria-labelledby={embedded ? 'embedded-priority-title' : 'priority-title'}>
      <div className="queue-header">
        <div>
          <span className="command-eyebrow">تحتاج تدخلًا</span>
          <h2 id={embedded ? 'embedded-priority-title' : 'priority-title'}>قائمة الأولويات</h2>
        </div>
        <span>{items.length} / {payload?.limit ?? 10}</span>
      </div>
      {items.length === 0 ? (
        <WorkspaceEmpty message="لا توجد عناصر أولوية ضمن الفترة الحالية." />
      ) : (
        <div className="priority-row-list" role="list">
          {items.map((item, index) => {
            const panel = panelForPriorityItem(item)
            const selected = selectedPanel?.type === panel.type && selectedPanel.entityId === panel.entityId
            return (
              <button
                key={`${item.type}-${item.reference}-${index}`}
                ref={selected ? selectedRowRef : undefined}
                type="button"
                className="priority-row"
                data-selected={selected}
                data-tone={priorityTone(item)}
                onClick={(event) => {
                  selectedRowRef.current = event.currentTarget
                  openPanel(panel)
                }}
              >
                <span className="priority-band" aria-hidden="true" />
                <span className="priority-row-main">
                  <strong>{item.titleAr}</strong>
                  <small>{item.reference} · {item.reasonAr}</small>
                </span>
                <span className="priority-row-meta">
                  <span>{item.severityAr}</span>
                  {item.overdueDays != null && <span>{item.overdueDays} يوم</span>}
                  {item.ownerAr && <span>{item.ownerAr}</span>}
                </span>
              </button>
            )
          })}
        </div>
      )}
    </aside>
  )
}

function CommandContextPanel({
  panel,
  shell,
  queue,
  activity,
  onClose,
  onChanged,
}: Readonly<{
  panel: PanelState
  shell: WorkspaceShellDto
  queue?: FacilityPriorityQueuePayload
  activity?: FacilityRecentActivityPayload
  onClose: () => void
  onChanged: () => void
}>) {
  const panelRef = useRef<HTMLElement | null>(null)
  const summary = findPanelSummary(panel, queue, activity)

  useEffect(() => {
    panelRef.current?.focus()
  }, [panel.type, panel.entityId])

  return (
    <aside
      ref={panelRef}
      className="command-context-panel"
      role="dialog"
      aria-modal="false"
      tabIndex={-1}
      aria-labelledby="context-panel-title"
    >
      <div className="context-panel-toolbar">
        <button type="button" className="command-icon-button" onClick={onClose} aria-label="إغلاق لوحة التفاصيل">×</button>
        <span>{panelLabel(panel.type)}</span>
        <Link className="command-button ghost" to={legacyRouteForPanel(panel, summary, shell)}>فتح الصفحة الكاملة</Link>
      </div>
      <div className="context-panel-summary">
        <span className="command-eyebrow">{summaryReference(summary) || panel.entityId}</span>
        <h2 id="context-panel-title">{summary?.titleAr ?? 'تفاصيل العنصر'}</h2>
        {summaryReason(summary) !== '-' && <p>{summaryReason(summary)}</p>}
      </div>
      <PanelDetail panel={panel} summary={summary} onChanged={onChanged} />
    </aside>
  )
}

function PanelDetail({
  panel,
  summary,
  onChanged,
}: Readonly<{ panel: PanelState; summary?: PriorityItem | ActivityItem; onChanged: () => void }>) {
  if (panel.type === 'note') {
    return <NotePanel noteId={panel.entityId} summary={summary} onChanged={onChanged} />
  }

  if (panel.type === 'corrective-action') {
    return <CorrectiveActionPanel actionId={panel.entityId} summary={summary} />
  }

  if (panel.type === 'form') {
    return <FormPreviewPanel summary={summary} />
  }

  if (panel.type === 'escalation') {
    return <EscalationPreviewPanel summary={summary} />
  }

  return <ActivityPreviewPanel summary={summary} />
}

function NotePanel({ noteId, summary, onChanged }: Readonly<{ noteId: string; summary?: PriorityItem | ActivityItem; onChanged: () => void }>) {
  const [activeAction, setActiveAction] = useState<NoteWorkspaceAllowedAction | ''>('')
  const [reason, setReason] = useState('')
  const detailQuery = useQuery({
    queryKey: ['workspace-panel', 'note', noteId],
    queryFn: () => api.notes.workspaceDetail(noteId),
  })
  const mutation = useMutation({
    mutationFn: async (action: NoteWorkspaceAllowedAction) => executeNoteAction(action, detailQuery.data!, reason),
    onSuccess: () => {
      setActiveAction('')
      setReason('')
      detailQuery.refetch()
      onChanged()
    },
  })

  if (detailQuery.isLoading) return <PanelLoading />
  if (detailQuery.isError) return <PanelError error={detailQuery.error} />
  if (!detailQuery.data) return <WorkspaceEmpty message="لا توجد تفاصيل متاحة." />

  const detail = detailQuery.data
  return (
    <div className="context-stack">
      <ContextSection title="ملخص الملاحظة">
        <StatusRail
          tone={detail.note.isOverdue ? 'danger' : summary && 'priorityRank' in summary ? priorityTone(summary) : 'info'}
          rows={[
            ['الحالة', detail.note.statusAr],
            ['الخطورة', detail.note.severityAr],
            ['النوع', detail.note.noteTypeNameAr],
            ['الموعد', detail.note.dueAtUtc ? formatDate(detail.note.dueAtUtc) : '-'],
          ]}
        />
        <p>{detail.note.description}</p>
      </ContextSection>
      <AllowedNoteActions
        actions={detail.allowedActions}
        activeAction={activeAction}
        reason={reason}
        isPending={mutation.isPending}
        error={mutation.error}
        onSelect={setActiveAction}
        onReasonChange={setReason}
        onSubmit={() => activeAction && mutation.mutate(activeAction)}
      />
      <ContextSection title="الإجراءات التصحيحية">
        {detail.correctiveActions.items.length === 0 ? (
          <WorkspaceEmpty message="لا توجد إجراءات مرتبطة." />
        ) : (
          <CompactList rows={detail.correctiveActions.items.map((item) => [item.referenceNumber, `${item.title} · ${item.statusAr}`])} />
        )}
      </ContextSection>
      <ContextSection title="الخط الزمني">
        <CompactTimeline rows={detail.timeline.map((item) => ({ title: item.titleAr, at: item.occurredAtUtc, tone: item.tone }))} />
      </ContextSection>
    </div>
  )
}

function CorrectiveActionPanel({ actionId }: Readonly<{ actionId: string; summary?: PriorityItem | ActivityItem }>) {
  const detailQuery = useQuery({
    queryKey: ['workspace-panel', 'corrective-action', actionId],
    queryFn: () => api.correctiveActions.get(actionId),
  })
  const historyQuery = useQuery({
    queryKey: ['workspace-panel', 'corrective-action-history', actionId],
    queryFn: () => api.correctiveActions.history(actionId),
    enabled: Boolean(detailQuery.data),
  })

  if (detailQuery.isLoading) return <PanelLoading />
  if (detailQuery.isError) return <PanelError error={detailQuery.error} />
  if (!detailQuery.data) return <WorkspaceEmpty message="لا توجد تفاصيل متاحة." />

  const action = detailQuery.data
  return (
    <div className="context-stack">
      <ContextSection title="ملخص الإجراء">
        <CorrectiveActionSnapshot action={action} />
      </ContextSection>
      <ContextSection title="المسؤولية والمهلة">
        <StatusRail
          tone={action.isOverdue ? 'danger' : 'info'}
          rows={[
            ['المسؤول', action.currentAssigneeDisplay ?? action.currentAssignment?.assignedToDepartmentName ?? '-'],
            ['الحالة', action.statusAr],
            ['الأولوية', action.priorityAr],
            ['الموعد', action.dueAtUtc ? formatDate(action.dueAtUtc) : '-'],
          ]}
        />
      </ContextSection>
      <ContextSection title="خط الحالة">
        {historyQuery.data ? (
          <CompactTimeline rows={historyQuery.data.map(toCorrectiveActionTimeline)} />
        ) : (
          <PanelLoading />
        )}
      </ContextSection>
      <div className="context-action-note">الإجراءات المركبة لهذا الإجراء متاحة من الصفحة الكاملة حتى يتم استخراج نماذجها داخل مركز القرار.</div>
    </div>
  )
}

function EscalationPreviewPanel({ summary }: Readonly<{ summary?: PriorityItem | ActivityItem }>) {
  return (
    <div className="context-stack">
      <ContextSection title="ملخص التصعيد">
        <StatusRail
          tone="danger"
          rows={[
            ['المرجع', summaryReference(summary)],
            ['السبب', summaryReason(summary)],
            ['الموعد', summaryDue(summary)],
            ['المصدر', 'التصعيدات التشغيلية'],
          ]}
        />
      </ContextSection>
      <div className="context-action-note">لا يحتوي عنصر الأولوية الحالي على معرف occurrence محدد؛ تعرض اللوحة ملخصًا آمنًا، والصفحة الكاملة متاحة عند الحاجة.</div>
    </div>
  )
}

function FormPreviewPanel({ summary }: Readonly<{ summary?: PriorityItem | ActivityItem }>) {
  return (
    <div className="context-stack">
      <ContextSection title="ملخص الالتزام">
        <StatusRail
          tone="warn"
          rows={[
            ['المرجع', summaryReference(summary)],
            ['الحملة', summaryTitle(summary)],
            ['سبب الظهور', summaryReason(summary)],
            ['الاستحقاق', summaryDue(summary)],
          ]}
        />
      </ContextSection>
      <div className="context-action-note">الانتقال إلى صفحة التعبئة أو المراجعة يبقى إجراءً صريحًا فقط عندما يحتاج المستخدم إدخال النموذج.</div>
    </div>
  )
}

function ActivityPreviewPanel({ summary }: Readonly<{ summary?: PriorityItem | ActivityItem }>) {
  return (
    <ContextSection title="تفاصيل الحدث">
      <StatusRail
        tone={summary && 'tone' in summary ? summary.tone : 'info'}
        rows={[
          ['العنوان', summaryTitle(summary)],
          ['المرجع', summaryReference(summary)],
          ['الوصف', summaryReason(summary)],
          ['الوقت', summary && 'occurredAtUtc' in summary ? formatDate(summary.occurredAtUtc) : '-'],
        ]}
      />
    </ContextSection>
  )
}

function ActionCenter({ data, onClose, openPanel }: Readonly<{ data: CommandData; onClose: () => void; openPanel: (panel: PanelState) => void }>) {
  const urgent = data.priority?.items.slice(0, 5) ?? []
  return (
    <aside className="action-center" aria-labelledby="action-center-title">
      <div className="context-panel-toolbar">
        <button type="button" className="command-icon-button" onClick={onClose} aria-label="إغلاق مركز الإجراءات">×</button>
        <h2 id="action-center-title">مركز الإجراءات</h2>
      </div>
      <div className="action-center-grid">
        <CommandMetric label="مسندة أو تحتاج إجراء" value={data.notes?.requiresMyAction ?? 0} tone="warn" />
        <CommandMetric label="متأخرة" value={(data.notes?.overdueNotes ?? 0) + (data.actions?.overdueActions ?? 0) + (data.forms?.overdueForms ?? 0)} tone="danger" />
        <CommandMetric label="مصعدة" value={data.alerts?.openEscalations ?? 0} tone="warn" />
      </div>
      <div className="priority-row-list" role="list">
        {urgent.map((item) => (
          <button key={`${item.type}-${item.reference}`} type="button" className="priority-row compact" onClick={() => openPanel(panelForPriorityItem(item))}>
            <span className="priority-band" aria-hidden="true" />
            <span className="priority-row-main"><strong>{item.titleAr}</strong><small>{item.reasonAr}</small></span>
          </button>
        ))}
      </div>
    </aside>
  )
}

function NotesOverview({ payload }: Readonly<{ payload?: FacilityNotesOverviewPayload }>) {
  return (
    <div className="command-metric-strip">
      <CommandMetric label="مفتوحة" value={payload?.openNotes ?? 0} tone="info" />
      <CommandMetric label="حرجة" value={payload?.criticalNotes ?? 0} tone="danger" />
      <CommandMetric label="متأخرة" value={payload?.overdueNotes ?? 0} tone="warn" />
      <CommandMetric label="إجراء مني" value={payload?.requiresMyAction ?? 0} tone="attention" />
      <TopBuckets rows={payload?.topNoteTypes ?? []} />
    </div>
  )
}

function CorrectiveActions({ payload }: Readonly<{ payload?: FacilityCorrectiveActionsPayload }>) {
  return (
    <div className="command-metric-strip">
      <CommandMetric label="مفتوحة" value={payload?.openActions ?? 0} tone="info" />
      <CommandMetric label="متأخرة" value={payload?.overdueActions ?? 0} tone="warn" />
      <CommandMetric label="قيد التنفيذ" value={payload?.inProgressActions ?? 0} tone="info" />
      <CommandMetric label="بانتظار التحقق" value={payload?.pendingVerificationActions ?? 0} tone="attention" />
      <CommandMetric label="متوسط الإغلاق" value={payload?.averageClosureHours == null ? '-' : Math.round(payload.averageClosureHours)} tone="muted" />
    </div>
  )
}

function FormCompliance({ payload }: Readonly<{ payload?: FacilityFormCompliancePayload }>) {
  return (
    <div className="command-metric-strip">
      <CommandMetric label="مستهدفة" value={payload?.targetedForms ?? 0} tone="info" />
      <CommandMetric label="مكتملة" value={payload?.completedForms ?? 0} tone="ok" />
      <CommandMetric label="متأخرة" value={payload?.overdueForms ?? 0} tone="warn" />
      <CommandMetric label="الإكمال" value={payload?.completionRate == null ? '-' : `${Math.round(payload.completionRate * 100)}%`} tone="ok" />
      <CommandMetric label="أقرب استحقاق" value={payload?.nearestDueAtUtc ? formatShortDate(payload.nearestDueAtUtc) : '-'} tone="muted" />
    </div>
  )
}

function RecentActivity({
  payload,
  openPanel,
  selectedPanel,
  selectedRowRef,
}: Readonly<{
  payload?: FacilityRecentActivityPayload
  openPanel: (panel: PanelState) => void
  selectedPanel: PanelState | null
  selectedRowRef: React.MutableRefObject<HTMLButtonElement | null>
}>) {
  const items = payload?.items ?? []
  if (items.length === 0) return <WorkspaceEmpty message="لا توجد أحداث حديثة ضمن السجن." />
  return (
    <ul className="compact-timeline-list">
      {items.map((item, index) => {
        const panel = panelForActivityItem(item, index)
        const selected = selectedPanel?.type === panel.type && selectedPanel.entityId === panel.entityId
        return (
          <li key={`${item.eventType}-${item.entityReference}-${item.occurredAtUtc}`} data-tone={item.tone}>
            <button
              ref={selected ? selectedRowRef : undefined}
              type="button"
              onClick={(event) => {
                selectedRowRef.current = event.currentTarget
                openPanel(panel)
              }}
            >
              <strong>{item.titleAr}</strong>
              <span>{formatDate(item.occurredAtUtc)}{item.actorDisplayName ? ` · ${item.actorDisplayName}` : ''}</span>
            </button>
          </li>
        )
      })}
    </ul>
  )
}

function AllowedNoteActions({
  actions,
  activeAction,
  reason,
  isPending,
  error,
  onSelect,
  onReasonChange,
  onSubmit,
}: Readonly<{
  actions: NoteWorkspaceAllowedAction[]
  activeAction: NoteWorkspaceAllowedAction | ''
  reason: string
  isPending: boolean
  error: unknown
  onSelect: (action: NoteWorkspaceAllowedAction | '') => void
  onReasonChange: (reason: string) => void
  onSubmit: () => void
}>) {
  if (actions.length === 0) {
    return <div className="context-action-note">لا توجد إجراءات مسموحة حاليًا لهذه الملاحظة.</div>
  }

  return (
    <ContextSection title="الإجراءات المسموحة">
      <div className="inline-action-list">
        {actions.map((action) => {
          const isSupported = INLINE_NOTE_ACTIONS.has(action)
          return (
            <button
              key={action}
              type="button"
              className="inline-action"
              disabled={!isSupported}
              title={isSupported ? undefined : 'يتطلب هذا الإجراء نموذجًا متقدمًا في الصفحة الكاملة.'}
              onClick={() => onSelect(activeAction === action ? '' : action)}
            >
              {noteActionLabel(action)}
            </button>
          )
        })}
      </div>
      {activeAction && (
        <form className="inline-action-form" onSubmit={(event) => { event.preventDefault(); onSubmit() }}>
          <label>
            <span>سبب الإجراء</span>
            <textarea value={reason} onChange={(event) => onReasonChange(event.target.value)} rows={3} required />
          </label>
          {Boolean(error) && <div className="error" role="alert">{error instanceof Error ? error.message : 'تعذر تنفيذ الإجراء.'}</div>}
          <button type="submit" className="command-button primary" disabled={isPending}>{isPending ? 'جار التنفيذ...' : 'تنفيذ'}</button>
        </form>
      )}
    </ContextSection>
  )
}

function CommandSection({ title, children }: Readonly<{ title: string; children: React.ReactNode }>) {
  return (
    <section className="command-section" aria-labelledby={`${title}-title`}>
      <h2 id={`${title}-title`}>{title}</h2>
      {children}
    </section>
  )
}

function CommandMetric({ label, value, tone = 'muted' }: Readonly<{ label: string; value: number | string; tone?: WorkspaceVisualTone | 'attention' }>) {
  return (
    <div className="command-metric" data-tone={tone}>
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  )
}

function OperationalPulseItem({ label, value, detail, tone }: Readonly<{ label: string; value: number | string; detail: string; tone: WorkspaceVisualTone }>) {
  return (
    <div className="pulse-item" data-tone={tone}>
      <span>{label}</span>
      <strong>{value}</strong>
      <small>{detail}</small>
    </div>
  )
}

function ConfidenceIndicator({ confidence, reasons }: Readonly<{ confidence: WorkspaceConfidence; reasons: string[] }>) {
  return (
    <div className="confidence-indicator" data-tone={confidenceTone(confidence.level)}>
      <strong>ثقة {confidence.labelAr}</strong>
      <span>{reasons[0] ?? 'المصادر الأساسية متاحة ضمن الفترة.'}</span>
    </div>
  )
}

function ContextSection({ title, children }: Readonly<{ title: string; children: React.ReactNode }>) {
  return (
    <section className="context-section">
      <h3>{title}</h3>
      {children}
    </section>
  )
}

function StatusRail({ tone, rows }: Readonly<{ tone: WorkspaceVisualTone | 'attention'; rows: Array<[string, string]> }>) {
  return (
    <dl className="status-rail" data-tone={tone}>
      {rows.map(([label, value]) => (
        <div key={label}>
          <dt>{label}</dt>
          <dd>{value}</dd>
        </div>
      ))}
    </dl>
  )
}

function CompactList({ rows }: Readonly<{ rows: Array<[string, string]> }>) {
  return (
    <ul className="compact-detail-list">
      {rows.map(([key, value]) => <li key={key}><strong>{key}</strong><span>{value}</span></li>)}
    </ul>
  )
}

function CompactTimeline({ rows }: Readonly<{ rows: Array<{ title: string; at: string; tone: WorkspaceVisualTone }> }>) {
  if (rows.length === 0) return <WorkspaceEmpty message="لا توجد أحداث." />
  return (
    <ul className="compact-timeline-list">
      {rows.slice(0, 6).map((row) => (
        <li key={`${row.title}-${row.at}`} data-tone={row.tone}>
          <strong>{row.title}</strong>
          <span>{formatDate(row.at)}</span>
        </li>
      ))}
    </ul>
  )
}

function CorrectiveActionSnapshot({ action }: Readonly<{ action: CorrectiveActionDetail }>) {
  return (
    <>
      <StatusRail
        tone={action.isOverdue ? 'danger' : 'info'}
        rows={[
          ['المرجع', action.referenceNumber],
          ['الحالة', action.statusAr],
          ['الأولوية', action.priorityAr],
          ['التأخر', action.overdueDays != null ? `${action.overdueDays} يوم` : '-'],
        ]}
      />
      <p>{action.description}</p>
    </>
  )
}

function TopBuckets({ rows }: Readonly<{ rows: Array<{ labelAr: string; count: number }> }>) {
  if (rows.length === 0) return null
  return (
    <div className="top-buckets">
      {rows.map((row) => <span key={row.labelAr}>{row.labelAr}: {row.count}</span>)}
    </div>
  )
}

function PanelLoading() {
  return <div className="panel-loading" aria-busy="true">جاري تحميل التفاصيل…</div>
}

function PanelError({ error }: Readonly<{ error: unknown }>) {
  return <div className="error" role="alert">{error instanceof ApiError ? error.message : 'تعذر تحميل تفاصيل العنصر داخل مساحة العمل.'}</div>
}

function extractCommandData(shell: WorkspaceShellDto): CommandData {
  return {
    header: payloadFor<FacilityHeaderPayload>(shell.widgets, 'facility.header'),
    executive: payloadFor<FacilityExecutiveSummaryPayload>(shell.widgets, 'facility.executive-summary'),
    notes: payloadFor<FacilityNotesOverviewPayload>(shell.widgets, 'facility.notes-overview'),
    actions: payloadFor<FacilityCorrectiveActionsPayload>(shell.widgets, 'facility.corrective-actions'),
    alerts: payloadFor<FacilityAlertsEscalationsPayload>(shell.widgets, 'facility.alerts-escalations'),
    forms: payloadFor<FacilityFormCompliancePayload>(shell.widgets, 'facility.form-compliance'),
    priority: payloadFor<FacilityPriorityQueuePayload>(shell.widgets, 'facility.priority-queue'),
    activity: payloadFor<FacilityRecentActivityPayload>(shell.widgets, 'facility.recent-activity'),
  }
}

function payloadFor<T>(widgets: WorkspaceWidgetEnvelope[], key: string): T | undefined {
  return widgets.find((widget) => widget.widgetKey === key)?.payload as T | undefined
}

function panelFromSearch(searchParams: URLSearchParams): PanelState | null {
  const type = searchParams.get('panel')
  const entityId = searchParams.get('entityId')
  if (!entityId || !isPanelType(type)) return null
  return { type, entityId }
}

function isPanelType(value: string | null): value is PanelType {
  return value === 'note' || value === 'corrective-action' || value === 'escalation' || value === 'form' || value === 'activity'
}

function closePanel(
  searchParams: URLSearchParams,
  setSearchParams: ReturnType<typeof useSearchParams>[1],
  selectedRowRef: React.MutableRefObject<HTMLButtonElement | null>,
) {
  const params = new URLSearchParams(searchParams)
  params.delete('panel')
  params.delete('entityId')
  setSearchParams(params, { replace: false })
  window.setTimeout(() => selectedRowRef.current?.focus(), 0)
}

function panelForPriorityItem(item: PriorityItem): PanelState {
  if (item.type === 'note') return { type: 'note', entityId: item.drillDownTarget.routeParameters.noteId ?? item.reference }
  if (item.type === 'corrective-action') return { type: 'corrective-action', entityId: item.drillDownTarget.routeParameters.id ?? item.reference }
  if (item.type === 'form') return { type: 'form', entityId: item.reference }
  if (item.type === 'escalation') return { type: 'escalation', entityId: item.reference }
  return { type: 'activity', entityId: item.reference }
}

function panelForActivityItem(item: ActivityItem, index: number): PanelState {
  if (item.drillDownTarget.routeKey === 'notes.workspace' && item.drillDownTarget.routeParameters.noteId) {
    return { type: 'note', entityId: item.drillDownTarget.routeParameters.noteId }
  }
  if (item.drillDownTarget.routeKey === 'corrective-actions.list' && item.drillDownTarget.routeParameters.id) {
    return { type: 'corrective-action', entityId: item.drillDownTarget.routeParameters.id }
  }
  if (item.drillDownTarget.routeKey === 'form-compliance.facility') {
    return { type: 'form', entityId: item.entityReference }
  }
  if (item.drillDownTarget.routeKey === 'escalations.occurrences') {
    return { type: 'escalation', entityId: item.entityReference }
  }
  return { type: 'activity', entityId: `${item.entityReference}-${index}` }
}

function findPanelSummary(panel: PanelState, queue?: FacilityPriorityQueuePayload, activity?: FacilityRecentActivityPayload): PriorityItem | ActivityItem | undefined {
  const priority = queue?.items.find((item) => {
    const itemPanel = panelForPriorityItem(item)
    return itemPanel.type === panel.type && itemPanel.entityId === panel.entityId
  })
  if (priority) return priority
  return activity?.items.find((item, index) => {
    const itemPanel = panelForActivityItem(item, index)
    return itemPanel.type === panel.type && itemPanel.entityId === panel.entityId
  })
}

function legacyRouteForPanel(panel: PanelState, summary: PriorityItem | ActivityItem | undefined, shell: WorkspaceShellDto) {
  if (panel.type === 'note') return `/notes/workspace?noteId=${encodeURIComponent(panel.entityId)}`
  if (panel.type === 'corrective-action') return `/corrective-actions?id=${encodeURIComponent(panel.entityId)}`
  if (panel.type === 'form') return `/form-compliance/facilities/${shell.context.facilityId ?? ''}`
  if (panel.type === 'escalation') return '/settings/escalations/occurrences'
  if (summary && 'drillDownTarget' in summary) return routeFromTarget(summary.drillDownTarget)
  return '#'
}

function routeFromTarget(target: { routeKey: string; routeParameters: Record<string, string>; preservedFilters: Record<string, string> }) {
  if (target.routeKey === 'notes.workspace') return `/notes/workspace?noteId=${target.routeParameters.noteId ?? ''}`
  if (target.routeKey === 'corrective-actions.list') return `/corrective-actions?id=${target.routeParameters.id ?? ''}`
  if (target.routeKey === 'form-compliance.facility') return `/form-compliance/facilities/${target.routeParameters.facilityId ?? ''}`
  if (target.routeKey === 'escalations.occurrences') return '/settings/escalations/occurrences'
  return '#'
}

function executeNoteAction(action: NoteWorkspaceAllowedAction, data: NoteWorkspaceDetail, reason: string) {
  const body = { reason, rowVersion: data.note.rowVersion }
  if (action === 'SUBMIT') return api.notes.submit(data.note.id, body)
  if (action === 'START_WORK') return api.notes.startWork(data.note.id, body)
  if (action === 'REQUEST_VERIFICATION') return api.notes.submitForVerification(data.note.id, body)
  if (action === 'REJECT_VERIFICATION') return api.notes.returnForRework(data.note.id, body)
  if (action === 'REOPEN') return api.notes.reopen(data.note.id, body)
  if (action === 'CANCEL') return api.notes.cancel(data.note.id, body)
  throw new Error('هذا الإجراء يحتاج نموذجًا متقدمًا في الصفحة الكاملة.')
}

function noteActionLabel(action: NoteWorkspaceAllowedAction) {
  const labels: Record<NoteWorkspaceAllowedAction, string> = {
    SUBMIT: 'فتح الملاحظة',
    ASSIGN: 'إسناد',
    REASSIGN: 'إعادة إسناد',
    START_WORK: 'بدء المعالجة',
    ADD_ACTION: 'إضافة إجراء',
    REQUEST_VERIFICATION: 'طلب تحقق',
    REJECT_VERIFICATION: 'رفض التحقق',
    REOPEN: 'إعادة فتح',
    CANCEL: 'إلغاء',
  }
  return labels[action] ?? action
}

function toCorrectiveActionTimeline(row: CorrectiveActionStatusHistoryEntry) {
  return { title: row.toStatusAr, at: row.changedAtUtc, tone: 'info' as const }
}

function priorityTone(item: PriorityItem): WorkspaceVisualTone {
  if (item.priorityRank >= 85) return 'danger'
  if (item.priorityRank >= 70) return 'warn'
  return 'info'
}

function statusToneFor(status?: string): WorkspaceVisualTone {
  if (status === 'critical') return 'danger'
  if (status === 'intervention') return 'warn'
  if (status === 'follow-up' || status === 'attention') return 'info'
  return 'ok'
}

function confidenceTone(level: number): WorkspaceVisualTone {
  if (level === 1) return 'ok'
  if (level === 2) return 'warn'
  if (level === 3) return 'danger'
  return 'muted'
}

function notesPulseTone(notes?: FacilityNotesOverviewPayload): WorkspaceVisualTone {
  if ((notes?.criticalNotes ?? 0) > 0) return 'danger'
  if ((notes?.overdueNotes ?? 0) > 0) return 'warn'
  return 'ok'
}

function alertsPulseTone(alerts?: FacilityAlertsEscalationsPayload): WorkspaceVisualTone {
  if ((alerts?.criticalEscalations ?? 0) > 0) return 'danger'
  if ((alerts?.openEscalations ?? 0) > 0) return 'warn'
  return 'muted'
}

function panelLabel(type: PanelType) {
  if (type === 'note') return 'ملاحظة تشغيلية'
  if (type === 'corrective-action') return 'إجراء تصحيحي'
  if (type === 'escalation') return 'تصعيد'
  if (type === 'form') return 'التزام نموذج'
  return 'حدث تشغيلي'
}

function summaryReference(summary?: PriorityItem | ActivityItem) {
  if (!summary) return '-'
  return 'reference' in summary ? summary.reference : summary.entityReference
}

function summaryTitle(summary?: PriorityItem | ActivityItem) {
  return summary ? summary.titleAr : '-'
}

function summaryReason(summary?: PriorityItem | ActivityItem) {
  if (!summary) return '-'
  if ('reasonAr' in summary) return summary.reasonAr
  return summary.descriptionAr ?? '-'
}

function summaryDue(summary?: PriorityItem | ActivityItem) {
  return summary && 'dueAtUtc' in summary && summary.dueAtUtc ? formatDate(summary.dueAtUtc) : '-'
}

function formatDate(value: string) {
  return DATE_FORMAT.format(new Date(value))
}

function formatShortDate(value?: string) {
  return value ? SHORT_DATE_FORMAT.format(new Date(value)) : '-'
}
