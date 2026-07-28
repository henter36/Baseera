import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router'
import {
  ApiError,
  api,
  type CorrectiveActionDetail,
  type CorrectiveActionStatusHistoryEntry,
  type CreateNoteRequest,
  type FacilityUnit,
  type NoteType,
  type FacilityAlertsEscalationsPayload,
  type FacilityCorrectiveActionsPayload,
  type FacilityExecutiveSummaryPayload,
  type FacilityFormCompliancePayload,
  type FacilityHeaderPayload,
  type FacilityDataQualityPayload,
  type FacilityNotesOverviewPayload,
  type OccupancyUnitPayload,
  type OccupancyWorkspacePayload,
  type DutyRosterPayload,
  type ResourceWorkspacePayload,
  type SensitiveCustodyWorkspacePayload,
  type WorkforceCoverageRowPayload,
  type WorkforceCoverageStatus,
  type WorkforceUnitCoveragePayload,
  type WorkforceWorkspacePayload,
  type FacilityPriorityQueuePayload,
  type FacilityRecentActivityPayload,
  type FacilityStructurePayload,
  type RiskWorkspacePayload,
  type RiskWorkspaceSummary,
  type RiskDetail,
  type RiskListItem,
  type RiskCommandBody,
  type NoteWorkspaceAllowedAction,
  type NoteWorkspaceDetail,
  type WorkspaceConfidence,
  type WorkspaceFilters,
  type WorkspaceShell as WorkspaceShellDto,
  type WorkspaceVisualTone,
  type WorkspaceWidgetEnvelope,
} from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import { NoteSeverityLabelsAr, enumOptions } from '../../notes/noteEnums'
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

const PANEL_TYPES = [
  'note',
  'note-create',
  'corrective-action',
  'escalation',
  'form-assignment',
  'facility-unit',
  'incident',
  'risk',
  'vehicle',
  'weapon',
  'communication-device',
  'equipment',
  'project',
  'emergency-plan',
  'decision',
  'activity',
  'workforce-member',
  'workforce-shift',
  'workforce-role',
  'workforce-gap',
  'workforce-unit',
  'workforce-roster',
  'workforce-requirement',
  'workforce-qualification',
  'workforce-critical-position',
  'custody-transaction',
  'armory-location',
  'ammunition-lot',
  'ammunition-transaction',
  'inventory-session',
  'inventory-discrepancy',
  'weapon-inspection',
  'maintenance-work-order',
  'requirement-gap',
] as const

type PanelType = (typeof PANEL_TYPES)[number]

type SectionKey =
  | 'overview'
  | 'urgent'
  | 'operations'
  | 'occupancy'
  | 'resources'
  | 'sensitive-custody'
  | 'workforce'
  | 'risks'
  | 'projects'
  | 'compliance'
  | 'plans'
  | 'decisions'
  | 'timeline'
  | 'data-quality'

type PanelState = Readonly<{
  type: PanelType
  entityId: string
}>

type PriorityItem = FacilityPriorityQueuePayload['items'][number]
type ActivityItem = FacilityRecentActivityPayload['items'][number]
type FacilityUnitItem = FacilityStructurePayload['units'][number]
type DataQualityDomain = FacilityDataQualityPayload['domains'][number]
type PanelSummary = PriorityItem | ActivityItem | FacilityUnitItem | OccupancyUnitPayload | DataQualityDomain
type WorkforceActionCenterItem =
  | Readonly<{ id: string; label: string; execute: () => void }>
  | Readonly<{ id: string; label: string; panel: PanelState }>

type CommandData = Readonly<{
  header?: FacilityHeaderPayload
  executive?: FacilityExecutiveSummaryPayload
  notes?: FacilityNotesOverviewPayload
  actions?: FacilityCorrectiveActionsPayload
  alerts?: FacilityAlertsEscalationsPayload
  forms?: FacilityFormCompliancePayload
  occupancy?: OccupancyWorkspacePayload
  resources?: ResourceWorkspacePayload
  sensitiveCustody?: SensitiveCustodyWorkspacePayload
  workforce?: WorkforceWorkspacePayload
  risk?: RiskWorkspacePayload
  priority?: FacilityPriorityQueuePayload
  activity?: FacilityRecentActivityPayload
  structure?: FacilityStructurePayload
  dataQuality?: FacilityDataQualityPayload
}>

const SECTION_NAV: ReadonlyArray<Readonly<{ key: SectionKey; label: string }>> = [
  { key: 'overview', label: 'المشهد العام' },
  { key: 'urgent', label: 'العمل العاجل' },
  { key: 'operations', label: 'التشغيل والوقوعات' },
  { key: 'occupancy', label: 'الإشغال والنزلاء' },
  { key: 'resources', label: 'الموارد والجاهزية' },
  { key: 'sensitive-custody', label: 'الأسلحة والعهد الحساسة' },
  { key: 'workforce', label: 'القوى البشرية والتغطية' },
  { key: 'risks', label: 'المخاطر والمعالجات' },
  { key: 'projects', label: 'المشاريع والمبادرات' },
  { key: 'compliance', label: 'النماذج والالتزام' },
  { key: 'plans', label: 'الخطط والطوارئ' },
  { key: 'decisions', label: 'القرارات والتوجيهات' },
  { key: 'timeline', label: 'السجل التشغيلي' },
  { key: 'data-quality', label: 'جودة البيانات' },
]

export function FacilityWorkspacePage() {
  const { facilityId } = useParams()
  const navigate = useNavigate()
  const canViewWorkspace = usePermission('Workspaces.View')
  const canViewFacility = usePermission('Workspaces.ViewFacility')
  const canView = canViewWorkspace && canViewFacility
  const [searchParams, setSearchParams] = useSearchParams()
  const queryClient = useQueryClient()
  const [isActionCenterOpen, setIsActionCenterOpen] = useState(false)
  const selectedRowRef = useRef<HTMLButtonElement | null>(null)
  const activeSection = sectionFromSearch(searchParams)

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
  const setActiveSection = (section: SectionKey) => {
    const params = new URLSearchParams(searchParams)
    if (section === 'overview') {
      params.delete('section')
    } else {
      params.set('section', section)
    }
    setSearchParams(params, { replace: false })
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
        onOpenNoteCreate={() => openPanel({ type: 'note-create', entityId: 'create' })}
      />

      <nav className="command-section-nav" aria-label="تنقل مركز القرار">
        {SECTION_NAV.map(({ key, label }) => (
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
        onReset={() => {
          const params = new URLSearchParams(searchParams)
          params.delete('fromUtc')
          params.delete('toUtc')
          params.delete('status')
          params.delete('severity')
          setSearchParams(params, { replace: true })
        }}
      />

      <div className="command-workspace-grid">
        <section className="command-main" aria-label="المشهد التشغيلي">
          <SituationOverview data={data} confidence={query.data.confidence} activeSection={activeSection} />
          <SectionDeck data={data} activeSection={activeSection} openPanel={openPanel} selectedPanel={panel} selectedRowRef={selectedRowRef} />
        </section>

        {activeSection !== 'urgent' && (
          <InterventionQueue
            payload={data.priority}
            selectedPanel={panel}
            openPanel={openPanel}
            selectedRowRef={selectedRowRef}
          />
        )}
      </div>

      {panel && (
        <CommandContextPanel
          panel={panel}
          facilityId={facilityId}
          shell={query.data}
          queue={data.priority}
          activity={data.activity}
          structure={data.structure}
          occupancy={data.occupancy}
          dataQuality={data.dataQuality}
          onClose={() => closePanel(searchParams, setSearchParams, selectedRowRef)}
          onChanged={() => {
            query.refetch()
            queryClient.invalidateQueries({ queryKey: ['workspace-panel'] })
          }}
          onNoteCreated={(noteId) => {
            const params = new URLSearchParams({
              noteId,
              facilityId,
              source: `facility:${facilityId}`,
            })
            navigate(buildUrl('/notes/workspace', params))
          }}
          facilityNameAr={data.header?.facilityNameAr ?? query.data.definition.titleAr}
        />
      )}

      {isActionCenterOpen && (
        <ActionCenter facilityId={facilityId} data={data} onClose={() => setIsActionCenterOpen(false)} openPanel={openPanel} />
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
  onOpenNoteCreate,
}: Readonly<{
  shell: WorkspaceShellDto
  data: CommandData
  filters: WorkspaceFilters
  onRefresh: () => void
  onOpenActions: () => void
  onOpenNoteCreate: () => void
}>) {
  const statusTone = statusToneFor(data.executive?.statusCode)
  const canCreateNote = usePermission('Notes.Create')
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
        {canCreateNote && (
          <button type="button" className="command-button" onClick={onOpenNoteCreate}>فتح ملاحظة</button>
        )}
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
}: Readonly<{ data: CommandData; confidence: WorkspaceConfidence; activeSection: SectionKey }>) {
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
  const occupancy = domainFor(data, 'occupancy')
  const resources = domainFor(data, 'resources')
  const sensitiveCustody = domainFor(data, 'sensitive-custody')
  const incidents = domainFor(data, 'incidents')
  const risks = domainFor(data, 'risks')
  const projects = domainFor(data, 'projects')
  const plans = domainFor(data, 'plans')
  const decisions = domainFor(data, 'decisions')
  return (
    <div className="operational-pulse" aria-label="نبض التشغيل">
      <OperationalPulseItem
        label="الإشغال"
        value={data.occupancy?.summary.occupancyRate == null ? occupancy?.statusAr ?? 'غير متاح' : `${Math.round(data.occupancy.summary.occupancyRate * 100)}%`}
        detail={data.occupancy ? `${data.occupancy.summary.currentCount ?? '-'} نزيل · طاقة ${data.occupancy.summary.approvedCapacity ?? '-'}` : occupancy?.impactAr ?? 'لا توجد بيانات إشغال'}
        tone={occupancyTone(data.occupancy?.summary.statusCode) ?? domainTone(occupancy)}
      />
      <OperationalPulseItem
        label="الجاهزية"
        value={resources?.statusAr ?? 'غير متاح'}
        detail={resources?.impactAr ?? 'لا توجد بيانات موارد'}
        tone={domainTone(resources)}
      />
      <OperationalPulseItem
        label="العهد الحساسة"
        value={data.sensitiveCustody?.summary.readinessRate == null ? sensitiveCustody?.statusAr ?? 'غير متاح' : `${Math.round(data.sensitiveCustody.summary.readinessRate * 100)}%`}
        detail={data.sensitiveCustody ? `${data.sensitiveCustody.summary.serviceableWeapons} جاهز · ${data.sensitiveCustody.summary.missingOrUnaccountedWeapons} مفقود/غير مطابق` : sensitiveCustody?.impactAr ?? 'لا توجد بيانات عهد حساسة'}
        tone={sensitiveCustodyTone(data.sensitiveCustody)}
      />
      <OperationalPulseItem
        label="الوقوعات"
        value={incidents?.statusAr ?? 'غير متاح'}
        detail={incidents?.impactAr ?? 'لا يوجد نموذج وقائع مستقل'}
        tone={domainTone(incidents)}
      />
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
      <OperationalPulseItem
        label="المخاطر"
        value={data.risk ? `${data.risk.summary.openRisks} مفتوح` : risks?.statusAr ?? 'غير متاح'}
        detail={data.risk ? `${data.risk.summary.criticalRisks} حرجة · ${data.risk.summary.overdueTreatmentActions} معالجة متأخرة` : risks?.impactAr ?? 'مصدر بيانات المخاطر غير متاح حاليًا'}
        tone={riskPulseTone(data.risk)}
      />
      <OperationalPulseItem label="المشاريع" value={projects?.statusAr ?? 'غير متاح'} detail={projects?.impactAr ?? 'مصدر بيانات المشاريع غير متاح حاليًا'} tone={domainTone(projects)} />
      <OperationalPulseItem label="الخطط" value={plans?.statusAr ?? 'غير متاح'} detail={plans?.impactAr ?? 'مصدر بيانات الخطط غير متاح حاليًا'} tone={domainTone(plans)} />
      <OperationalPulseItem label="القرارات" value={decisions?.statusAr ?? 'غير متاح'} detail={decisions?.impactAr ?? 'مصدر بيانات القرارات غير متاح حاليًا'} tone={domainTone(decisions)} />
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
  activeSection: SectionKey
  openPanel: (panel: PanelState) => void
  selectedPanel: PanelState | null
  selectedRowRef: React.MutableRefObject<HTMLButtonElement | null>
}>) {
  if (activeSection === 'urgent') {
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

  if (activeSection === 'overview') {
    return (
      <CommandSection title="قراءة المجالات التشغيلية">
        <DomainCoverageMap data={data} openPanel={openPanel} />
      </CommandSection>
    )
  }

  if (activeSection === 'operations') {
    return (
      <CommandSection title="التشغيل والوقوعات">
        <OperationsIncidentsSection data={data} openPanel={openPanel} selectedPanel={selectedPanel} selectedRowRef={selectedRowRef} />
      </CommandSection>
    )
  }

  if (activeSection === 'occupancy') {
    return (
      <CommandSection title="الإشغال والنزلاء">
        <OccupancySection data={data} openPanel={openPanel} />
      </CommandSection>
    )
  }

  if (activeSection === 'resources') {
    return (
      <CommandSection title="الموارد والجاهزية">
        <ResourcesReadinessSection data={data} openPanel={openPanel} />
      </CommandSection>
    )
  }

  if (activeSection === 'sensitive-custody') {
    return (
      <CommandSection title="الأسلحة والعهد الحساسة">
        <SensitiveCustodySection data={data} openPanel={openPanel} />
      </CommandSection>
    )
  }

  if (activeSection === 'workforce') {
    return (
      <CommandSection title="القوى البشرية والتغطية">
        <WorkforceCoverageSection data={data} openPanel={openPanel} />
      </CommandSection>
    )
  }

  if (activeSection === 'risks') {
    return <CommandSection title="المخاطر والمعالجات"><RiskSection data={data} openPanel={openPanel} /></CommandSection>
  }

  if (activeSection === 'projects') {
    return <CommandSection title="المشاريع والمبادرات"><DomainUnavailableSection domain={domainFor(data, 'projects')} panelType="project" openPanel={openPanel} /></CommandSection>
  }

  if (activeSection === 'compliance') {
    return <CommandSection title="الالتزام بالنماذج"><FormCompliance payload={data.forms} /></CommandSection>
  }

  if (activeSection === 'plans') {
    return <CommandSection title="الخطط والطوارئ"><DomainUnavailableSection domain={domainFor(data, 'plans')} panelType="emergency-plan" openPanel={openPanel} /></CommandSection>
  }

  if (activeSection === 'decisions') {
    return <CommandSection title="القرارات والتوجيهات"><DomainUnavailableSection domain={domainFor(data, 'decisions')} panelType="decision" openPanel={openPanel} /></CommandSection>
  }

  if (activeSection === 'timeline') {
    return (
      <CommandSection title="السجل التشغيلي الموحد">
        <RecentActivity payload={data.activity} openPanel={openPanel} selectedPanel={selectedPanel} selectedRowRef={selectedRowRef} />
      </CommandSection>
    )
  }

  if (activeSection === 'data-quality') {
    return <CommandSection title="جودة البيانات"><DataQualitySection payload={data.dataQuality} openPanel={openPanel} /></CommandSection>
  }

  return <CommandSection title="القسم غير متاح"><WorkspaceEmpty message="القسم المطلوب غير معروف." /></CommandSection>
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
        <ul className="priority-row-list" aria-label="قائمة الأولويات">
          {items.map((item, index) => {
            const panel = panelForPriorityItem(item)
            const selected = selectedPanel?.type === panel.type && selectedPanel.entityId === panel.entityId
            return (
              <li key={`${item.type}-${item.reference}-${index}`}>
                <button
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
              </li>
            )
          })}
        </ul>
      )}
    </aside>
  )
}

function CommandContextPanel({
  panel,
  facilityId,
  shell,
  queue,
  activity,
  structure,
  occupancy,
  dataQuality,
  onClose,
  onChanged,
  onNoteCreated,
  facilityNameAr,
}: Readonly<{
  panel: PanelState
  facilityId: string
  shell: WorkspaceShellDto
  queue?: FacilityPriorityQueuePayload
  activity?: FacilityRecentActivityPayload
  structure?: FacilityStructurePayload
  occupancy?: OccupancyWorkspacePayload
  dataQuality?: FacilityDataQualityPayload
  onClose: () => void
  onChanged: () => void
  onNoteCreated: (noteId: string) => void
  facilityNameAr: string
}>) {
  const panelRef = useRef<HTMLDialogElement | null>(null)
  const summary = findPanelSummary(panel, queue, activity, structure, occupancy, dataQuality)
  const title = summary ? summaryTitle(summary) : panelLabel(panel.type)
  const fullPageRoute = legacyRouteForPanel(panel, summary, shell)

  useEffect(() => {
    panelRef.current?.focus()
  }, [panel.type, panel.entityId])

  return (
    <dialog
      ref={panelRef}
      className="command-context-panel"
      open
      tabIndex={-1}
      aria-labelledby="context-panel-title"
    >
      <div className="context-panel-toolbar">
        <button type="button" className="command-icon-button" onClick={onClose} aria-label="إغلاق لوحة التفاصيل">×</button>
        <span>{panelLabel(panel.type)}</span>
        {fullPageRoute && <Link className="command-button ghost" to={fullPageRoute}>فتح الصفحة الكاملة</Link>}
      </div>
      <div className="context-panel-summary">
        {panel.type !== 'note-create' && (
          <span className="command-eyebrow">{summaryReference(summary) || panel.entityId}</span>
        )}
        <h2 id="context-panel-title">{title}</h2>
        {panel.type !== 'note-create' && summaryReason(summary) !== '-' && <p>{summaryReason(summary)}</p>}
      </div>
      <PanelDetail
        panel={panel}
        facilityId={facilityId}
        regionId={shell.context.regionId ?? undefined}
        summary={summary}
        onChanged={onChanged}
        onNoteCreated={onNoteCreated}
        facilityNameAr={facilityNameAr}
      />
    </dialog>
  )
}

function PanelDetail({
  panel,
  facilityId,
  regionId,
  summary,
  onChanged,
  onNoteCreated,
  facilityNameAr,
}: Readonly<{
  panel: PanelState
  facilityId: string
  regionId?: string
  summary?: PanelSummary
  onChanged: () => void
  onNoteCreated: (noteId: string) => void
  facilityNameAr: string
}>) {
  if (panel.type === 'note') {
    return <NotePanel noteId={panel.entityId} summary={summary} onChanged={onChanged} />
  }

  if (panel.type === 'note-create') {
    return (
      <NoteCreatePanel
        facilityId={facilityId}
        facilityNameAr={facilityNameAr}
        regionId={regionId}
        initialUnitId={panel.entityId === 'create' ? undefined : panel.entityId}
        onCreated={onNoteCreated}
      />
    )
  }

  if (panel.type === 'corrective-action') {
    return <CorrectiveActionPanel actionId={panel.entityId} summary={summary} />
  }

  if (panel.type === 'risk' && !panel.entityId.startsWith('domain-')) {
    return <RiskPanel facilityId={facilityId} riskId={panel.entityId} onChanged={onChanged} />
  }

  if (panel.type === 'form-assignment') {
    return <FormPreviewPanel summary={summary} />
  }

  if (panel.type === 'escalation') {
    return <EscalationPreviewPanel summary={summary} />
  }

  if (panel.type === 'facility-unit') {
    return <FacilityUnitPanelDetail panel={panel} summary={summary} />
  }

  if (isWorkforcePanelType(panel.type)) {
    return <WorkforcePreviewPanel type={panel.type} summary={summary} entityId={panel.entityId} />
  }

  if (isSensitiveCustodyPanelType(panel.type)) {
    return <SensitiveCustodyPreviewPanel type={panel.type} summary={summary} entityId={panel.entityId} />
  }

  if (panel.entityId.startsWith('domain-') && summary && 'statusCode' in summary) {
    return <DomainGapPanel type={panel.type} summary={summary} />
  }

  if (panel.type !== 'activity') {
    return <DomainGapPanel type={panel.type} summary={summary} />
  }

  return <ActivityPreviewPanel summary={summary} />
}

function FacilityUnitPanelDetail({
  panel,
  summary,
}: Readonly<{
  panel: PanelState
  summary?: PanelSummary
}>) {
  if (summary && 'unitNameAr' in summary) {
    return <OccupancyUnitPanel summary={summary} />
  }

  if (summary && 'unitId' in summary) {
    return <FacilityUnitPanel summary={summary} />
  }

  return <DomainGapPanel type={panel.type} summary={summary} />
}

function SensitiveCustodyPreviewPanel({ type, summary, entityId }: Readonly<{ type: PanelType; summary?: PanelSummary; entityId: string }>) {
  return (
    <div className="context-stack">
      <ContextSection title={panelLabel(type)}>
        <StatusRail
          tone={summary && 'priorityRank' in summary ? priorityTone(summary) : 'warn'}
          rows={[
            ['المرجع', summaryReference(summary) || entityId],
            ['الحالة', summaryTitle(summary)],
            ['السبب', summaryReason(summary)],
            ['الموعد', summaryDue(summary)],
            ['المصدر', 'العهد الحساسة'],
          ]}
        />
      </ContextSection>
      <div className="context-action-note">تعرض هذه اللوحة ملخصًا آمنًا فقط. لا تظهر الأرقام التسلسلية أو مواقع التخزين التفصيلية داخل مركز القرار العام.</div>
    </div>
  )
}

function NotePanel({ noteId, summary, onChanged }: Readonly<{ noteId: string; summary?: PanelSummary; onChanged: () => void }>) {
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
  let noteTone: WorkspaceVisualTone = 'info'

  if (detail.note.isOverdue) {
    noteTone = 'danger'
  } else if (summary && 'priorityRank' in summary) {
    noteTone = priorityTone(summary)
  }

  return (
    <div className="context-stack">
      <ContextSection title="ملخص الملاحظة">
        <StatusRail
          tone={noteTone}
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

// Create-note-in-context panel (Phase 1A #143 "فتح ملاحظة" from the Facility Workspace): Facility is
// fixed from the Route/props, never re-selected by the user; FacilityUnit only narrows scope within
// the same facility and permission, and the server independently re-derives/validates both (a
// client-supplied facilityId here is presentation state only — see NoteCommandService.CreateDraftAsync).
function NoteCreatePanel({
  facilityId,
  facilityNameAr,
  regionId,
  initialUnitId,
  onCreated,
}: Readonly<{
  facilityId: string
  facilityNameAr: string
  regionId?: string
  initialUnitId?: string
  onCreated: (noteId: string) => void
}>) {
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [noteTypeId, setNoteTypeId] = useState('')
  const [severity, setSeverity] = useState('1')
  const [facilityUnitId, setFacilityUnitId] = useState(initialUnitId ?? '')
  const [showAdvanced, setShowAdvanced] = useState(false)
  const [dueAtUtc, setDueAtUtc] = useState('')

  const noteTypesQuery = useQuery({ queryKey: ['note-create-panel-types'], queryFn: () => api.myNoteTypes() })
  const unitsQuery = useQuery({
    queryKey: ['note-create-panel-units', facilityId],
    queryFn: () => api.facilityUnits(facilityId),
  })
  const unitNameAr = unitsQuery.data?.items.find((unit) => unit.id === facilityUnitId)?.nameAr

  const mutation = useMutation({
    mutationFn: () => {
      const body: CreateNoteRequest = {
        title,
        description,
        noteTypeId,
        severity: Number(severity),
        sourceType: 0,
        sourceReference: null,
        classification: 0,
        scopeType: facilityUnitId ? 4 : 3,
        regionId: regionId ?? null,
        facilityId,
        facilityUnitId: facilityUnitId || null,
        ownerDepartmentId: null,
        dueAtUtc: dueAtUtc ? new Date(dueAtUtc).toISOString() : null,
      }
      return api.notes.create(body)
    },
    onSuccess: (created) => onCreated(created.id),
  })

  const canSubmit = title.trim().length >= 3 && description.trim().length >= 3 && Boolean(noteTypeId) && !mutation.isPending

  return (
    <form
      className="context-stack note-create-panel"
      onSubmit={(event) => { event.preventDefault(); if (canSubmit) mutation.mutate() }}
    >
      <ContextSection title="السياق">
        <StatusRail
          tone="info"
          rows={[
            ['السجن', facilityNameAr],
            ['الوحدة', unitNameAr ?? 'بلا وحدة محددة'],
          ]}
        />
      </ContextSection>

      <label>
        <span>نوع الملاحظة</span>
        <select value={noteTypeId} onChange={(event) => setNoteTypeId(event.target.value)} required>
          <option value="">اختر النوع</option>
          {(noteTypesQuery.data as NoteType[] | undefined)?.map((type) => (
            <option key={type.id} value={type.id}>{type.nameAr}</option>
          ))}
        </select>
      </label>
      <label>
        <span>العنوان</span>
        <input value={title} onChange={(event) => setTitle(event.target.value)} required />
      </label>
      <label>
        <span>الوصف</span>
        <textarea value={description} onChange={(event) => setDescription(event.target.value)} rows={3} required />
      </label>
      <label>
        <span>الوحدة (اختياري)</span>
        <select value={facilityUnitId} onChange={(event) => setFacilityUnitId(event.target.value)}>
          <option value="">بلا وحدة محددة</option>
          {(unitsQuery.data?.items as FacilityUnit[] | undefined)?.map((unit) => (
            <option key={unit.id} value={unit.id}>{unit.nameAr}</option>
          ))}
        </select>
      </label>
      <label>
        <span>الخطورة</span>
        <select value={severity} onChange={(event) => setSeverity(event.target.value)}>
          {enumOptions(NoteSeverityLabelsAr).map((option) => <option key={option.value} value={option.value}>{option.labelAr}</option>)}
        </select>
      </label>

      <button type="button" className="command-button ghost" onClick={() => setShowAdvanced((v) => !v)}>
        {showAdvanced ? 'إخفاء خيارات إضافية' : 'خيارات إضافية'}
      </button>
      {showAdvanced && (
        <label>
          <span>تاريخ الاستحقاق</span>
          <input type="datetime-local" value={dueAtUtc} onChange={(event) => setDueAtUtc(event.target.value)} />
        </label>
      )}

      {mutation.isError && (
        <div className="error" role="alert">{workspaceActionError(mutation.error)}</div>
      )}
      <button type="submit" className="command-button primary" disabled={!canSubmit}>
        {mutation.isPending ? 'جارٍ الحفظ…' : 'حفظ الملاحظة'}
      </button>
    </form>
  )
}

function CorrectiveActionPanel({ actionId }: Readonly<{ actionId: string; summary?: PanelSummary }>) {
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
  let historyContent: React.ReactNode = <PanelLoading />

  if (historyQuery.isError) {
    historyContent = <PanelError error={historyQuery.error} />
  } else if (historyQuery.data !== undefined) {
    historyContent = (
      <CompactTimeline
        rows={historyQuery.data.map(toCorrectiveActionTimeline)}
      />
    )
  }

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
        {historyContent}
      </ContextSection>
      <div className="context-action-note">الإجراءات المركبة لهذا الإجراء متاحة من الصفحة الكاملة حتى يتم استخراج نماذجها داخل مركز القرار.</div>
    </div>
  )
}

function RiskPanel({
  facilityId,
  riskId,
  onChanged,
}: Readonly<{ facilityId: string; riskId: string; onChanged: () => void }>) {
  const queryClient = useQueryClient()
  const [reasonDrafts, setReasonDrafts] = useState<Record<string, string>>({})
  const draftFor = (command: string) => reasonDrafts[command] ?? ''
  const detailQuery = useQuery({
    queryKey: ['workspace-panel', 'risk', facilityId, riskId],
    queryFn: () => api.risks.get(facilityId, riskId),
  })

  const commandMutation = useMutation({
    mutationFn: (body: RiskCommandBody) => api.risks.executeCommand(facilityId, riskId, body),
    onSuccess: () => {
      setReasonDrafts({})
      void queryClient.invalidateQueries({ queryKey: ['workspace-panel', 'risk', facilityId, riskId] })
      void queryClient.invalidateQueries({ queryKey: ['risk-register'] })
      onChanged()
    },
  })

  if (detailQuery.isLoading) return <PanelLoading />
  if (detailQuery.isError) return <PanelError error={detailQuery.error} />
  if (!detailQuery.data) return <WorkspaceEmpty message="لا توجد تفاصيل متاحة." />

  const risk = detailQuery.data
  const conflict = commandMutation.error instanceof ApiError && commandMutation.error.status === 409

  const runCommand = (command: string, reason?: string) => {
    commandMutation.mutate({ command, reason, rowVersion: risk.rowVersion })
  }

  const wiredActions = new Set(['StartMonitoring', 'Escalate', 'Reopen'])
  const informationalActions = risk.allowedActions.filter((action) => !wiredActions.has(action))

  return (
    <div className="context-stack">
      <ContextSection title="ملخص الخطر">
        <StatusRail
          tone={risk.isDataStale ? 'warn' : 'info'}
          rows={[
            ['الرمز', risk.riskCode],
            ['العنوان', risk.title],
            ['التصنيف', risk.categoryNameAr],
            ['الحالة', risk.statusAr],
            ['المالك', risk.ownerDisplayName ?? 'بلا مالك'],
            ['موعد المراجعة القادم', risk.nextReviewDueAtUtc ? formatDate(risk.nextReviewDueAtUtc) : '-'],
          ]}
        />
      </ContextSection>

      <ContextSection title="التقييم والاتجاه">
        <StatusRail
          tone="info"
          rows={[
            ['التقييم الأصلي', scoreSummary(risk.inherentAssessment)],
            ['التقييم الحالي', scoreSummary(risk.currentAssessment)],
            ['التقييم المتبقي', scoreSummary(risk.residualAssessment)],
            ['الاتجاه', `${risk.trendAr} — ${risk.trendReasonAr}`],
          ]}
        />
      </ContextSection>

      <ContextSection title="الضوابط والمعالجة والمصادر">
        <StatusRail
          tone={risk.overdueTreatmentActionCount > 0 ? 'danger' : 'info'}
          rows={[
            ['ضوابط قائمة', String(risk.openControlCount)],
            ['خطط معالجة مفتوحة', String(risk.openTreatmentPlanCount)],
            ['إجراءات متأخرة', String(risk.overdueTreatmentActionCount)],
            ['مصادر وأدلة مرتبطة', String(risk.sourceCount)],
          ]}
        />
      </ContextSection>

      {risk.allowedActions.includes('StartMonitoring') && (
        <ContextSection title="بدء المتابعة">
          <button
            type="button"
            className="command-button"
            disabled={commandMutation.isPending}
            onClick={() => runCommand('StartMonitoring')}
          >
            بدء متابعة الخطر
          </button>
        </ContextSection>
      )}

      {risk.allowedActions.includes('Escalate') && (
        <ContextSection title="تصعيد الخطر">
          <textarea
            aria-label="سبب التصعيد"
            value={draftFor('Escalate')}
            onChange={(event) => setReasonDrafts((prev) => ({ ...prev, Escalate: event.target.value }))}
            placeholder="سبب التصعيد"
          />
          <button
            type="button"
            className="command-button"
            disabled={commandMutation.isPending || !draftFor('Escalate').trim()}
            onClick={() => runCommand('Escalate', draftFor('Escalate').trim())}
          >
            تصعيد
          </button>
        </ContextSection>
      )}

      {risk.allowedActions.includes('Reopen') && (
        <ContextSection title="إعادة فتح الخطر">
          <textarea
            aria-label="سبب إعادة الفتح"
            value={draftFor('Reopen')}
            onChange={(event) => setReasonDrafts((prev) => ({ ...prev, Reopen: event.target.value }))}
            placeholder="الدليل أو سبب إعادة الفتح"
          />
          <button
            type="button"
            className="command-button"
            disabled={commandMutation.isPending || !draftFor('Reopen').trim()}
            onClick={() => runCommand('Reopen', draftFor('Reopen').trim())}
          >
            إعادة فتح
          </button>
        </ContextSection>
      )}

      {commandMutation.isError && (
        <div className="context-action-note" data-tone="danger">
          {riskCommandErrorMessage(conflict, commandMutation.error)}
          {conflict && (
            <button type="button" className="command-button ghost" onClick={() => detailQuery.refetch()}>
              إعادة تحميل
            </button>
          )}
        </div>
      )}

      {informationalActions.length > 0 && (
        <div className="context-action-note">
          إجراءات إضافية متاحة من الصفحة الكاملة عند استخراجها: {informationalActions.join('، ')}
        </div>
      )}
    </div>
  )
}

function riskCommandErrorMessage(conflict: boolean, error: unknown): string {
  if (conflict) return 'حدث تعارض في RowVersion أو انتقال غير صالح. أعد تحميل البيانات والمحاولة مجددًا.'
  if (error instanceof ApiError) return error.message
  return 'تعذر تنفيذ الإجراء.'
}

function scoreSummary(assessment: RiskDetail['inherentAssessment']): string {
  if (!assessment) return 'لا يوجد تقييم معتمد'
  return `${assessment.calculatedScore} (${assessment.ratingBandLabelAr})`
}

function EscalationPreviewPanel({ summary }: Readonly<{ summary?: PanelSummary }>) {
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

function FormPreviewPanel({ summary }: Readonly<{ summary?: PanelSummary }>) {
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

function FacilityUnitPanel({ summary }: Readonly<{ summary: FacilityUnitItem }>) {
  return (
    <div className="context-stack">
      <ContextSection title="ملخص الوحدة">
        <StatusRail
          tone={summary.overdueNotes > 0 ? 'warn' : 'info'}
          rows={[
            ['الكود', summary.code],
            ['الوحدة', summary.nameAr],
            ['الوحدة الأم', summary.parentUnitNameAr ?? '-'],
            ['ملاحظات مفتوحة', String(summary.openNotes)],
            ['ملاحظات متأخرة', String(summary.overdueNotes)],
            ['إجراءات مفتوحة', String(summary.openCorrectiveActions)],
          ]}
        />
      </ContextSection>
      <div className="context-action-note">لا توجد بيانات إشغال نزلاء أو سعة معتمدة مرتبطة بهذه الوحدة حاليًا؛ تعرض اللوحة المؤشرات التشغيلية المتاحة فقط.</div>
    </div>
  )
}

function OccupancyUnitPanel({ summary }: Readonly<{ summary: OccupancyUnitPayload }>) {
  return (
    <div className="context-stack">
      <ContextSection title="إشغال الوحدة">
        <StatusRail
          tone={occupancyTone(summary.statusCode) ?? 'info'}
          rows={[
            ['الكود', summary.unitCode],
            ['الوحدة', summary.unitNameAr],
            ['الحالة', summary.statusAr],
            ['الطاقة المعتمدة', summary.approvedCapacity == null ? '-' : String(summary.approvedCapacity)],
            ['العدد الحالي', summary.currentCount == null ? '-' : String(summary.currentCount)],
            ['نسبة الإشغال', summary.occupancyRate == null ? '-' : `${Math.round(summary.occupancyRate * 100)}%`],
            ['الشواغر', summary.availablePlaces == null ? '-' : String(summary.availablePlaces)],
            ['التجاوز', summary.overloadCount == null ? '-' : String(summary.overloadCount)],
            ['آخر تحديث', formatShortDate(summary.lastUpdatedAtUtc ?? undefined)],
            ['المصدر', summary.dataSourceAr],
          ]}
        />
      </ContextSection>
      <ContextSection title="المتابعة التشغيلية">
        <StatusRail
          tone={summary.alertReasons.length > 0 ? 'warn' : 'ok'}
          rows={[
            ['ملاحظات مفتوحة', String(summary.openNotesCount)],
            ['وقوعات مفتوحة', String(summary.openIncidentsCount)],
            ['مخاطر مرتبطة', String(summary.riskCount)],
            ['أسباب التنبيه', summary.alertReasons.length > 0 ? summary.alertReasons.join('، ') : 'لا توجد'],
          ]}
        />
      </ContextSection>
      <div className="context-action-note">لا تعرض هذه اللوحة هوية النزلاء. الإجراءات المتقدمة مثل تسجيل Snapshot أو تحديث الطاقة متاحة من صفحة إدارة الإشغال حسب الصلاحية.</div>
    </div>
  )
}

function DomainGapPanel({ type, summary }: Readonly<{ type: PanelType; summary?: PanelSummary }>) {
  const domain = summary && 'key' in summary ? summary : undefined
  return (
    <div className="context-stack">
      <ContextSection title={panelLabel(type)}>
        <StatusRail
          tone="muted"
          rows={[
            ['الحالة', domain?.statusAr ?? 'غير متاح'],
            ['الثقة', domain?.confidenceAr ?? 'غير معروفة'],
            ['الأثر', domain?.impactAr ?? 'لا يوجد مصدر بيانات مستقل لهذا المجال في النموذج الحالي.'],
            ['المتابعة', domain?.followUpIssue ?? 'موثقة كفجوة ضمن Phase D.3'],
          ]}
        />
      </ContextSection>
      <div className="context-action-note">لا يتم إنشاء بيانات بديلة أو استخدام الملاحظات ككيان بديل. يلزم نموذج Domain مستقل وصلاحياته قبل تمكين الإجراءات داخل اللوحة.</div>
    </div>
  )
}

function WorkforcePreviewPanel({
  type,
  summary,
  entityId,
}: Readonly<{ type: PanelType; summary?: PanelSummary; entityId: string }>) {
  const canViewMembers = usePermission('Workforce.ViewMembers')
  const canManageMembers = usePermission('Workforce.ManageMembers')
  const canManageRosters = usePermission('Workforce.ManageRosters')
  const canManageRequirements = usePermission('Workforce.ManageRequirements')
  const canManageQualifications = usePermission('Workforce.ManageQualifications')
  const canReconcile = usePermission('Workforce.Reconcile')
  const actions = workforcePanelActions(type, {
    canViewMembers,
    canManageMembers,
    canManageRosters,
    canManageRequirements,
    canManageQualifications,
    canReconcile,
  })

  return (
    <div className="context-stack">
      <ContextSection title={panelLabel(type)}>
        <StatusRail
          tone={summary && 'priorityRank' in summary && summary.priorityRank >= 900 ? 'danger' : 'warn'}
          rows={[
            ['النوع', panelLabel(type)],
            ['المرجع', summaryReference(summary) || entityId],
            ['العنوان', summaryTitle(summary)],
            ['السبب', summaryReason(summary)],
          ]}
        />
      </ContextSection>
      {actions.length > 0 ? (
        <ContextSection title="إجراءات مسموحة">
          <ul className="priority-row-list" aria-label="إجراءات لوحة القوى البشرية">
            {actions.map((action) => (
              <li key={action}>
                <article className="priority-row compact" data-tone="info">
                  <span className="priority-band" />
                  <span><strong>{action}</strong><small>تُنفَّذ من صفحة الإدارة وفق صلاحيات الخادم</small></span>
                </article>
              </li>
            ))}
          </ul>
        </ContextSection>
      ) : (
        <div className="context-action-note">لا توجد إجراءات مسموحة لهذا النوع بصلاحياتك الحالية. الخادم هو مصدر الحقيقة.</div>
      )}
      <div className="context-action-note">تفاصيل القوى البشرية الكاملة متاحة من صفحة الإدارة عند الحاجة إلى أعضاء أو جداول أو متطلبات.</div>
    </div>
  )
}

function workforcePanelActions(
  type: PanelType,
  permissions: Readonly<{
    canViewMembers: boolean
    canManageMembers: boolean
    canManageRosters: boolean
    canManageRequirements: boolean
    canManageQualifications: boolean
    canReconcile: boolean
  }>,
): string[] {
  const actions: string[] = []
  if (type === 'workforce-member' && permissions.canViewMembers) actions.push('عرض العضو')
  if (type === 'workforce-member' && permissions.canManageMembers) actions.push('تعديل العضو')
  if (type === 'workforce-roster' && permissions.canManageRosters) actions.push('إدارة الجدول')
  if (type === 'workforce-requirement' && permissions.canManageRequirements) actions.push('إدارة المتطلب')
  if (type === 'workforce-qualification' && permissions.canManageQualifications) actions.push('إدارة المؤهل')
  if (type === 'workforce-critical-position' && permissions.canReconcile) actions.push('متابعة المصالحة')
  if (type === 'workforce-gap' || type === 'workforce-unit' || type === 'workforce-shift' || type === 'workforce-role') {
    if (permissions.canViewMembers) actions.push('فتح مركز القوى البشرية')
  }
  return actions
}

function ActivityPreviewPanel({ summary }: Readonly<{ summary?: PanelSummary }>) {
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

function ActionCenter({
  facilityId,
  data,
  onClose,
  openPanel,
}: Readonly<{ facilityId: string; data: CommandData; onClose: () => void; openPanel: (panel: PanelState) => void }>) {
  const urgent = data.priority?.items.slice(0, 5) ?? []
  const missingDomains = data.dataQuality?.domains.filter((domain) => domain.statusCode === 'unavailable') ?? []
  const missingDomainChips = missingDomains.slice(0, 3)
  const queryClient = useQueryClient()
  const canManageRosters = usePermission('Workforce.ManageRosters')
  const canManageAssignments = usePermission('Workforce.ManageAssignments')
  const canManageQualifications = usePermission('Workforce.ManageQualifications')
  const canReconcile = usePermission('Workforce.Reconcile')
  const canImport = usePermission('Workforce.Import')
  const canViewCoverage = usePermission('Workforce.ViewCoverage')
  const publishRosterMutation = useMutation({
    mutationFn: async () => {
      const rosters = await api.workforce.rosters(facilityId)
      const draft = rosters.find(isDraftRoster)
      if (!draft) throw new Error('لا يوجد جدول مناوبة مسودة قابل للنشر.')
      await api.workforce.publishRoster(facilityId, draft.id)
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['workspace'] })
    },
  })
  const workforceActions: WorkforceActionCenterItem[] = [
    canManageRosters ? { id: 'publish-roster', label: 'نشر أول جدول مناوبة مسودة', execute: () => publishRosterMutation.mutate() } : null,
    canManageAssignments ? { id: 'assign-replacement', label: 'تعيين بديل', panel: { type: 'workforce-member' as const, entityId: 'action:replacement' } } : null,
    canManageAssignments ? { id: 'confirm-assignment', label: 'اعتماد تكليف', panel: { type: 'workforce-requirement' as const, entityId: 'action:assignment' } } : null,
    canManageQualifications ? { id: 'verify-qualification', label: 'التحقق من مؤهل', panel: { type: 'workforce-qualification' as const, entityId: 'action:qualification' } } : null,
    canManageQualifications ? { id: 'expired-qualification', label: 'معالجة شهادة منتهية', panel: { type: 'workforce-qualification' as const, entityId: 'action:qualification-expired' } } : null,
    canViewCoverage ? { id: 'review-restriction', label: 'مراجعة قيد تشغيلي', panel: { type: 'workforce-member' as const, entityId: 'action:restriction' } } : null,
    canManageAssignments ? { id: 'no-ops-location', label: 'عضو بلا موقع تشغيلي', panel: { type: 'workforce-gap' as const, entityId: 'action:missing-location' } } : null,
    canViewCoverage ? { id: 'no-alternate', label: 'منصب بلا بديل', panel: { type: 'workforce-critical-position' as const, entityId: 'action:no-alternate' } } : null,
    canImport ? { id: 'confirm-import', label: 'تأكيد استيراد', panel: { type: 'workforce-gap' as const, entityId: 'action:import' } } : null,
    canReconcile ? { id: 'reconcile', label: 'مصالحة فروقات', panel: { type: 'workforce-gap' as const, entityId: 'action:reconcile' } } : null,
    canViewCoverage ? { id: 'stale-data', label: 'تحديث بيانات قديمة', panel: { type: 'workforce-gap' as const, entityId: 'action:stale' } } : null,
  ].filter((item): item is NonNullable<typeof item> => item !== null)

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
        <CommandMetric label="نواقص بيانات" value={missingDomains.length} tone="muted" />
      </div>
      {workforceActions.length > 0 && (
        <ul className="priority-row-list" aria-label="إجراءات القوى البشرية المسموحة">
          {workforceActions.map((action) => (
            <li key={action.id}>
              <button
                type="button"
                className="priority-row compact"
                disabled={'execute' in action && publishRosterMutation.isPending}
                onClick={() => {
                  if ('execute' in action) {
                    action.execute()
                  } else {
                    openPanel(action.panel)
                  }
                }}
              >
                <span className="priority-band" aria-hidden="true" />
                <span className="priority-row-main">
                  <strong>{action.label}</strong>
                  <small>{'execute' in action ? 'ينفذ عبر API ثم يحدّث بيانات مساحة العمل' : 'يفتح الصفحة الكاملة لإكمال بيانات الإجراء'}</small>
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
      {publishRosterMutation.isSuccess && <output aria-live="polite">تم نشر جدول مناوبة مسودة وتحديث البيانات.</output>}
      {publishRosterMutation.isError && <div className="error" role="alert">{workspaceActionError(publishRosterMutation.error)}</div>}
      <ul className="priority-row-list" aria-label="الإجراءات العاجلة">
        {urgent.map((item) => (
          <li key={`${item.type}-${item.reference}`}>
            <button type="button" className="priority-row compact" onClick={() => openPanel(panelForPriorityItem(item))}>
              <span className="priority-band" aria-hidden="true" />
              <span className="priority-row-main"><strong>{item.titleAr}</strong><small>{item.reasonAr}</small></span>
            </button>
          </li>
        ))}
      </ul>
      {missingDomainChips.length > 0 && (
        <div className="domain-gap-strip" aria-label="مجالات تحتاج استكمال نموذج بيانات">
          {missingDomainChips.map((domain) => <span key={domain.key}>{domain.labelAr}</span>)}
        </div>
      )}
    </aside>
  )
}

function DomainCoverageMap({ data, openPanel }: Readonly<{ data: CommandData; openPanel: (panel: PanelState) => void }>) {
  const domains = data.dataQuality?.domains ?? []
  if (domains.length === 0) {
    return <WorkspaceEmpty message="لا توجد قراءة جودة بيانات متاحة." />
  }

  return (
    <div className="domain-coverage-grid">
      {domains.map((domain) => (
        <button
          key={domain.key}
          type="button"
          className="domain-coverage-row"
          data-status={domain.statusCode}
          onClick={() => openPanel(panelForDomain(domain))}
        >
          <span>{domain.labelAr}</span>
          <strong>{domain.statusAr}</strong>
          <small>{domain.impactAr}</small>
        </button>
      ))}
    </div>
  )
}

function OperationsIncidentsSection({
  data,
  openPanel,
  selectedPanel,
  selectedRowRef,
}: Readonly<{
  data: CommandData
  openPanel: (panel: PanelState) => void
  selectedPanel: PanelState | null
  selectedRowRef: React.MutableRefObject<HTMLButtonElement | null>
}>) {
  return (
    <div className="command-section-stack">
      <div className="command-metric-strip">
        <CommandMetric label="تصعيدات مفتوحة" value={data.alerts?.openEscalations ?? 0} tone="warn" />
        <CommandMetric label="تصعيدات حرجة" value={data.alerts?.criticalEscalations ?? 0} tone="danger" />
        <CommandMetric label="تنبيهات غير مقروءة" value={data.alerts?.personalUnreadNotifications ?? 0} tone="info" />
        <CommandMetric label="وقوعات مستقلة" value={domainFor(data, 'incidents')?.statusAr ?? 'غير متاح'} tone="muted" />
      </div>
      <NotesOverview payload={data.notes} />
      <CorrectiveActions payload={data.actions} />
      <DomainUnavailableSection domain={domainFor(data, 'incidents')} panelType="incident" openPanel={openPanel} compact />
      <RecentActivity payload={data.activity} openPanel={openPanel} selectedPanel={selectedPanel} selectedRowRef={selectedRowRef} />
    </div>
  )
}

function OccupancySection({
  data,
  openPanel,
}: Readonly<{ data: CommandData; openPanel: (panel: PanelState) => void }>) {
  const occupancy = data.occupancy
  const occupancyGap = domainFor(data, 'occupancy')
  if (!occupancy) {
    return (
      <div className="command-section-stack">
        <div className="readiness-rail">
          <CommandMetric label="وحدات مسجلة" value={data.structure?.unitsCount ?? 0} tone="info" />
          <CommandMetric label="مبانٍ" value={data.structure?.buildingsCount ?? 0} tone="info" />
          <CommandMetric label="مواقع أصول" value={data.structure?.assetLocationsCount ?? 0} tone="info" />
          <CommandMetric label="الإشغال" value={occupancyGap?.statusAr ?? 'غير متاح'} tone="muted" />
        </div>
        <UnitLoadRows units={data.structure?.units ?? []} openPanel={openPanel} />
        <DomainUnavailableSection domain={occupancyGap} panelType="facility-unit" openPanel={openPanel} compact />
      </div>
    )
  }

  return (
    <div className="command-section-stack">
      <div className="occupancy-command-strip" data-status={occupancy.summary.statusCode}>
        <div>
          <span className="command-eyebrow">مصدر الإشغال</span>
          <h3>{occupancy.summary.statusAr}</h3>
          <p>{occupancy.summary.sourceAr} · آخر Snapshot {formatShortDate(occupancy.summary.latestSnapshotAtUtc ?? undefined)}</p>
        </div>
        <strong>{occupancy.summary.occupancyRate == null ? '-' : `${Math.round(occupancy.summary.occupancyRate * 100)}%`}</strong>
      </div>
      <div className="readiness-rail">
        <CommandMetric label="الطاقة المعتمدة" value={occupancy.summary.approvedCapacity ?? '-'} tone="info" />
        <CommandMetric label="العدد الحالي" value={occupancy.summary.currentCount ?? '-'} tone={occupancyTone(occupancy.summary.statusCode) ?? 'info'} />
        <CommandMetric label="الشواغر" value={occupancy.summary.availablePlaces ?? '-'} tone="ok" />
        <CommandMetric label="التجاوز" value={occupancy.summary.overCapacityCount ?? 0} tone={(occupancy.summary.overCapacityCount ?? 0) > 0 ? 'danger' : 'ok'} />
      </div>
      <div className="movement-pulse" aria-label="حركة النزلاء">
        <CommandMetric label="دخول" value={occupancy.movementSummary.admissions} tone="info" />
        <CommandMetric label="إفراج" value={occupancy.movementSummary.releases} tone="ok" />
        <CommandMetric label="نقل داخلي" value={occupancy.movementSummary.internalTransfers} tone="info" />
        <CommandMetric label="صافي الحركة" value={occupancy.movementSummary.netMovement} tone={occupancy.movementSummary.netMovement > 0 ? 'warn' : 'ok'} />
      </div>
      <OccupancyUnitRows units={occupancy.unitBreakdown.units} openPanel={openPanel} />
      {occupancy.interventions.length > 0 && (
        <ul className="data-quality-list" aria-label="تدخلات الإشغال">
          {occupancy.interventions.map((item) => (
            <li key={`${item.type}-${item.reference}`}>
              <button type="button" data-status={item.priorityRank >= 900 ? 'unavailable' : 'partial'} onClick={() => openPanel({ type: 'facility-unit', entityId: item.unitId ?? `domain-occupancy` })}>
                <span>{item.titleAr}</span>
                <strong>{item.severityAr}</strong>
                <small>{item.reasonAr}</small>
              </button>
            </li>
          ))}
        </ul>
      )}
      {occupancy.summary.warnings.length > 0 && <div className="context-action-note">{occupancy.summary.warnings.join(' ')}</div>}
    </div>
  )
}

function OccupancyUnitRows({ units, openPanel }: Readonly<{ units: OccupancyUnitPayload[]; openPanel: (panel: PanelState) => void }>) {
  if (units.length === 0) {
    return <WorkspaceEmpty message="لا توجد قراءة إشغال حسب الوحدة." />
  }

  return (
    <ul className="unit-load-list occupancy-units" aria-label="إشغال الوحدات">
      {units.map((unit) => (
        <li key={unit.unitId}>
          <button type="button" data-status={unit.statusCode} onClick={() => openPanel({ type: 'facility-unit', entityId: unit.unitId })}>
            <span className="unit-load-title"><strong>{unit.unitNameAr}</strong><small>{unit.unitCode} · {unit.statusAr}</small></span>
            <span className="unit-capacity-bar" aria-label={`نسبة الإشغال ${unit.occupancyRate == null ? 'غير معروفة' : Math.round(unit.occupancyRate * 100) + '%'}`}>
              <i style={{ inlineSize: `${Math.min(120, Math.round((unit.occupancyRate ?? 0) * 100))}%` }} />
            </span>
            <span className="unit-load-values">
              <b>{unit.currentCount ?? '-'}</b><small>عدد</small>
              <b>{unit.approvedCapacity ?? '-'}</b><small>طاقة</small>
              <b>{unit.overloadCount ?? 0}</b><small>تجاوز</small>
            </span>
          </button>
        </li>
      ))}
    </ul>
  )
}

function ResourcesReadinessSection({ data, openPanel }: Readonly<{ data: CommandData; openPanel: (panel: PanelState) => void }>) {
  const resources = data.resources
  const resourceGap = domainFor(data, 'resources')
  if (!resources) {
    return (
      <div className="command-section-stack">
        <DomainUnavailableSection domain={resourceGap} panelType="equipment" openPanel={openPanel} compact />
      </div>
    )
  }

  return (
    <div className="command-section-stack">
      <div className="occupancy-command-strip" data-status={resources.summary.gap > 0 ? 'partial' : 'complete'}>
        <div>
          <span className="command-eyebrow">جاهزية الموارد</span>
          <h3>{resources.summary.readinessRate == null ? 'لا يوجد baseline احتياج' : `${Math.round(resources.summary.readinessRate * 100)}% جاهزية`}</h3>
          <p>{resources.summary.warnings.join(' ') || 'لا توجد تحذيرات موارد حالية.'}</p>
        </div>
        <strong>{resources.summary.operational}/{resources.summary.required || '-'}</strong>
      </div>
      <div className="readiness-rail">
        <CommandMetric label="الإجمالي" value={resources.summary.totalRegistered} tone="info" />
        <CommandMetric label="تشغيلي" value={resources.summary.operational} tone="ok" />
        <CommandMetric label="تحت الصيانة" value={resources.summary.underMaintenance} tone="warn" />
        <CommandMetric label="خارج الخدمة" value={resources.summary.outOfService} tone={resources.summary.outOfService > 0 ? 'danger' : 'ok'} />
        <CommandMetric label="الفجوة" value={resources.summary.gap} tone={resources.summary.gap > 0 ? 'danger' : 'ok'} />
      </div>
      <div className="resource-rail">
        {resources.categories.map((item) => {
          const railStatus = resourceCategoryRailStatus(item.gap, item.total)
          return (
            <button key={item.resourceTypeCode} type="button" data-status={railStatus} onClick={() => openPanel({ type: resourcePanelType(item.resourceTypeCode), entityId: `domain-resources-${item.resourceTypeCode}` })}>
              <span>{item.labelAr}</span>
              <strong>{item.readinessRate == null ? '-' : `${Math.round(item.readinessRate * 100)}%`}</strong>
              <small>{item.operational} تشغيلي · {item.underMaintenance} صيانة · فجوة {item.gap}</small>
            </button>
          )
        })}
      </div>
      {resources.exceptions.length > 0 && (
        <ul className="priority-row-list" aria-label="استثناءات الموارد">
          {resources.exceptions.map((item) => (
            <li key={`${item.type}-${item.reference}`}>
              <button type="button" className="priority-row compact" data-tone={item.priorityRank >= 900 ? 'danger' : 'warn'} onClick={() => openPanel({ type: resourcePanelType(item.resourceType == null ? undefined : String(item.resourceType)), entityId: item.resourceAssetId ?? `domain-resources-${item.type}` })}>
                <span className="priority-band" />
                <span><strong>{item.titleAr}</strong><small>{item.reference} · {item.reasonAr}</small></span>
                <b>{item.severityAr}</b>
              </button>
            </li>
          ))}
        </ul>
      )}
      {resources.unitDistribution.length > 0 && (
        <div className="resource-unit-grid">
          {resources.unitDistribution.map((unit) => (
            <article key={unit.facilityUnitId ?? unit.unitNameAr}>
              <span>{unit.unitNameAr}</span>
              <strong>{unit.readinessRate == null ? '-' : `${Math.round(unit.readinessRate * 100)}%`}</strong>
              <small>مركبات {unit.vehicles} · اتصال {unit.communicationDevices} · معدات {unit.equipment} · أصول {unit.facilityAssets}</small>
            </article>
          ))}
        </div>
      )}
    </div>
  )
}

function SensitiveCustodySection({ data, openPanel }: Readonly<{ data: CommandData; openPanel: (panel: PanelState) => void }>) {
  const custody = data.sensitiveCustody
  const custodyGap = domainFor(data, 'sensitive-custody')
  if (!custody) {
    return (
      <div className="command-section-stack">
        <DomainUnavailableSection domain={custodyGap} panelType="weapon" openPanel={openPanel} compact />
      </div>
    )
  }

  const summary = custody.summary
  const readiness = summary.readinessRate == null ? '-' : `${Math.round(summary.readinessRate * 100)}%`
  return (
    <div className="command-section-stack">
      <div className="occupancy-command-strip" data-status={sensitiveCustodyStatus(summary)}>
        <div>
          <span className="command-eyebrow">جاهزية العهد الحساسة</span>
          <h3>{readiness === '-' ? 'لا يوجد baseline حساس' : `${readiness} جاهزية`}</h3>
          <p>{summary.warnings.join(' ') || 'لا توجد تحذيرات عهد حساسة حالية.'}</p>
        </div>
        <strong>{summary.serviceableWeapons}/{summary.totalWeapons || '-'}</strong>
      </div>

      <div className="workforce-rail readiness-rail" aria-label="مؤشرات الأسلحة والعهد الحساسة">
        <CommandMetric label="إجمالي الأسلحة" value={summary.totalWeapons} tone="info" />
        <CommandMetric label="جاهز" value={summary.serviceableWeapons} tone="ok" />
        <CommandMetric label="مصروف" value={summary.issuedWeapons} tone="info" />
        <CommandMetric label="في المستودع" value={summary.inArmoryWeapons} tone="info" />
        <CommandMetric label="صيانة" value={summary.underMaintenanceWeapons} tone={summary.underMaintenanceWeapons > 0 ? 'warn' : 'ok'} />
        <CommandMetric label="مفقود أو غير مطابق" value={summary.missingOrUnaccountedWeapons} tone={summary.missingOrUnaccountedWeapons > 0 ? 'danger' : 'ok'} />
      </div>

      <div className="duty-status-band" data-status={sensitiveCustodyStatus(summary)}>
        <span>إعادات متأخرة {summary.overdueReturns}</span>
        <span>فحوص مستحقة {summary.inspectionsDue}</span>
        <span>فروقات مفتوحة {summary.openDiscrepancies}</span>
        <span>ذخيرة متاحة {summary.ammunitionAvailable}</span>
        <span>عجز ذخيرة {summary.ammunitionGap}</span>
        <span>اعتمادات معلقة {summary.pendingApprovals}</span>
      </div>

      {custody.interventions.length > 0 && (
        <ul className="priority-row-list" aria-label="استثناءات العهد الحساسة">
          {custody.interventions.map((item) => (
            <li key={`${item.code}-${item.sourceEntityId ?? item.sourceEntityType}`}>
              <button
                type="button"
                className="priority-row compact"
                data-tone={sensitiveSeverityTone(item.severity)}
                onClick={() => openPanel(sensitivePanelForTarget(item.drillDownTarget, item.sourceEntityId ?? item.code))}
              >
                <span className="priority-band" aria-hidden="true" />
                <span><strong>{sensitiveInterventionLabel(item.code)}</strong><small>{item.reasonAr}</small></span>
                <b>{item.primaryActionAr}</b>
              </button>
            </li>
          ))}
        </ul>
      )}

      {custody.dataQualityIssues.length > 0 && (
        <ul className="priority-row-list" aria-label="جودة بيانات العهد الحساسة">
          {custody.dataQualityIssues.slice(0, 6).map((item) => (
            <li key={item.code}>
              <button
                type="button"
                className="priority-row compact"
                data-tone={sensitiveSeverityTone(item.severity)}
                onClick={() => openPanel({ type: 'requirement-gap', entityId: `dq-${item.code}` })}
              >
                <span className="priority-band" aria-hidden="true" />
                <span><strong>{item.code}</strong><small>{item.impactAr}</small></span>
                <b>{item.count}</b>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

function RiskSection({ data, openPanel }: Readonly<{ data: CommandData; openPanel: (panel: PanelState) => void }>) {
  const risk = data.risk
  const riskGap = domainFor(data, 'risks')
  const facilityId = data.header?.facilityId
  const registerQuery = useQuery({
    queryKey: ['risk-register', facilityId],
    queryFn: () => api.risks.list(facilityId ?? '', { pageSize: 10 }),
    enabled: Boolean(risk && facilityId),
    placeholderData: keepPreviousData,
  })

  if (!risk) {
    return (
      <div className="command-section-stack">
        <DomainUnavailableSection domain={riskGap} panelType="risk" openPanel={openPanel} compact />
      </div>
    )
  }

  const summary = risk.summary
  return (
    <div className="command-section-stack">
      <div className="occupancy-command-strip" data-status={riskStatus(summary)}>
        <div>
          <span className="command-eyebrow">حالة سجل المخاطر</span>
          <h3>{summary.openRisks} خطر مفتوح</h3>
          <p>متوسط العمر {summary.averageOpenRiskAgeDays} يوم · آخر تحديث {summary.lastUpdatedAtUtc ? SHORT_DATE_FORMAT.format(new Date(summary.lastUpdatedAtUtc)) : 'غير متاح'}</p>
        </div>
        <strong>{summary.criticalRisks}/{summary.highRisks}</strong>
      </div>

      <div className="workforce-rail readiness-rail" aria-label="مؤشرات المخاطر">
        <CommandMetric label="حرجة" value={summary.criticalRisks} tone={summary.criticalRisks > 0 ? 'danger' : 'ok'} />
        <CommandMetric label="عالية" value={summary.highRisks} tone={summary.highRisks > 0 ? 'warn' : 'ok'} />
        <CommandMetric label="اتجاه تصاعدي" value={summary.increasingTrendRisks} tone={summary.increasingTrendRisks > 0 ? 'warn' : 'ok'} />
        <CommandMetric label="متكررة" value={summary.recurringRisks} tone={summary.recurringRisks > 0 ? 'warn' : 'ok'} />
        <CommandMetric label="بلا مالك" value={summary.risksWithoutOwner} tone={summary.risksWithoutOwner > 0 ? 'warn' : 'ok'} />
        <CommandMetric label="بلا معالجة" value={summary.risksWithoutTreatment} tone={summary.risksWithoutTreatment > 0 ? 'warn' : 'ok'} />
      </div>

      <div className="duty-status-band" data-status={riskStatus(summary)}>
        <span>مراجعات متأخرة {summary.overdueReviewRisks}</span>
        <span>معالجات متأخرة {summary.overdueTreatmentActions}</span>
        <span>قبول قارب الاستحقاق {summary.acceptedRisksNearingReview}</span>
        <span>بيانات قديمة {summary.staleDataRisks}</span>
      </div>

      {risk.interventions.length > 0 && (
        <ul className="priority-row-list" aria-label="تدخلات المخاطر">
          {risk.interventions.map((item) => (
            <li key={`${item.interventionType}-${item.riskRecordId}`}>
              <button
                type="button"
                className="priority-row compact"
                data-tone={riskSeverityTone(item.severityAr)}
                onClick={() => openPanel({ type: 'risk', entityId: item.riskRecordId })}
              >
                <span className="priority-band" aria-hidden="true" />
                <span><strong>{item.riskCode} — {item.titleAr}</strong><small>{item.reasonAr}</small></span>
                <b>{item.primaryActionAr}</b>
              </button>
            </li>
          ))}
        </ul>
      )}

      {registerQuery.data && registerQuery.data.items.length > 0 && (
        <table className="risk-register-table" aria-label="سجل المخاطر">
          <thead>
            <tr>
              <th>الرمز</th>
              <th>العنوان</th>
              <th>الحالة</th>
              <th>التصنيف</th>
              <th>الاتجاه</th>
              <th>المالك</th>
            </tr>
          </thead>
          <tbody>
            {registerQuery.data.items.map((item) => (
              <RiskRegisterRow key={item.id} item={item} openPanel={openPanel} />
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}

function RiskRegisterRow({ item, openPanel }: Readonly<{ item: RiskListItem; openPanel: (panel: PanelState) => void }>) {
  return (
    <tr>
      <td>
        <button type="button" className="link-button" onClick={() => openPanel({ type: 'risk', entityId: item.id })}>
          {item.riskCode}
        </button>
      </td>
      <td>{item.title}</td>
      <td>{item.statusAr}</td>
      <td>{item.residualRatingLabelAr ?? item.inherentRatingLabelAr ?? '-'}</td>
      <td>{item.trendAr}</td>
      <td>{item.ownerDisplayName ?? 'بلا مالك'}</td>
    </tr>
  )
}

function riskStatus(summary: RiskWorkspaceSummary): string {
  if (summary.criticalRisks > 0) return 'partial'
  if (summary.openRisks === 0) return 'complete'
  return 'complete'
}

function riskPulseTone(risk?: RiskWorkspacePayload): WorkspaceVisualTone {
  if (!risk) return 'muted'
  if (risk.summary.criticalRisks > 0) return 'danger'
  if (risk.summary.highRisks > 0 || risk.summary.increasingTrendRisks > 0) return 'warn'
  return 'ok'
}

function riskSeverityTone(severityAr: string): WorkspaceVisualTone {
  if (severityAr === 'حرج' || severityAr === 'حرجة') return 'danger'
  if (severityAr === 'عالية') return 'warn'
  return 'info'
}

function WorkforceCoverageSection({ data, openPanel }: Readonly<{ data: CommandData; openPanel: (panel: PanelState) => void }>) {
  const workforce = data.workforce
  const workforceGap = domainFor(data, 'workforce')
  if (!workforce) {
    return (
      <div className="command-section-stack">
        <DomainUnavailableSection domain={workforceGap} panelType="workforce-gap" openPanel={openPanel} compact />
      </div>
    )
  }

  const { summary, coverage, units, roles } = workforce
  const roleGaps = coverage.filter((row) => row.gap > 0 || row.safeGap > 0)
  const shiftRows = coverage.filter((row) => row.shiftCode)
  const exceptions = workforceExceptions(summary, roleGaps)

  return (
    <div className="command-section-stack">
      <div className="occupancy-command-strip" data-status={workforceStripStatus(summary.coverageStatus, summary.gap)}>
        <div>
          <span className="command-eyebrow">تغطية القوى البشرية</span>
          <h3>{summary.coverageRate == null ? 'لا يوجد baseline تغطية' : `${Math.round(summary.coverageRate * 100)}% تغطية`}</h3>
          <p>{summary.warnings.join(' ') || 'لا توجد تحذيرات تغطية حالية.'}</p>
        </div>
        <strong>{summary.operationallyAvailable}/{summary.required || '-'}</strong>
      </div>

      <div className="workforce-rail readiness-rail" aria-label="مؤشرات التغطية">
        <CommandMetric label="المطلوب" value={summary.required} tone="info" />
        <CommandMetric label="المتاح تشغيليًا" value={summary.operationallyAvailable} tone="ok" />
        <CommandMetric label="الحاضر" value={summary.present} tone="info" />
        <CommandMetric label="الفجوة" value={summary.gap} tone={summary.gap > 0 ? 'danger' : 'ok'} />
        <CommandMetric label="الحد الآمن الأدنى" value={summary.minimumSafe} tone={summary.safeGap > 0 ? 'warn' : 'ok'} />
      </div>

      <div className="duty-status-band" data-status={workforceStripStatus(summary.coverageStatus, summary.gap)}>
        <span>المجدول {summary.scheduled}</span>
        <span>إجازة {summary.onLeave}</span>
        <span>تدريب {summary.inTraining}</span>
        <span>مقيّد {summary.restricted}</span>
        <span>مواقع حرجة {summary.criticalPositionsAtRisk}</span>
      </div>

      {roleGaps.length > 0 && (
        <ul className="priority-row-list" aria-label="فجوات الأدوار">
          {roleGaps.map((row) => (
            <li key={`role-gap-${row.roleDefinitionId}-${row.facilityUnitId ?? 'facility'}-${row.shiftDefinitionId ?? 'any'}`}>
              <button
                type="button"
                className="priority-row compact"
                data-tone={coverageTone(row.coverageStatus)}
                onClick={() => openPanel({ type: 'workforce-role', entityId: row.roleDefinitionId })}
              >
                <span className="priority-band" />
                <span>
                  <strong>{row.roleNameAr}</strong>
                  <small>{row.roleCode}{row.unitNameAr ? ` · ${row.unitNameAr}` : ''} · فجوة {row.gap}</small>
                </span>
                <b>{row.coverageRate == null ? '-' : `${Math.round(row.coverageRate * 100)}%`}</b>
              </button>
            </li>
          ))}
        </ul>
      )}

      {units.length > 0 && (
        <div className="workforce-rail" aria-label="حرارة تغطية الوحدات">
          {units.map((unit) => (
            <button
              key={unit.facilityUnitId ?? unit.unitNameAr}
              type="button"
              data-status={unitRailStatus(unit)}
              onClick={() => openPanel({ type: 'workforce-unit', entityId: unit.facilityUnitId ?? `unit-${unit.unitNameAr}` })}
            >
              <span>{unit.unitNameAr}</span>
              <strong>{unit.coverageRate == null ? '-' : `${Math.round(unit.coverageRate * 100)}%`}</strong>
              <small>مطلوب {unit.required} · متاح {unit.operationallyAvailable} · فجوة {unit.gap}</small>
            </button>
          ))}
        </div>
      )}

      {shiftRows.length > 0 && (
        <ul className="shift-coverage-list" aria-label="تغطية الورديات">
          {shiftRows.map((row) => (
            <li key={`shift-${row.roleDefinitionId}-${row.shiftDefinitionId}-${row.facilityUnitId ?? 'facility'}`}>
              <button
                type="button"
                className="shift-coverage-row"
                data-status={unitRailStatus({ gap: row.gap, required: row.required, coverageStatus: row.coverageStatus })}
                onClick={() => openPanel({ type: 'workforce-shift', entityId: row.shiftDefinitionId ?? row.roleDefinitionId })}
              >
                <span>
                  <strong>{row.shiftCode}</strong>
                  <small>{row.roleNameAr}{row.unitNameAr ? ` · ${row.unitNameAr}` : ''}</small>
                </span>
                <span>مطلوب {row.required}</span>
                <span>حاضر {row.present}</span>
                <span>فجوة {row.gap}</span>
              </button>
            </li>
          ))}
        </ul>
      )}

      {roles.length > 0 && roleGaps.length === 0 && (
        <div className="workforce-rail" aria-label="تعريفات الأدوار">
          {roles.slice(0, 8).map((role) => (
            <button key={role.id} type="button" data-status="complete" onClick={() => openPanel({ type: 'workforce-role', entityId: role.id })}>
              <span>{role.nameAr}</span>
              <strong>{role.code}</strong>
              <small>{role.isShiftBased ? 'ورديات' : 'ثابت'}{role.requiresCertification ? ' · شهادة' : ''}</small>
            </button>
          ))}
        </div>
      )}

      {exceptions.length > 0 && (
        <ul className="priority-row-list" aria-label="استثناءات القوى البشرية">
          {exceptions.map((item) => (
            <li key={item.id}>
              <button
                type="button"
                className="priority-row compact"
                data-tone={item.tone}
                onClick={() => openPanel({ type: item.panelType, entityId: item.id })}
              >
                <span className="priority-band" />
                <span><strong>{item.titleAr}</strong><small>{item.reasonAr}</small></span>
                <b>{item.severityAr}</b>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

function DomainUnavailableSection({
  domain,
  panelType,
  openPanel,
  compact = false,
}: Readonly<{ domain?: DataQualityDomain; panelType: PanelType; openPanel: (panel: PanelState) => void; compact?: boolean }>) {
  return (
    <div className={`domain-unavailable ${compact ? 'compact' : ''}`} data-status={domain?.statusCode ?? 'unavailable'}>
      <div>
        <span className="command-eyebrow">{domain?.statusAr ?? 'غير متاح'}</span>
        <h3>{domain?.labelAr ?? panelLabel(panelType)}</h3>
        <p>{domain?.impactAr ?? 'لا يوجد مصدر بيانات مستقل لهذا المجال في النموذج الحالي.'}</p>
      </div>
      <button type="button" className="command-button ghost" onClick={() => openPanel({ type: panelType, entityId: `domain-${domain?.key ?? panelType}` })}>
        فتح الفجوة
      </button>
    </div>
  )
}

function UnitLoadRows({ units, openPanel }: Readonly<{ units: FacilityUnitItem[]; openPanel: (panel: PanelState) => void }>) {
  if (units.length === 0) {
    return <WorkspaceEmpty message="لا توجد وحدات داخلية مسجلة لهذا السجن." />
  }

  return (
    <ul className="unit-load-list" aria-label="وحدات السجن">
      {units.map((unit) => (
        <li key={unit.unitId}>
          <button type="button" onClick={() => openPanel({ type: 'facility-unit', entityId: unit.unitId })}>
            <span className="unit-load-title"><strong>{unit.nameAr}</strong><small>{unit.code}{unit.parentUnitNameAr ? ` · ${unit.parentUnitNameAr}` : ''}</small></span>
            <span className="unit-load-values">
              <b>{unit.openNotes}</b><small>ملاحظات</small>
              <b>{unit.openCorrectiveActions}</b><small>إجراءات</small>
              <b>{unit.overdueNotes}</b><small>متأخرة</small>
            </span>
          </button>
        </li>
      ))}
    </ul>
  )
}

function DataQualitySection({ payload, openPanel }: Readonly<{ payload?: FacilityDataQualityPayload; openPanel: (panel: PanelState) => void }>) {
  const domains = payload?.domains ?? []
  if (domains.length === 0) {
    return <WorkspaceEmpty message="لا توجد قراءة جودة بيانات." />
  }

  return (
    <ul className="data-quality-list" aria-label="جودة بيانات المجالات">
      {domains.map((domain) => (
        <li key={domain.key} data-status={domain.statusCode}>
          <button type="button" onClick={() => openPanel(panelForDomain(domain))}>
            <span><strong>{domain.labelAr}</strong><small>{domain.impactAr}</small></span>
            <span>{domain.statusAr}</span>
            <span>{domain.confidenceAr}</span>
            <span>{domain.lastUpdatedAtUtc ? formatShortDate(domain.lastUpdatedAtUtc) : 'لا يوجد تحديث'}</span>
          </button>
        </li>
      ))}
    </ul>
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
    occupancy: payloadFor<OccupancyWorkspacePayload>(shell.widgets, 'facility.occupancy'),
    resources: payloadFor<ResourceWorkspacePayload>(shell.widgets, 'facility.resources'),
    sensitiveCustody: payloadFor<SensitiveCustodyWorkspacePayload>(shell.widgets, 'facility.sensitive-custody'),
    workforce: payloadFor<WorkforceWorkspacePayload>(shell.widgets, 'facility.workforce'),
    risk: payloadFor<RiskWorkspacePayload>(shell.widgets, 'facility.risks'),
    priority: payloadFor<FacilityPriorityQueuePayload>(shell.widgets, 'facility.priority-queue'),
    activity: payloadFor<FacilityRecentActivityPayload>(shell.widgets, 'facility.recent-activity'),
    structure: payloadFor<FacilityStructurePayload>(shell.widgets, 'facility.structure'),
    dataQuality: payloadFor<FacilityDataQualityPayload>(shell.widgets, 'facility.data-quality'),
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
  return typeof value === 'string' && (PANEL_TYPES as readonly string[]).includes(value)
}

function sectionFromSearch(searchParams: URLSearchParams): SectionKey {
  const section = searchParams.get('section')
  return SECTION_NAV.some((item) => item.key === section) ? section as SectionKey : 'overview'
}

function buildUrl(path: string, params: URLSearchParams): string {
  const queryString = params.toString()
  return queryString ? `${path}?${queryString}` : path
}

function closePanel(
  searchParams: URLSearchParams,
  setSearchParams: ReturnType<typeof useSearchParams>[1],
  selectedRowRef: React.MutableRefObject<HTMLButtonElement | null>,
) {
  const selectedRow = selectedRowRef.current
  const params = new URLSearchParams(searchParams)
  params.delete('panel')
  params.delete('entityId')
  setSearchParams(params, { replace: false })
  window.setTimeout(() => selectedRow?.focus(), 0)
}

function panelForPriorityItem(item: PriorityItem): PanelState {
  if (item.type === 'note') return { type: 'note', entityId: item.drillDownTarget.routeParameters.noteId ?? item.reference }
  if (item.type === 'corrective-action') return { type: 'corrective-action', entityId: item.drillDownTarget.routeParameters.id ?? item.reference }
  if (item.type === 'form') return { type: 'form-assignment', entityId: item.reference }
  if (item.type === 'escalation') return { type: 'escalation', entityId: item.reference }
  if (item.type === 'occupancy') return { type: 'facility-unit', entityId: item.drillDownTarget.routeParameters.unitId ?? 'domain-occupancy' }
  if (item.type === 'resource') return { type: 'equipment', entityId: item.drillDownTarget.routeParameters.assetId ?? item.reference }
  if (item.type === 'sensitive-custody') return sensitivePanelForTarget(item.drillDownTarget, item.reference)
  if (item.type === 'workforce') return workforcePanelForReference(item.reference)
  if (item.type === 'risk') return { type: 'risk', entityId: item.reference.split(':')[1] ?? item.reference }
  return { type: 'activity', entityId: item.reference }
}

function workforcePanelForReference(reference: string): PanelState {
  if (reference.startsWith('gap:')) return { type: 'workforce-gap', entityId: reference }
  if (reference.startsWith('critical:')) return { type: 'workforce-critical-position', entityId: reference }
  if (reference.startsWith('roster:')) return { type: 'workforce-roster', entityId: reference }
  if (reference.startsWith('requirement:')) return { type: 'workforce-requirement', entityId: reference }
  if (reference.startsWith('qualification:')) return { type: 'workforce-qualification', entityId: reference }
  if (reference.startsWith('unit:')) return { type: 'workforce-unit', entityId: reference }
  return { type: 'workforce-role', entityId: reference }
}

function panelForActivityItem(item: ActivityItem, index: number): PanelState {
  if (item.drillDownTarget.routeKey === 'notes.workspace' && item.drillDownTarget.routeParameters.noteId) {
    return { type: 'note', entityId: item.drillDownTarget.routeParameters.noteId }
  }
  if (item.drillDownTarget.routeKey === 'corrective-actions.list' && item.drillDownTarget.routeParameters.id) {
    return { type: 'corrective-action', entityId: item.drillDownTarget.routeParameters.id }
  }
  if (item.drillDownTarget.routeKey === 'form-compliance.facility') {
    return { type: 'form-assignment', entityId: item.entityReference }
  }
  if (item.drillDownTarget.routeKey === 'escalations.occurrences') {
    return { type: 'escalation', entityId: item.entityReference }
  }
  if (item.drillDownTarget.routeKey === 'facility.occupancy') {
    return { type: 'facility-unit', entityId: item.drillDownTarget.routeParameters.unitId ?? 'domain-occupancy' }
  }
  if (item.drillDownTarget.routeKey === 'facility.resources') {
    return { type: 'equipment', entityId: item.drillDownTarget.routeParameters.assetId ?? item.entityReference }
  }
  if (item.drillDownTarget.routeKey === 'facility.sensitive-custody') {
    return sensitivePanelForTarget(item.drillDownTarget, item.entityReference)
  }
  if (item.drillDownTarget.routeKey === 'facility.workforce') {
    return { type: 'workforce-gap', entityId: item.drillDownTarget.routeParameters.memberId ?? item.entityReference }
  }
  return { type: 'activity', entityId: `${item.entityReference}-${index}` }
}

function panelForDomain(domain: DataQualityDomain): PanelState {
  const panelTypeByDomain: Record<string, PanelType> = {
    structure: 'activity',
    notes: 'activity',
    'corrective-actions': 'activity',
    escalations: 'activity',
    forms: 'activity',
    occupancy: 'facility-unit',
    resources: 'equipment',
    'sensitive-custody': 'weapon',
    workforce: 'workforce-gap',
    incidents: 'incident',
    risks: 'risk',
    projects: 'project',
    plans: 'emergency-plan',
    decisions: 'decision',
  }

  return { type: panelTypeByDomain[domain.key] ?? 'activity', entityId: `domain-${domain.key}` }
}

function findPanelSummary(
  panel: PanelState,
  queue?: FacilityPriorityQueuePayload,
  activity?: FacilityRecentActivityPayload,
  structure?: FacilityStructurePayload,
  occupancy?: OccupancyWorkspacePayload,
  dataQuality?: FacilityDataQualityPayload,
): PanelSummary | undefined {
  if (panel.type === 'facility-unit') {
    const occupancyUnit = occupancy?.unitBreakdown.units.find((item) => item.unitId === panel.entityId)
    if (occupancyUnit) return occupancyUnit
    const unit = structure?.units.find((item) => item.unitId === panel.entityId)
    if (unit) return unit
  }

  if (panel.entityId.startsWith('domain-')) {
    const domainKey = panel.entityId.replace(/^domain-/, '')
    const domain = dataQuality?.domains.find((item) => item.key === domainKey)
    if (domain) return domain
  }

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

function legacyRouteForPanel(panel: PanelState, summary: PanelSummary | undefined, shell: WorkspaceShellDto): string | null {
  if (panel.type === 'note') return `/notes/workspace?noteId=${encodeURIComponent(panel.entityId)}`
  if (panel.type === 'corrective-action') return `/corrective-actions?id=${encodeURIComponent(panel.entityId)}`
  if (panel.type === 'form-assignment') return `/form-compliance/facilities/${shell.context.facilityId ?? ''}`
  if (panel.type === 'escalation') return '/settings/escalations/occurrences'
  if (panel.type === 'facility-unit') {
    return summary && 'dataSourceAr' in summary
      ? `/facilities/${shell.context.facilityId ?? ''}/occupancy`
      : null
  }
  if (isWorkforcePanelType(panel.type)) {
    const section = workforceAdminSectionForPanel(panel.type)
    const base = `/facilities/${shell.context.facilityId ?? ''}/workforce`
    return section ? `${base}?section=${section}` : base
  }
  if (isSensitiveCustodyPanelType(panel.type)) {
    return null
  }
  if (summary && 'drillDownTarget' in summary) return routeFromTarget(summary.drillDownTarget)
  return null
}

function routeFromTarget(target: { routeKey: string; routeParameters: Record<string, string>; preservedFilters: Record<string, string> }): string | null {
  if (target.routeKey === 'notes.workspace') return `/notes/workspace?noteId=${target.routeParameters.noteId ?? ''}`
  if (target.routeKey === 'corrective-actions.list') return `/corrective-actions?id=${target.routeParameters.id ?? ''}`
  if (target.routeKey === 'form-compliance.facility') return `/form-compliance/facilities/${target.routeParameters.facilityId ?? ''}`
  if (target.routeKey === 'escalations.occurrences') return '/settings/escalations/occurrences'
  if (target.routeKey === 'facility.occupancy') return `/facilities/${target.routeParameters.facilityId ?? ''}/occupancy`
  if (target.routeKey === 'facility.resources') return `/facilities/${target.routeParameters.facilityId ?? ''}/resources`
  if (target.routeKey === 'facility.sensitive-custody') return null
  if (target.routeKey === 'facility.workforce') return `/facilities/${target.routeParameters.facilityId ?? ''}/workforce`
  return null
}

function resourceCategoryRailStatus(gap: number, total: number) {
  if (gap > 0) return 'partial'
  if (total > 0) return 'complete'
  return 'missing'
}

function resourcePanelType(resourceTypeCode?: string): PanelType {
  if (resourceTypeCode === '0' || resourceTypeCode === 'Vehicle') return 'vehicle'
  if (resourceTypeCode === '1' || resourceTypeCode === 'CommunicationDevice') return 'communication-device'
  return 'equipment'
}

type SensitivePanelParameter =
  | 'weaponId'
  | 'transactionId'
  | 'armoryLocationId'
  | 'ammunitionLotId'
  | 'inventoryId'
  | 'discrepancyId'
  | 'inspectionId'
  | 'requirementId'

const SENSITIVE_PANEL_TARGETS = [
  { parameter: 'weaponId', type: 'weapon' },
  { parameter: 'transactionId', type: 'custody-transaction' },
  { parameter: 'armoryLocationId', type: 'armory-location' },
  { parameter: 'ammunitionLotId', type: 'ammunition-lot' },
  { parameter: 'inventoryId', type: 'inventory-session' },
  { parameter: 'discrepancyId', type: 'inventory-discrepancy' },
  { parameter: 'inspectionId', type: 'weapon-inspection' },
  { parameter: 'requirementId', type: 'requirement-gap' },
] as const satisfies ReadonlyArray<{
  parameter: SensitivePanelParameter
  type: PanelType
}>

function sensitivePanelForTarget(
  target: { routeParameters: Record<string, string> },
  fallback: string,
): PanelState {
  const matchedTarget = SENSITIVE_PANEL_TARGETS.find(
    ({ parameter }) => Boolean(target.routeParameters[parameter]),
  )

  if (!matchedTarget) {
    return { type: 'weapon', entityId: fallback }
  }

  return {
    type: matchedTarget.type,
    entityId: target.routeParameters[matchedTarget.parameter],
  }
}

function sensitiveCustodyStatus(summary: SensitiveCustodyWorkspacePayload['summary']) {
  if (summary.missingOrUnaccountedWeapons > 0 || summary.ammunitionGap > 0) return 'partial'
  if (summary.totalWeapons > 0 || summary.ammunitionAvailable > 0) return 'complete'
  return 'missing'
}

function sensitiveCustodyTone(payload?: SensitiveCustodyWorkspacePayload): WorkspaceVisualTone {
  if (!payload) return domainTone()
  const status = sensitiveCustodyStatus(payload.summary)
  if (status === 'partial') return 'danger'
  if (status === 'complete') return 'ok'
  return 'muted'
}

function sensitiveSeverityTone(severity: string): WorkspaceVisualTone {
  if (severity === 'Critical') return 'danger'
  if (severity === 'Major') return 'warn'
  return 'info'
}

function sensitiveInterventionLabel(code: string) {
  const labels: Record<string, string> = {
    WeaponMissing: 'سلاح مفقود',
    WeaponUnaccountedFor: 'سلاح غير مطابق',
    CustodyReturnOverdue: 'عهدة متأخرة',
    CustodyHandoverIncomplete: 'تسليم غير مكتمل',
    WeaponInspectionExpired: 'فحص سلاح منتهي',
    WeaponMaintenanceOverdue: 'صيانة متأخرة',
    WeaponUnserviceable: 'سلاح غير صالح',
    InventoryDiscrepancyCritical: 'فرق جرد حرج',
    AmmunitionBelowMinimum: 'ذخيرة دون الحد الأدنى',
    AmmunitionExpired: 'ذخيرة منتهية',
    ArmoryInspectionExpired: 'فحص مستودع مستحق',
    SensitiveDataStale: 'بيانات قديمة',
    SourceConflict: 'تعارض مصادر',
    UnverifiedWeapon: 'سلاح غير محقق',
  }
  return labels[code] ?? code
}

function workforceStripStatus(status: WorkforceCoverageStatus, gap: number) {
  if (status === 3 || status === 2 || gap > 0) return 'partial'
  if (status === 0) return 'complete'
  if (status === 4) return 'missing'
  return 'attention'
}

function coverageTone(status: WorkforceCoverageStatus): WorkspaceVisualTone {
  if (status === 2 || status === 3) return 'danger'
  if (status === 1) return 'warn'
  if (status === 0) return 'ok'
  return 'muted'
}

function unitRailStatus(unit: Pick<WorkforceUnitCoveragePayload, 'gap' | 'required' | 'coverageStatus'> | { gap: number; required: number; coverageStatus: WorkforceCoverageStatus }) {
  if (unit.coverageStatus === 2 || unit.coverageStatus === 3 || unit.gap > 0) return 'partial'
  if (unit.required > 0) return 'complete'
  return 'missing'
}

function workforceExceptions(
  summary: WorkforceWorkspacePayload['summary'],
  roleGaps: WorkforceCoverageRowPayload[],
): Array<{ id: string; titleAr: string; reasonAr: string; severityAr: string; tone: WorkspaceVisualTone; panelType: PanelType }> {
  const items: Array<{ id: string; titleAr: string; reasonAr: string; severityAr: string; tone: WorkspaceVisualTone; panelType: PanelType }> = []
  if (summary.criticalPositionsAtRisk > 0) {
    items.push({
      id: 'critical-positions',
      titleAr: 'مواقع حرجة غير مغطاة',
      reasonAr: `عدد المواقع الحرجة المعرضة للخطر: ${summary.criticalPositionsAtRisk}`,
      severityAr: 'حرجة',
      tone: 'danger',
      panelType: 'workforce-critical-position',
    })
  }
  for (const warning of summary.warnings.slice(0, 3)) {
    items.push({
      id: `warning-${warning}`,
      titleAr: 'تحذير تغطية',
      reasonAr: warning,
      severityAr: 'عالية',
      tone: 'warn',
      panelType: 'workforce-gap',
    })
  }
  for (const row of roleGaps.slice(0, 5)) {
    items.push({
      id: `gap-${row.roleDefinitionId}-${row.shiftDefinitionId ?? 'any'}`,
      titleAr: `فجوة دور ${row.roleNameAr}`,
      reasonAr: `مطلوب ${row.required} · متاح ${row.operationallyAvailable} · فجوة ${row.gap}`,
      severityAr: row.safeGap > 0 ? 'حرجة' : 'عالية',
      tone: coverageTone(row.coverageStatus),
      panelType: 'workforce-role',
    })
  }
  return items
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

function isDraftRoster(roster: DutyRosterPayload) {
  return roster.status === 'Draft'
}

function workspaceActionError(error: unknown) {
  if (!(error instanceof ApiError)) return error instanceof Error ? error.message : 'تعذر تنفيذ الإجراء.'
  if (error.status === 403) return 'ليست لديك صلاحية تنفيذ هذا الإجراء.'
  if (error.status === 404) return 'السجل غير موجود ضمن نطاق السجن.'
  if (error.status === 409) return 'تعارض في البيانات. حدّث مساحة العمل ثم أعد المحاولة.'
  if (error.status === 422) return error.message || 'البيانات غير صالحة.'
  return error.message
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
    VERIFY_CLOSURE: 'اعتماد الإغلاق',
    REOPEN: 'إعادة فتح',
    CANCEL: 'إلغاء',
    TRIAGE_VALID: 'اعتماد صحيحة',
    TRIAGE_PROPOSE_INVALID: 'اقتراح غير صحيحة',
    TRIAGE_PROPOSE_DUPLICATE: 'اقتراح مكررة',
    RECORD_TREATMENT: 'تسجيل نتيجة المعالجة',
    PROPOSE_NO_ACTION: 'اقتراح لا تتطلب إجراء',
    MANAGE_PARTS: 'إدارة القطع والمواد',
    REQUEST_SLA_PAUSE: 'طلب تجميد SLA',
    APPROVE_SLA_PAUSE: 'اعتماد تجميد SLA',
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

function occupancyTone(status?: string): WorkspaceVisualTone | null {
  if (status === 'over-capacity') return 'danger'
  if (status === 'high') return 'warn'
  if (status === 'attention') return 'warn'
  if (status === 'normal') return 'ok'
  return null
}

function domainFor(data: CommandData, key: string): DataQualityDomain | undefined {
  return data.dataQuality?.domains.find((domain) => domain.key === key)
}

function domainTone(domain?: DataQualityDomain): WorkspaceVisualTone {
  if (!domain) return 'muted'
  if (domain.statusCode === 'complete') return 'ok'
  if (domain.statusCode === 'partial') return 'warn'
  if (domain.statusCode === 'stale') return 'warn'
  return 'muted'
}

function isWorkforcePanelType(type: PanelType): boolean {
  return type.startsWith('workforce-')
}

function isSensitiveCustodyPanelType(type: PanelType): boolean {
  return type === 'custody-transaction'
    || type === 'armory-location'
    || type === 'ammunition-lot'
    || type === 'ammunition-transaction'
    || type === 'inventory-session'
    || type === 'inventory-discrepancy'
    || type === 'weapon-inspection'
    || type === 'maintenance-work-order'
    || type === 'requirement-gap'
}

function workforceAdminSectionForPanel(type: PanelType): string | null {
  if (type === 'workforce-member') return 'members'
  if (type === 'workforce-shift' || type === 'workforce-roster') return 'shifts'
  if (type === 'workforce-role') return 'roles'
  if (type === 'workforce-unit') return 'units'
  if (type === 'workforce-requirement') return 'requirements'
  if (type === 'workforce-qualification') return 'qualifications'
  if (type === 'workforce-critical-position' || type === 'workforce-gap') return 'coverage'
  return null
}

function panelLabel(type: PanelType) {
  const labels: Partial<Record<PanelType, string>> = {
    note: 'ملاحظة تشغيلية',
    'note-create': 'فتح ملاحظة',
    'corrective-action': 'إجراء تصحيحي',
    escalation: 'تصعيد',
    'form-assignment': 'التزام نموذج',
    'facility-unit': 'وحدة داخلية',
    incident: 'وقوعات وحوادث',
    risk: 'مخاطر ومعالجات',
    vehicle: 'مركبة',
    weapon: 'سلاح أو عهدة',
    'communication-device': 'جهاز اتصال',
    equipment: 'مورد أو معدة',
    'workforce-member': 'عضو قوى بشرية',
    'workforce-shift': 'وردية تشغيل',
    'workforce-role': 'دور تشغيلي',
    'workforce-gap': 'فجوة تغطية',
    'workforce-unit': 'وحدة قوى بشرية',
    'workforce-roster': 'جدول واجب',
    'workforce-requirement': 'متطلب تسكين',
    'workforce-qualification': 'مؤهل تشغيلي',
    'workforce-critical-position': 'موقع حرج',
    'custody-transaction': 'حركة عهدة',
    'armory-location': 'موقع عهدة حساس',
    'ammunition-lot': 'دفعة ذخيرة',
    'ammunition-transaction': 'حركة ذخيرة',
    'inventory-session': 'جلسة جرد',
    'inventory-discrepancy': 'فرق جرد',
    'weapon-inspection': 'فحص سلاح',
    'maintenance-work-order': 'أمر صيانة',
    'requirement-gap': 'فجوة احتياج',
    project: 'مشروع أو مبادرة',
    'emergency-plan': 'خطة تشغيلية أو طوارئ',
    decision: 'قرار أو توجيه',
  }
  return labels[type] ?? 'حدث تشغيلي'
}

function summaryReference(summary?: PanelSummary) {
  if (!summary) return '-'
  if ('unitNameAr' in summary) return summary.unitCode
  if ('unitId' in summary) return summary.code
  if ('key' in summary) return summary.key
  return 'reference' in summary ? summary.reference : summary.entityReference
}

function summaryTitle(summary?: PanelSummary) {
  if (!summary) return '-'
  if ('unitNameAr' in summary) return summary.unitNameAr
  if ('unitId' in summary) return summary.nameAr
  if ('labelAr' in summary) return summary.labelAr
  return summary.titleAr
}

function summaryReason(summary?: PanelSummary) {
  if (!summary) return '-'
  if ('unitNameAr' in summary) return `${summary.statusAr} · ${summary.currentCount ?? '-'} من ${summary.approvedCapacity ?? '-'}`
  if ('unitId' in summary) return `${summary.openNotes} ملاحظات مفتوحة · ${summary.openCorrectiveActions} إجراءات مفتوحة`
  if ('impactAr' in summary) return summary.impactAr
  if ('reasonAr' in summary) return summary.reasonAr
  return summary.descriptionAr ?? '-'
}

function summaryDue(summary?: PanelSummary) {
  return summary && 'dueAtUtc' in summary && summary.dueAtUtc ? formatDate(summary.dueAtUtc) : '-'
}

function formatDate(value: string) {
  return DATE_FORMAT.format(new Date(value))
}

function formatShortDate(value?: string) {
  return value ? SHORT_DATE_FORMAT.format(new Date(value)) : '-'
}
