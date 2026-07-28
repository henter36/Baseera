import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import {
  api,
  ApiError,
  type EligibleUser,
  type NoteDecisionApproval,
  type NoteListFilters,
  type NoteListItem,
  type NotePartsRequirement,
  type NoteWorkspaceAllowedAction,
  type NoteWorkspaceDetail,
} from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import {
  NoteDecisionApprovalStatus,
  NotePartsRequirementStatus,
  NoteSeverityLabelsAr,
  NoteStatusLabelsAr,
  NoteTreatmentExecutionType,
  NoteTreatmentExecutionTypeLabelsAr,
  NoteTreatmentResultType,
  enumOptions,
  severityTone,
  statusTone,
} from '../../notes/noteEnums'
import { listQueryErrorMessage } from '../../shared/listPageUtils'
import { WorkspaceEmptyState, WorkspaceErrorState, WorkspaceSkeletonRows } from '../../shared/workspaces/WorkspaceStateView'
import { ObservationMasterDetailLayout, ObservationDetailPane, ObservationListPane } from './workspace/ObservationMasterDetailLayout'
import { ObservationWorkspaceHeader } from './workspace/ObservationWorkspaceHeader'

const PAGE_SIZE = 20
const DATE_FORMAT = new Intl.DateTimeFormat('ar-SA', {
  timeZone: 'Asia/Riyadh',
  year: 'numeric',
  month: 'short',
  day: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
})

const ACTION_LABELS: Record<NoteWorkspaceAllowedAction, string> = {
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
  TRIAGE_PROPOSE_INVALID: 'فرز: غير صحيحة',
  TRIAGE_PROPOSE_DUPLICATE: 'فرز: مكررة',
  RECORD_TREATMENT: 'تسجيل نتيجة المعالجة',
  PROPOSE_NO_ACTION: 'لا تتطلب إجراء',
  MANAGE_PARTS: 'إدارة القطع والمواد',
  REQUEST_SLA_PAUSE: 'طلب تجميد SLA',
  APPROVE_SLA_PAUSE: 'اعتماد تجميد SLA',
}

// Actions with a simple "reason only" inline form, rendered directly in the action bar.
const SIMPLE_INLINE_ACTIONS = new Set<NoteWorkspaceAllowedAction>([
  'SUBMIT',
  'START_WORK',
  'REQUEST_VERIFICATION',
  'REJECT_VERIFICATION',
  'REOPEN',
  'CANCEL',
])

// Section-navigating actions: instead of a floating action-bar form, these jump to the section
// that holds the real (inline, non-modal) form — spec: "لا تستخدم Modal كبيرة".
const SECTION_JUMP_ACTIONS: Partial<Record<NoteWorkspaceAllowedAction, SectionKey>> = {
  TRIAGE_PROPOSE_INVALID: 'triage',
  TRIAGE_PROPOSE_DUPLICATE: 'triage',
  RECORD_TREATMENT: 'treatment',
  PROPOSE_NO_ACTION: 'treatment',
  MANAGE_PARTS: 'parts',
  REQUEST_SLA_PAUSE: 'parts',
  APPROVE_SLA_PAUSE: 'parts',
}

const ALL_SECTIONS = [
  ['summary', 'الملخص'],
  ['triage', 'قرار الفرز'],
  ['assignment', 'التكليف'],
  ['treatment', 'نتيجة المعالجة'],
  ['parts', 'القطع والمواد'],
  ['evidence', 'الأدلة'],
  ['approvals', 'الاعتمادات'],
  ['escalations', 'التصعيدات'],
  ['history', 'السجل الزمني'],
] as const

type SectionKey = (typeof ALL_SECTIONS)[number][0]

const SECTION_KEYS = new Set<SectionKey>(ALL_SECTIONS.map(([key]) => key))

function resolveSectionKey(value: string | null): SectionKey {
  if (value && SECTION_KEYS.has(value as SectionKey)) {
    return value as SectionKey
  }

  return 'summary'
}

/**
 * Section visibility is state-driven, not a static list — spec:
 * "لا يظهر قسم نتيجة المعالجة قبل اعتماد صحيحة"، "لا يظهر قسم القطع إلا عند اختيار نوع تنفيذ يتطلب قطعًا".
 */
function visibleSections(data: NoteWorkspaceDetail): typeof ALL_SECTIONS[number][] {
  const isValid = data.note.triageOutcome === 0
  const requiresParts = data.note.treatmentExecutionType === NoteTreatmentExecutionType.RequiresParts
  return ALL_SECTIONS.filter(([key]) => {
    if (key === 'treatment') return isValid
    if (key === 'parts') return requiresParts
    return true
  })
}

export function ObservationWorkspacePage() {
  const canView = usePermission('Notes.View')
  const canCreate = usePermission('Notes.Create')
  const [searchParams, setSearchParams] = useSearchParams()
  const [searchInput, setSearchInput] = useState(searchParams.get('search') ?? '')
  const [debouncedSearch, setDebouncedSearch] = useState(searchInput)
  const [status, setStatus] = useState(searchParams.get('status') ?? '')
  const [severity, setSeverity] = useState(searchParams.get('severity') ?? '')
  const [regionId, setRegionId] = useState(searchParams.get('regionId') ?? '')
  const [facilityId, setFacilityId] = useState(searchParams.get('facilityId') ?? '')
  const [facilityUnitId, setFacilityUnitId] = useState(searchParams.get('facilityUnitId') ?? '')
  const [noteTypeId, setNoteTypeId] = useState(searchParams.get('noteType') ?? '')
  const [due, setDue] = useState(searchParams.get('due') ?? '')
  const [overdueOnly, setOverdueOnly] = useState(searchParams.get('due') === 'overdue')
  const [requiresMyAction, setRequiresMyAction] = useState(searchParams.get('requiresMyAction') === 'true')
  const [requiresRouting, setRequiresRouting] = useState(searchParams.get('requiresRouting') === 'true')
  const [page, setPage] = useState(Number(searchParams.get('page') ?? '1') || 1)
  const [sortBy] = useState(searchParams.get('sort') ?? searchParams.get('sortBy') ?? 'createdAtUtc')
  const [sortDesc] = useState(searchParams.get('sortDesc') !== 'false')
  const [selectedId, setSelectedId] = useState(searchParams.get('noteId') ?? '')
  const [listCollapsed, setListCollapsed] = useState(searchParams.get('view') === 'detail')
  const [activeSection, setActiveSection] = useState<SectionKey>(() =>
    resolveSectionKey(searchParams.get('section')),
  )
  const source = searchParams.get('source') ?? ''
  const listScrollRef = useRef<HTMLDivElement | null>(null)
  const selectedCardRef = useRef<HTMLButtonElement | null>(null)
  const searchDebounceMountedRef = useRef(false)
  const pushNextUrlUpdateRef = useRef(false)

  useEffect(() => {
    if (!searchDebounceMountedRef.current) {
      searchDebounceMountedRef.current = true
      return
    }

    const handle = window.setTimeout(() => {
      setPage(1)
      setDebouncedSearch(searchInput)
    }, 300)
    return () => window.clearTimeout(handle)
  }, [searchInput])

  const filters = useMemo<NoteListFilters>(
    () => ({
      page,
      pageSize: PAGE_SIZE,
      search: debouncedSearch || undefined,
      status: status === '' ? undefined : Number(status),
      severity: severity === '' ? undefined : Number(severity),
      regionId: regionId || undefined,
      facilityId: facilityId || undefined,
      facilityUnitId: facilityUnitId || undefined,
      noteTypeId: noteTypeId || undefined,
      overdueOnly: overdueOnly || undefined,
      dueSoonDays: due === 'soon' ? 7 : undefined,
      requiresMyAction: requiresMyAction || undefined,
      requiresRouting: requiresRouting || undefined,
      sortBy,
      sortDesc,
    }),
    [page, debouncedSearch, status, severity, regionId, facilityId, facilityUnitId, noteTypeId, overdueOnly, due, requiresMyAction, requiresRouting, sortBy, sortDesc],
  )

  useEffect(() => {
    const next = new URLSearchParams()
    appendFilterParam(next, 'search', filters.search)
    appendFilterParam(next, 'status', filters.status)
    appendFilterParam(next, 'severity', filters.severity)
    appendFilterParam(next, 'regionId', filters.regionId)
    appendFilterParam(next, 'facilityId', filters.facilityId)
    appendFilterParam(next, 'facilityUnitId', filters.facilityUnitId)
    appendFilterParam(next, 'noteType', filters.noteTypeId)
    appendFilterParam(next, 'due', due || undefined)
    appendFilterParam(next, 'requiresMyAction', filters.requiresMyAction)
    appendFilterParam(next, 'requiresRouting', filters.requiresRouting)
    appendFilterParam(next, 'page', page > 1 ? page : undefined)
    appendFilterParam(next, 'sort', sortBy !== 'createdAtUtc' ? sortBy : undefined)
    if (!sortDesc) next.set('sortDesc', 'false')
    if (selectedId) next.set('noteId', selectedId)
    if (selectedId) next.set('section', activeSection)
    if (source) next.set('source', source)
    const replace = !pushNextUrlUpdateRef.current
    pushNextUrlUpdateRef.current = false
    setSearchParams(next, { replace })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filters, due, selectedId, activeSection, source, setSearchParams, page, sortBy, sortDesc])

  useEffect(() => {
    const section = resolveSectionKey(searchParams.get('section'))

    setActiveSection((current) =>
      current === section ? current : section,
    )
  }, [searchParams])

  useEffect(() => {
    const noteId = searchParams.get('noteId') ?? ''
    setSelectedId((current) => current === noteId ? current : noteId)
    setListCollapsed(searchParams.get('view') === 'detail')
  }, [searchParams])

  const regionsQuery = useQuery({ queryKey: ['workspace-regions'], queryFn: () => api.regions(), enabled: canView })
  const facilitiesQuery = useQuery({
    queryKey: ['workspace-facilities', regionId],
    queryFn: () => api.facilities(regionId || undefined),
    enabled: canView,
  })
  const facilityUnitsQuery = useQuery({
    queryKey: ['workspace-facility-units', facilityId],
    queryFn: () => api.facilityUnits(facilityId),
    enabled: canView && !!facilityId,
  })
  const noteTypesQuery = useQuery({ queryKey: ['workspace-note-types'], queryFn: () => api.noteTypes(false), enabled: canView })
  const listQuery = useQuery({
    queryKey: ['notes-workspace', filters],
    queryFn: () => api.notes.workspace(filters),
    enabled: canView,
    placeholderData: keepPreviousData,
  })
  const detailQuery = useQuery({
    queryKey: ['notes-workspace-detail', selectedId],
    queryFn: () => api.notes.workspaceDetail(selectedId),
    enabled: canView && !!selectedId,
    staleTime: 10_000,
  })

  const notes = listQuery.data?.notes.items ?? []
  const currentIndex = notes.findIndex((n) => n.id === selectedId)
  const previousNote = currentIndex > 0 ? notes[currentIndex - 1] : undefined
  const nextNote = currentIndex >= 0 && currentIndex < notes.length - 1 ? notes[currentIndex + 1] : undefined

  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      const target = event.target as HTMLElement | null
      const isEditable = target && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.tagName === 'SELECT' || target.isContentEditable)
      if (isEditable || !selectedId) return
      if (event.altKey && event.key === 'ArrowRight' && previousNote) {
        event.preventDefault()
        selectNote(previousNote.id)
      } else if (event.altKey && event.key === 'ArrowLeft' && nextNote) {
        event.preventDefault()
        selectNote(nextNote.id)
      }
    }
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedId, previousNote, nextNote])

  if (!canView) {
    return <WorkspaceErrorState message="ليست لديك صلاحية عرض مساحة عمل الملاحظات." />
  }

  const totalCount = listQuery.data?.notes.totalCount ?? 0
  const totalPages = listQuery.data ? Math.max(1, Math.ceil(totalCount / PAGE_SIZE)) : 1
  const errorMessage = listQueryErrorMessage(listQuery.error, 'ليست لديك صلاحية عرض الملاحظات.', 'تعذر تحميل مساحة عمل الملاحظات.')

  function selectNote(id: string) {
    pushNextUrlUpdateRef.current = true
    setSelectedId(id)
    setActiveSection('summary')
  }

  function closeSelection() {
    const selectedCard = selectedCardRef.current
    pushNextUrlUpdateRef.current = true
    setSelectedId('')
    window.setTimeout(() => selectedCard?.focus(), 0)
  }

  return (
    <div className="observation-workspace">
      <ObservationWorkspaceHeader
        source={source}
        listCollapsed={listCollapsed}
        canCreate={canCreate}
        onToggleList={() => setListCollapsed((v) => !v)}
      />

      <section className="workspace-filters" role="search" aria-label="بحث وفلاتر الملاحظات">
        <input aria-label="بحث الملاحظات" value={searchInput} onChange={(e) => setSearchInput(e.target.value)} placeholder="بحث بالرقم أو العنوان" />
        <select aria-label="الحالة" value={status} onChange={(e) => { setPage(1); setStatus(e.target.value) }}>
          <option value="">كل الحالات</option>
          {enumOptions(NoteStatusLabelsAr).map((option) => <option key={option.value} value={option.value}>{option.labelAr}</option>)}
        </select>
        <select aria-label="الخطورة" value={severity} onChange={(e) => { setPage(1); setSeverity(e.target.value) }}>
          <option value="">كل درجات الخطورة</option>
          {enumOptions(NoteSeverityLabelsAr).map((option) => <option key={option.value} value={option.value}>{option.labelAr}</option>)}
        </select>
        <select aria-label="نوع الملاحظة" value={noteTypeId} onChange={(e) => { setPage(1); setNoteTypeId(e.target.value) }}>
          <option value="">كل الأنواع</option>
          {noteTypesQuery.data?.map((type) => <option key={type.id} value={type.id}>{type.nameAr}</option>)}
        </select>
        <select aria-label="المنطقة" value={regionId} onChange={(e) => { setPage(1); setRegionId(e.target.value); setFacilityId(''); setFacilityUnitId('') }}>
          <option value="">كل المناطق</option>
          {regionsQuery.data?.items.map((region) => <option key={region.id} value={region.id}>{region.nameAr}</option>)}
        </select>
        <select aria-label="السجن" value={facilityId} onChange={(e) => { setPage(1); setFacilityId(e.target.value); setFacilityUnitId('') }}>
          <option value="">كل السجون</option>
          {facilitiesQuery.data?.items.map((facility) => <option key={facility.id} value={facility.id}>{facility.nameAr}</option>)}
        </select>
        <select aria-label="الوحدة" value={facilityUnitId} onChange={(e) => { setPage(1); setFacilityUnitId(e.target.value) }} disabled={!facilityId}>
          <option value="">كل الوحدات</option>
          {facilityUnitsQuery.data?.items.map((unit) => <option key={unit.id} value={unit.id}>{unit.nameAr}</option>)}
        </select>
        <select aria-label="الاستحقاق" value={due} onChange={(e) => { setPage(1); setDue(e.target.value); setOverdueOnly(e.target.value === 'overdue') }}>
          <option value="">كل المواعيد</option>
          <option value="overdue">المتأخرة</option>
          <option value="soon">مستحقة قريبًا</option>
        </select>
        <label className="compact-check"><input type="checkbox" checked={requiresMyAction} onChange={(e) => { setPage(1); setRequiresMyAction(e.target.checked) }} /> بانتظار إجراء مني</label>
        <label className="compact-check"><input type="checkbox" checked={requiresRouting} onChange={(e) => { setPage(1); setRequiresRouting(e.target.checked) }} /> تحتاج توجيه</label>
      </section>

      {listQuery.isError && <WorkspaceErrorState message={errorMessage ?? 'تعذر تحميل مساحة عمل الملاحظات.'} onRetry={() => listQuery.refetch()} />}

      <ObservationMasterDetailLayout
        listCollapsed={listCollapsed}
        hasSelection={Boolean(selectedId)}
        list={(
          <ObservationListPane>
          <div className="workspace-list-header">
            <strong>الملاحظات</strong>
            <span className="muted">{totalCount} نتيجة</span>
          </div>
          <div className="workspace-list" ref={listScrollRef}>
            {listQuery.isLoading && <WorkspaceSkeletonRows count={5} />}
            {!listQuery.isLoading && notes.length === 0 && (
              <WorkspaceEmptyState
                title="لا توجد ملاحظات مطابقة"
                hint={totalCount === 0 && !filters.search && !filters.status ? 'لا توجد ملاحظات ضمن نطاقك حاليًا.' : 'جرّب تعديل الفلاتر أو مسح البحث.'}
              />
            )}
            {notes.map((note) => (
              <ObservationCard
                key={note.id}
                note={note}
                selected={selectedId === note.id}
                refCallback={(element) => {
                  if (selectedId === note.id) selectedCardRef.current = element
                }}
                onSelect={() => selectNote(note.id)}
              />
            ))}
          </div>
          <div className="pagination compact-pagination">
            <button type="button" className="secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>السابق</button>
            <span className="muted">صفحة {page} من {totalPages}</span>
            <button type="button" className="secondary" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>التالي</button>
          </div>
          </ObservationListPane>
        )}
        detail={(
          <ObservationDetailPane>
          {!selectedId && <NoSelection />}
          {selectedId && detailQuery.isLoading && <div className="detail-skeleton" aria-hidden="true" />}
          {selectedId && detailQuery.isError && (
            <WorkspaceErrorState
              message={detailQuery.error instanceof ApiError ? detailQuery.error.message : 'تعذر تحميل تفاصيل الملاحظة.'}
              onRetry={() => detailQuery.refetch()}
            />
          )}
          {selectedId && detailQuery.data && (
            <WorkspaceDetail
              key={detailQuery.data.note.id}
              data={detailQuery.data}
              activeSection={activeSection}
              onSectionChange={setActiveSection}
              onBack={closeSelection}
              previousNote={previousNote}
              nextNote={nextNote}
              position={currentIndex >= 0 ? { index: currentIndex, total: notes.length } : undefined}
              onNavigate={selectNote}
            />
          )}
          </ObservationDetailPane>
        )}
      />
    </div>
  )
}

function ObservationCard({
  note,
  selected,
  refCallback,
  onSelect,
}: Readonly<{
  note: NoteListItem
  selected: boolean
  refCallback: (element: HTMLButtonElement | null) => void
  onSelect: () => void
}>) {
  const locationLabel = noteLocationLabel(note)
  return (
    <button
      ref={refCallback}
      type="button"
      className={`observation-card ${selected ? 'selected' : ''} ${note.isOverdue ? 'overdue' : ''}`}
      onClick={onSelect}
      aria-pressed={selected}
      aria-current={selected ? 'true' : undefined}
    >
      <div className="observation-card-row">
        <span className="mono ref">{note.referenceNumber}</span>
        <span className="badge" data-tone={severityTone(note.severity)}>{note.severityAr}</span>
        <span className="badge" data-tone={statusTone(note.status)}>{note.statusAr}</span>
        {note.isOverdue && <span className="badge" data-tone="danger">متأخرة</span>}
      </div>
      <div className="observation-card-title">{note.title}</div>
      <div className="observation-card-meta">
        <span>{locationLabel}</span>
        <span>{note.currentAssigneeDisplay || 'بلا مالك'}</span>
        <span>{note.dueAtUtc ? `استحقاق ${formatDate(note.dueAtUtc)}` : 'دون استحقاق'}</span>
        <span>تحديث {formatDate(note.createdAtUtc)}</span>
      </div>
    </button>
  )
}

function WorkspaceDetail({
  data,
  activeSection,
  onSectionChange,
  onBack,
  previousNote,
  nextNote,
  position,
  onNavigate,
}: Readonly<{
  data: NoteWorkspaceDetail
  activeSection: SectionKey
  onSectionChange: (section: SectionKey) => void
  onBack: () => void
  previousNote?: NoteListItem
  nextNote?: NoteListItem
  position?: { index: number; total: number }
  onNavigate: (id: string) => void
}>) {
  const titleRef = useRef<HTMLHeadingElement | null>(null)
  const sections = visibleSections(data)
  const effectiveSection = sections.some(([key]) => key === activeSection) ? activeSection : 'summary'

  useEffect(() => {
    titleRef.current?.focus()
  }, [data.note.id])

  return (
    <article className="workspace-detail" data-testid="observation-detail-document-flow">
      <button type="button" className="secondary mobile-back" onClick={onBack}>رجوع إلى القائمة</button>
      <header className="workspace-detail-header">
        <div>
          <div className="observation-card-row">
            <span className="mono ref">{data.note.referenceNumber}</span>
            <span className="badge" data-tone={statusTone(data.note.status)}>{data.note.statusAr}</span>
            <span className="badge" data-tone={severityTone(data.note.severity)}>{data.note.severityAr}</span>
            {data.note.closureReasonAr && <span className="badge" data-tone="ok">{data.note.closureReasonAr}</span>}
            {data.actionCenter.blocker && <span className="badge" data-tone="warn">{data.actionCenter.blocker}</span>}
          </div>
          <h2 ref={titleRef} tabIndex={-1}>{data.note.title}</h2>
          <div className="workspace-header-meta">
            <span>{data.note.noteTypeNameAr}</span>
            <span>{data.note.reportedByDisplayName || 'مبلّغ غير محدد'}</span>
            <span>{data.note.dueAtUtc ? `SLA: ${formatDate(data.note.dueAtUtc)}` : 'دون SLA'}</span>
            <span>آخر تحديث {formatDate(data.summary.lastUpdatedAtUtc)}</span>
          </div>
        </div>
        <div className="progress-box" aria-label={`نسبة التقدم ${data.summary.progressPercent}%`}>
          <strong>{data.summary.progressPercent}%</strong>
          <span>التقدم</span>
        </div>
      </header>

      <SlaIndicators data={data} />

      <nav className="workspace-prev-next" aria-label="التنقل بين الملاحظات">
        <button type="button" className="secondary" disabled={!previousNote} onClick={() => previousNote && onNavigate(previousNote.id)} title="alt+→">
          ‹ السابقة
        </button>
        {position && <span className="muted">{position.index + 1} من {position.total} (ضمن النتائج المحملة)</span>}
        <button type="button" className="secondary" disabled={!nextNote} onClick={() => nextNote && onNavigate(nextNote.id)} title="alt+←">
          التالية ›
        </button>
      </nav>

      <ActionBar data={data} onNavigateToSection={onSectionChange} />
      <nav className="workspace-tabs" aria-label="أقسام الملاحظة">
        {sections.map(([key, label]) => (
          <button key={key} type="button" className={effectiveSection === key ? 'active' : undefined} onClick={() => onSectionChange(key)}>
            {label}
          </button>
        ))}
      </nav>
      <section className="workspace-tab-panel">
        {effectiveSection === 'summary' && <SummaryTab data={data} />}
        {effectiveSection === 'triage' && <TriageTab data={data} />}
        {effectiveSection === 'assignment' && <AssignmentTab data={data} />}
        {effectiveSection === 'treatment' && <TreatmentTab data={data} />}
        {effectiveSection === 'parts' && <PartsTab data={data} />}
        {effectiveSection === 'evidence' && <EvidenceTab data={data} />}
        {effectiveSection === 'approvals' && <ApprovalsTab data={data} />}
        {effectiveSection === 'escalations' && <EscalationsTab />}
        {effectiveSection === 'history' && <HistoryTab data={data} />}
      </section>
    </article>
  )
}

function SlaIndicators({ data }: Readonly<{ data: NoteWorkspaceDetail }>) {
  const sla = data.sla
  return (
    <div className="workspace-sla-indicators" aria-label="مؤشرات SLA">
      <span className="sla-chip">العمر الكلي: {formatDuration(sla.overallAgeSeconds)}</span>
      <span className="sla-chip" data-tone={sla.isProcessingSlaPaused ? 'warn' : undefined}>
        {sla.isProcessingSlaPaused ? `SLA المعالجة متوقف مؤقتًا (${sla.activePauseReason ?? ''})` : `مدة المعالجة: ${formatDuration(sla.processingSlaSeconds)}`}
      </span>
      {sla.externalWaitDurationSeconds > 0 && (
        <span className="sla-chip">مدة انتظار القطع: {formatDuration(sla.externalWaitDurationSeconds)}</span>
      )}
    </div>
  )
}

function primaryAndSecondaryActions(data: NoteWorkspaceDetail) {
  const center = data.actionCenter
  if (center.primaryAction || center.secondaryActions.length > 0) {
    return { primary: center.primaryAction ?? undefined, secondary: center.secondaryActions }
  }

  const [primary, ...secondary] = data.allowedActions
  return { primary, secondary }
}

function ActionBar({ data, onNavigateToSection }: Readonly<{ data: NoteWorkspaceDetail; onNavigateToSection: (section: SectionKey) => void }>) {
  const queryClient = useQueryClient()
  const [reason, setReason] = useState('')
  const [closureSummary, setClosureSummary] = useState('')
  const [assigneeUserId, setAssigneeUserId] = useState('')
  const [activeAction, setActiveAction] = useState<NoteWorkspaceAllowedAction | ''>('')
  const { primary, secondary } = primaryAndSecondaryActions(data)

  const eligibleAssigneesQuery = useQuery({
    queryKey: ['note-eligible-assignees', data.note.id],
    queryFn: () => api.notes.eligibleAssignees(data.note.id),
    enabled: activeAction === 'ASSIGN' || activeAction === 'REASSIGN',
  })

  const runAction = useMutation({
    mutationFn: async (action: NoteWorkspaceAllowedAction) => {
      if (action === 'SUBMIT') return api.notes.submit(data.note.id, { reason, rowVersion: data.note.rowVersion })
      if (action === 'START_WORK') return api.notes.startWork(data.note.id, { reason, rowVersion: data.note.rowVersion })
      if (action === 'REQUEST_VERIFICATION') return api.notes.submitForVerification(data.note.id, { reason, rowVersion: data.note.rowVersion })
      if (action === 'REJECT_VERIFICATION') return api.notes.returnForRework(data.note.id, { reason, rowVersion: data.note.rowVersion })
      if (action === 'REOPEN') return api.notes.reopen(data.note.id, { reason, rowVersion: data.note.rowVersion })
      if (action === 'CANCEL') return api.notes.cancel(data.note.id, { reason, rowVersion: data.note.rowVersion })
      if (action === 'VERIFY_CLOSURE') return api.notes.verifyClosure(data.note.id, { reason, closureSummary, rowVersion: data.note.rowVersion })
      if (action === 'TRIAGE_VALID') return api.notes.triageValid(data.note.id, { rowVersion: data.note.rowVersion })
      if (action === 'ASSIGN' || action === 'REASSIGN') {
        if (!assigneeUserId) throw new Error('اختر المكلَّف أولًا.')
        return api.notes.assign(data.note.id, { assignedToUserId: assigneeUserId, assignedToDepartmentId: null, dueAtUtc: null, reason, rowVersion: data.note.rowVersion })
      }
      throw new Error('هذا الإجراء غير مدعوم كعملية فورية.')
    },
    onSuccess: async () => {
      setReason('')
      setClosureSummary('')
      setAssigneeUserId('')
      setActiveAction('')
      await queryClient.invalidateQueries({ queryKey: ['notes-workspace'] })
      await queryClient.invalidateQueries({ queryKey: ['notes-workspace-detail', data.note.id] })
    },
  })

  function openAction(action: NoteWorkspaceAllowedAction) {
    const jumpSection = SECTION_JUMP_ACTIONS[action]
    if (jumpSection) {
      onNavigateToSection(jumpSection)
      return
    }

    if (action === 'TRIAGE_VALID') {
      runAction.mutate(action)
      return
    }

    setReason('')
    setClosureSummary('')
    setAssigneeUserId('')
    setActiveAction(action)
  }

  function renderButton(action: NoteWorkspaceAllowedAction, variant: 'primary' | 'secondary') {
    const className = actionButtonClassName(activeAction, action, variant)

    if (action === 'ADD_ACTION') {
      return (
        <Link key={action} to={`/notes/${data.note.id}/corrective-actions/new`}>
          <button type="button" className={className}>{ACTION_LABELS[action]}</button>
        </Link>
      )
    }
    return (
      <button
        key={action}
        type="button"
        className={className}
        onClick={() => openAction(action)}
        disabled={action === 'TRIAGE_VALID' && runAction.isPending}
      >
        {ACTION_LABELS[action]}
      </button>
    )
  }

  const canSubmitAssign = assigneeUserId && reason.trim().length >= 3
  const canSubmitClose = reason.trim().length >= 3 && closureSummary.trim().length >= 3
  const canSubmitSimple = reason.trim().length >= 3

  return (
    <div className="workspace-actionbar">
      <div className="workspace-actionbar-primary">
        {primary && renderButton(primary, 'primary')}
      </div>
      {secondary.length > 0 && (
        <div className="workspace-actionbar-secondary">
          {secondary.slice(0, 3).map((action) => renderButton(action, 'secondary'))}
        </div>
      )}
      {data.actionCenter.nextAction && (
        <p className="muted workspace-next-action">التالي: {data.actionCenter.nextAction}</p>
      )}

      {activeAction && (activeAction === 'ASSIGN' || activeAction === 'REASSIGN') && (
        <form className="inline-action-form" onSubmit={(event) => { event.preventDefault(); runAction.mutate(activeAction) }}>
          <select aria-label="المكلَّف" value={assigneeUserId} onChange={(event) => setAssigneeUserId(event.target.value)}>
            <option value="">اختر المكلَّف</option>
            {(eligibleAssigneesQuery.data ?? []).map((user: EligibleUser) => (
              <option key={user.id} value={user.id}>{user.displayNameAr}</option>
            ))}
          </select>
          <input aria-label="سبب الإسناد" value={reason} onChange={(event) => setReason(event.target.value)} placeholder="سبب الإسناد" />
          <button type="submit" disabled={!canSubmitAssign || runAction.isPending}>{runAction.isPending ? 'جاري…' : 'تنفيذ'}</button>
          <button type="button" className="secondary" onClick={() => setActiveAction('')}>إلغاء</button>
          {runAction.isError && <span className="field-error">{runAction.error instanceof Error ? runAction.error.message : 'تعذر تنفيذ الإجراء.'}</span>}
        </form>
      )}

      {activeAction === 'VERIFY_CLOSURE' && (
        <form className="inline-action-form" onSubmit={(event) => { event.preventDefault(); runAction.mutate('VERIFY_CLOSURE') }}>
          <input aria-label="سبب الاعتماد" value={reason} onChange={(event) => setReason(event.target.value)} placeholder="سبب الاعتماد" />
          <input aria-label="ملخص الإغلاق" value={closureSummary} onChange={(event) => setClosureSummary(event.target.value)} placeholder="ملخص الإغلاق" />
          <button type="submit" disabled={!canSubmitClose || runAction.isPending}>{runAction.isPending ? 'جاري…' : 'اعتماد الإغلاق'}</button>
          <button type="button" className="secondary" onClick={() => setActiveAction('')}>إلغاء</button>
          {runAction.isError && <span className="field-error">{runAction.error instanceof Error ? runAction.error.message : 'تعذر تنفيذ الإجراء.'}</span>}
        </form>
      )}

      {activeAction && SIMPLE_INLINE_ACTIONS.has(activeAction) && (
        <form className="inline-action-form" onSubmit={(event) => { event.preventDefault(); runAction.mutate(activeAction) }}>
          <input aria-label="سبب الإجراء" value={reason} onChange={(event) => setReason(event.target.value)} placeholder="سبب الإجراء" />
          <button type="submit" disabled={!canSubmitSimple || runAction.isPending}>{runAction.isPending ? 'جاري…' : 'تنفيذ'}</button>
          <button type="button" className="secondary" onClick={() => setActiveAction('')}>إلغاء</button>
          {runAction.isError && <span className="field-error">{runAction.error instanceof Error ? runAction.error.message : 'تعذر تنفيذ الإجراء.'}</span>}
        </form>
      )}
      {runAction.isError && activeAction === '' && (
        <span className="field-error">{runAction.error instanceof Error ? runAction.error.message : 'تعذر تنفيذ الإجراء.'}</span>
      )}
    </div>
  )
}

function actionButtonClassName(
  activeAction: NoteWorkspaceAllowedAction | '',
  action: NoteWorkspaceAllowedAction,
  variant: 'primary' | 'secondary',
) {
  if (activeAction === action) {
    return undefined
  }

  if (variant === 'primary') {
    return undefined
  }

  return 'secondary'
}

function SummaryTab({ data }: Readonly<{ data: NoteWorkspaceDetail }>) {
  const locationLabel = noteLocationLabel(data.note)
  const ownerLabel = data.note.currentAssignment?.assignedToUserDisplayName || data.note.currentAssignment?.assignedToDepartmentName || '—'
  return (
    <div className="workspace-summary-grid">
      <div className="summary-description"><span className="muted">الوصف</span><p>{data.note.description}</p></div>
      <Metric label="المصدر" value={data.note.sourceAr} />
      <Metric label="التصنيف" value={data.note.noteTypeNameAr} />
      <Metric label="الأثر" value={data.note.severityAr} />
      <Metric label="الموقع/المنطقة" value={locationLabel} />
      <Metric label="المبلّغ" value={data.note.reportedByDisplayName || '—'} />
      <Metric label="المالك" value={ownerLabel} />
      <Metric label="الحالة" value={data.note.statusAr} />
      <Metric label="قرار الفرز" value={data.note.triageOutcomeAr || 'بانتظار الفرز'} />
      <Metric label="آخر إجراء" value={data.timeline[0]?.titleAr || '—'} />
      <Metric label="تاريخ الإنشاء" value={formatDate(data.note.createdAtUtc)} />
      <Metric label="تاريخ الاستحقاق" value={data.note.dueAtUtc ? formatDate(data.note.dueAtUtc) : '—'} />
    </div>
  )
}

// ===== Triage gate (قرار الفرز) — Layer 1, fully independent of treatment result =====

function TriageTab({ data }: Readonly<{ data: NoteWorkspaceDetail }>) {
  const queryClient = useQueryClient()
  const canProposeInvalid = usePermission('Notes.ProposeInvalid')
  const canProposeDuplicate = usePermission('Notes.ProposeDuplicate')
  const [mode, setMode] = useState<'' | 'invalid' | 'duplicate'>('')
  const [justification, setJustification] = useState('')
  const [originalNoteId, setOriginalNoteId] = useState('')

  const invalidateAll = async () => {
    await queryClient.invalidateQueries({ queryKey: ['notes-workspace'] })
    await queryClient.invalidateQueries({ queryKey: ['notes-workspace-detail', data.note.id] })
  }

  const proposeInvalid = useMutation({
    mutationFn: () => api.notes.triageProposeInvalid(data.note.id, { justificationAr: justification, rowVersion: data.note.rowVersion }),
    onSuccess: async () => { setMode(''); setJustification(''); await invalidateAll() },
  })
  const proposeDuplicate = useMutation({
    mutationFn: () => api.notes.triageProposeDuplicate(data.note.id, { originalNoteId, justificationAr: justification, rowVersion: data.note.rowVersion }),
    onSuccess: async () => { setMode(''); setJustification(''); setOriginalNoteId(''); await invalidateAll() },
  })

  const pendingDecision = data.decisionApprovals.find((d) => d.status === NoteDecisionApprovalStatus.Pending)
  const alreadyDecided = data.note.triageOutcome !== null && data.note.triageOutcome !== undefined

  if (alreadyDecided) {
    return (
      <div className="workspace-stack">
        <div className="workspace-row-card">
          <div>
            <strong>قرار فرز الملاحظة: {data.note.triageOutcomeAr}</strong>
            <p className="muted">
              {data.note.triageDecidedByDisplayName ? `بواسطة ${data.note.triageDecidedByDisplayName}` : ''}
              {data.note.triageDecidedAtUtc ? ` — ${formatDate(data.note.triageDecidedAtUtc)}` : ''}
            </p>
            {data.note.duplicateOfNoteReferenceNumber && (
              <p className="muted">الملاحظة الأصلية: {data.note.duplicateOfNoteReferenceNumber}</p>
            )}
          </div>
        </div>
        {pendingDecision && <PendingDecisionBanner approval={pendingDecision} data={data} />}
      </div>
    )
  }

  return (
    <div className="workspace-stack">
      <p className="muted">اختر قرار فرز الملاحظة. لا يظهر هذا القرار ضمن نتيجة المعالجة — طبقة مستقلة تمامًا.</p>
      <div className="workspace-actionbar-secondary">
        {canProposeInvalid && <button type="button" className="secondary" onClick={() => setMode('invalid')}>غير صحيحة</button>}
        {canProposeDuplicate && <button type="button" className="secondary" onClick={() => setMode('duplicate')}>مكررة</button>}
      </div>

      {mode === 'invalid' && (
        <form className="inline-action-form-stack" onSubmit={(event) => { event.preventDefault(); proposeInvalid.mutate() }}>
          <label htmlFor="triage-invalid-justification">مبرر اعتبار الملاحظة غير صحيحة</label>
          <textarea id="triage-invalid-justification" value={justification} onChange={(event) => setJustification(event.target.value)} rows={3} required />
          <div className="inline-action-form-buttons">
            <button type="submit" disabled={justification.trim().length < 3 || proposeInvalid.isPending}>{proposeInvalid.isPending ? 'جاري…' : 'اقتراح غير صحيحة'}</button>
            <button type="button" className="secondary" onClick={() => setMode('')}>إلغاء</button>
          </div>
          {proposeInvalid.isError && <span className="field-error">{errorMessageOf(proposeInvalid.error)}</span>}
        </form>
      )}

      {mode === 'duplicate' && (
        <form className="inline-action-form-stack" onSubmit={(event) => { event.preventDefault(); proposeDuplicate.mutate() }}>
          <label htmlFor="triage-duplicate-original">معرّف الملاحظة الأصلية</label>
          <input id="triage-duplicate-original" value={originalNoteId} onChange={(event) => setOriginalNoteId(event.target.value)} placeholder="GUID الملاحظة الأصلية" required />
          <label htmlFor="triage-duplicate-justification">مبرر اعتبارها مكررة</label>
          <textarea id="triage-duplicate-justification" value={justification} onChange={(event) => setJustification(event.target.value)} rows={3} required />
          <div className="inline-action-form-buttons">
            <button type="submit" disabled={!originalNoteId || justification.trim().length < 3 || proposeDuplicate.isPending}>{proposeDuplicate.isPending ? 'جاري…' : 'اقتراح مكررة'}</button>
            <button type="button" className="secondary" onClick={() => setMode('')}>إلغاء</button>
          </div>
          {proposeDuplicate.isError && <span className="field-error">{errorMessageOf(proposeDuplicate.error)}</span>}
        </form>
      )}
    </div>
  )
}

function PendingDecisionBanner({ approval, data }: Readonly<{ approval: NoteDecisionApproval; data: NoteWorkspaceDetail }>) {
  const queryClient = useQueryClient()
  const [reviewReason, setReviewReason] = useState('')
  const [showReturn, setShowReturn] = useState(false)
  const canApprove = data.actionCenter.canApprovePendingDecision

  const invalidateAll = async () => {
    await queryClient.invalidateQueries({ queryKey: ['notes-workspace'] })
    await queryClient.invalidateQueries({ queryKey: ['notes-workspace-detail', data.note.id] })
  }

  const approve = useMutation({
    mutationFn: () => api.notes.approveDecision(data.note.id, approval.id, { reviewReason: reviewReason || null, rowVersion: approval.rowVersion }),
    onSuccess: invalidateAll,
  })
  const returnDecision = useMutation({
    mutationFn: () => api.notes.returnDecision(data.note.id, approval.id, { reviewReason, rowVersion: approval.rowVersion }),
    onSuccess: async () => { setShowReturn(false); setReviewReason(''); await invalidateAll() },
  })

  return (
    <div className="workspace-row-card" data-tone="warn">
      <div>
        <strong>بانتظار الاعتماد: {approval.decisionTypeAr}</strong>
        <p className="muted">اقترحه {approval.proposedByDisplayName ?? 'مستخدم'} — {formatDate(approval.proposedAtUtc)}</p>
        {approval.justificationAr && <p>{approval.justificationAr}</p>}
      </div>
      {canApprove && !showReturn && (
        <div className="inline-action-form-buttons">
          <button type="button" onClick={() => approve.mutate()} disabled={approve.isPending}>{approve.isPending ? 'جاري…' : 'اعتماد'}</button>
          <button type="button" className="secondary" onClick={() => setShowReturn(true)}>إعادة</button>
        </div>
      )}
      {!canApprove && <span className="muted">يتطلب مراجعًا مستقلًا عن مقترح القرار.</span>}
      {showReturn && (
        <form className="inline-action-form" onSubmit={(event) => { event.preventDefault(); returnDecision.mutate() }}>
          <input aria-label="سبب الإعادة" value={reviewReason} onChange={(event) => setReviewReason(event.target.value)} placeholder="سبب الإعادة (إلزامي)" required />
          <button type="submit" disabled={reviewReason.trim().length < 3 || returnDecision.isPending}>{returnDecision.isPending ? 'جاري…' : 'تأكيد الإعادة'}</button>
          <button type="button" className="secondary" onClick={() => setShowReturn(false)}>إلغاء</button>
        </form>
      )}
      {(approve.isError || returnDecision.isError) && (
        <span className="field-error">{errorMessageOf(approve.error ?? returnDecision.error)}</span>
      )}
    </div>
  )
}

function AssignmentTab({ data }: Readonly<{ data: NoteWorkspaceDetail }>) {
  if (data.assignments.length === 0) return <WorkspaceEmptyState title="لا توجد تكليفات مسجلة" />
  return <div className="workspace-stack">{data.assignments.map((assignment) => (
    <div key={assignment.id} className="workspace-row-card">
      <div><strong>{assignment.assignedToUserDisplayName || assignment.assignedToDepartmentName}</strong><p className="muted">{assignment.reason}</p></div>
      <span>{assignment.isCurrent ? 'حالي' : 'سابق'}</span>
      <span>{assignment.acceptedAtUtc ? 'مقبول' : 'بانتظار القبول'}</span>
      <span>{formatDate(assignment.assignedAtUtc)}</span>
    </div>
  ))}</div>
}

// ===== Treatment result (نتيجة المعالجة) — Layer 2, only reachable once TriageOutcome=Valid =====

function TreatmentTab({ data }: Readonly<{ data: NoteWorkspaceDetail }>) {
  const queryClient = useQueryClient()
  const canRecord = usePermission('Notes.StartWork')
  const canProposeNoAction = usePermission('Notes.ProposeNoAction')
  const [choice, setChoice] = useState<'' | 'treated' | 'noaction'>('')
  const [resultText, setResultText] = useState('')
  const [executionType, setExecutionType] = useState<number>(NoteTreatmentExecutionType.Direct)
  const [justification, setJustification] = useState('')

  const invalidateAll = async () => {
    await queryClient.invalidateQueries({ queryKey: ['notes-workspace'] })
    await queryClient.invalidateQueries({ queryKey: ['notes-workspace-detail', data.note.id] })
  }

  const recordTreatment = useMutation({
    mutationFn: () => api.notes.recordTreatment(data.note.id, { treatmentResultText: resultText, executionType, rowVersion: data.note.rowVersion }),
    onSuccess: async () => { setChoice(''); await invalidateAll() },
  })
  const proposeNoAction = useMutation({
    mutationFn: () => api.notes.proposeNoAction(data.note.id, { justificationAr: justification, rowVersion: data.note.rowVersion }),
    onSuccess: async () => { setChoice(''); setJustification(''); await invalidateAll() },
  })

  const pendingDecision = data.decisionApprovals.find((d) => d.status === NoteDecisionApprovalStatus.Pending)
  const alreadyRecorded = data.note.treatmentResultType !== null && data.note.treatmentResultType !== undefined

  return (
    <div className="workspace-stack">
      {alreadyRecorded && (
        <div className="workspace-summary-grid">
          <Metric label="نتيجة المعالجة" value={data.note.treatmentResultTypeAr || '—'} />
          {data.note.treatmentResultType === NoteTreatmentResultType.Treated && (
            <Metric label="نوع التنفيذ" value={data.note.treatmentExecutionTypeAr || '—'} />
          )}
          <div className="summary-description">
            <span className="muted">{data.note.treatmentResultType === NoteTreatmentResultType.Treated ? 'نتيجة المعالجة' : 'مبرر عدم الحاجة إلى إجراء'}</span>
            <p>{data.note.treatmentResultText || data.note.noActionJustificationAr || '—'}</p>
          </div>
        </div>
      )}
      {pendingDecision && <PendingDecisionBanner approval={pendingDecision} data={data} />}

      {!alreadyRecorded && choice === '' && (
        <div className="workspace-actionbar-secondary">
          {canRecord && <button type="button" onClick={() => setChoice('treated')}>معالجة</button>}
          {canProposeNoAction && <button type="button" className="secondary" onClick={() => setChoice('noaction')}>لا تتطلب إجراء</button>}
        </div>
      )}

      {choice === 'treated' && (
        <form className="inline-action-form-stack" onSubmit={(event) => { event.preventDefault(); recordTreatment.mutate() }}>
          <label htmlFor="treatment-result-text">نتيجة المعالجة</label>
          <textarea id="treatment-result-text" value={resultText} onChange={(event) => setResultText(event.target.value)} rows={3} required />
          <label htmlFor="treatment-execution-type">نوع التنفيذ</label>
          <select id="treatment-execution-type" value={executionType} onChange={(event) => setExecutionType(Number(event.target.value))}>
            <option value={NoteTreatmentExecutionType.Direct}>{NoteTreatmentExecutionTypeLabelsAr[NoteTreatmentExecutionType.Direct]}</option>
            {data.note.noteTypeSupportsPartsWorkflow && (
              <option value={NoteTreatmentExecutionType.RequiresParts}>{NoteTreatmentExecutionTypeLabelsAr[NoteTreatmentExecutionType.RequiresParts]}</option>
            )}
          </select>
          <div className="inline-action-form-buttons">
            <button type="submit" disabled={resultText.trim().length < 3 || recordTreatment.isPending}>{recordTreatment.isPending ? 'جاري…' : 'حفظ نتيجة المعالجة'}</button>
            <button type="button" className="secondary" onClick={() => setChoice('')}>إلغاء</button>
          </div>
          {recordTreatment.isError && <span className="field-error">{errorMessageOf(recordTreatment.error)}</span>}
        </form>
      )}

      {choice === 'noaction' && (
        <form className="inline-action-form-stack" onSubmit={(event) => { event.preventDefault(); proposeNoAction.mutate() }}>
          <label htmlFor="no-action-justification">مبرر عدم الحاجة إلى إجراء</label>
          <textarea id="no-action-justification" value={justification} onChange={(event) => setJustification(event.target.value)} rows={3} required />
          <div className="inline-action-form-buttons">
            <button type="submit" disabled={justification.trim().length < 3 || proposeNoAction.isPending}>{proposeNoAction.isPending ? 'جاري…' : 'اقتراح لا تتطلب إجراء'}</button>
            <button type="button" className="secondary" onClick={() => setChoice('')}>إلغاء</button>
          </div>
          {proposeNoAction.isError && <span className="field-error">{errorMessageOf(proposeNoAction.error)}</span>}
        </form>
      )}

      {data.note.treatmentExecutionType === NoteTreatmentExecutionType.RequiresParts && (
        <p className="muted">هذه الملاحظة تتطلب قطعًا أو مواد — راجع قسم «القطع والمواد».</p>
      )}

      <div className="workspace-stack">
        <strong>الإجراءات التصحيحية المرتبطة</strong>
        {data.correctiveActions.items.length === 0 && <WorkspaceEmptyState title="لا توجد إجراءات تصحيحية مرتبطة" />}
        {data.correctiveActions.items.map((action) => (
          <div key={action.id} className="workspace-row-card">
            <div><strong>{action.title}</strong><p className="muted">{action.descriptionSnippet || 'لا يوجد وصف مختصر.'}</p></div>
            <span className="badge" data-tone={action.isOverdue ? 'danger' : 'muted'}>{action.statusAr}</span>
            <span>{action.currentAssigneeDisplay || 'بلا مسؤول'}</span>
            <span>{action.dueAtUtc ? formatDate(action.dueAtUtc) : 'دون استحقاق'}</span>
          </div>
        ))}
        <Link to={`/notes/${data.note.id}/corrective-actions/new`} className="muted">
          فتح صفحة الإجراءات التصحيحية المتقدمة ←
        </Link>
      </div>
    </div>
  )
}

// ===== Parts & materials (القطع والمواد) — real multiplicity, inline management =====

function PartsTab({ data }: Readonly<{ data: NoteWorkspaceDetail }>) {
  const canManage = usePermission('Notes.StartWork')
  const canApproveSlaPause = usePermission('Notes.ApproveSlaPause')
  const [showAdd, setShowAdd] = useState(false)
  const progress = data.actionCenter.partsProgress

  return (
    <div className="workspace-stack">
      {progress && (
        <p className="muted">{progress.installed} من {progress.total} تم تركيبها ({progress.cancelled} ملغاة، {progress.remaining} متبقية)</p>
      )}
      {canManage && (
        <div>
          <button type="button" className="secondary" onClick={() => setShowAdd((v) => !v)}>
            {showAdd ? 'إخفاء نموذج الإضافة' : 'إضافة قطعة أو مادة'}
          </button>
        </div>
      )}
      {showAdd && <AddPartForm data={data} onDone={() => setShowAdd(false)} />}

      {data.partsRequirements.length === 0 && <WorkspaceEmptyState title="لا توجد عناصر قطع مسجلة" />}
      {data.partsRequirements.map((item) => (
        <PartRow key={item.id} item={item} noteId={data.note.id} canManage={canManage} />
      ))}

      <SlaPauseSection data={data} canManage={canManage} canApprove={canApproveSlaPause} />
    </div>
  )
}

function AddPartForm({ data, onDone }: Readonly<{ data: NoteWorkspaceDetail; onDone: () => void }>) {
  const queryClient = useQueryClient()
  const [itemName, setItemName] = useState('')
  const [itemCode, setItemCode] = useState('')
  const [quantity, setQuantity] = useState('1')
  const [unit, setUnit] = useState('')
  const [requestNumber, setRequestNumber] = useState('')
  const [supplierOrSource, setSupplierOrSource] = useState('')

  const add = useMutation({
    mutationFn: () => api.notes.addPart(data.note.id, {
      itemName,
      itemCode: itemCode || null,
      quantity: Number(quantity),
      unit,
      requestNumber: requestNumber || null,
      supplierOrSource: supplierOrSource || null,
      notes: null,
    }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['notes-workspace-detail', data.note.id] })
      onDone()
    },
  })

  return (
    <form className="inline-action-form-stack" onSubmit={(event) => { event.preventDefault(); add.mutate() }}>
      <input aria-label="اسم القطعة" value={itemName} onChange={(event) => setItemName(event.target.value)} placeholder="اسم القطعة أو المادة" required />
      <input aria-label="رمز القطعة" value={itemCode} onChange={(event) => setItemCode(event.target.value)} placeholder="رمز القطعة (اختياري)" />
      <input aria-label="الكمية" type="number" min="0.001" step="0.001" value={quantity} onChange={(event) => setQuantity(event.target.value)} required />
      <input aria-label="الوحدة" value={unit} onChange={(event) => setUnit(event.target.value)} placeholder="الوحدة" required />
      <input aria-label="رقم الطلب" value={requestNumber} onChange={(event) => setRequestNumber(event.target.value)} placeholder="رقم طلب التوريد (اختياري)" />
      <input aria-label="جهة التوريد" value={supplierOrSource} onChange={(event) => setSupplierOrSource(event.target.value)} placeholder="الجهة المسؤولة عن التوريد (اختياري)" />
      <div className="inline-action-form-buttons">
        <button type="submit" disabled={!itemName.trim() || !unit.trim() || add.isPending}>{add.isPending ? 'جاري…' : 'إضافة'}</button>
        <button type="button" className="secondary" onClick={onDone}>إلغاء</button>
      </div>
      {add.isError && <span className="field-error">{errorMessageOf(add.error)}</span>}
    </form>
  )
}

function resolvePartStatusTone(status: number): 'ok' | 'danger' | 'muted' {
  if (status === NotePartsRequirementStatus.Installed) {
    return 'ok'
  }

  if (status === NotePartsRequirementStatus.Cancelled) {
    return 'danger'
  }

  return 'muted'
}

function PartRow({ item, noteId, canManage }: Readonly<{ item: NotePartsRequirement; noteId: string; canManage: boolean }>) {
  const queryClient = useQueryClient()
  const [showCancel, setShowCancel] = useState(false)
  const [cancelReason, setCancelReason] = useState('')
  const locked = item.status === NotePartsRequirementStatus.Installed || item.status === NotePartsRequirementStatus.Cancelled

  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: ['notes-workspace-detail', noteId] })
  }

  const updateStatus = useMutation({
    mutationFn: (status: number) => api.notes.updatePartStatus(noteId, item.id, { status, rowVersion: item.rowVersion }),
    onSuccess: invalidate,
  })
  const cancelPart = useMutation({
    mutationFn: () => api.notes.cancelPart(noteId, item.id, { reason: cancelReason, rowVersion: item.rowVersion }),
    onSuccess: async () => { setShowCancel(false); setCancelReason(''); await invalidate() },
  })
  const deletePart = useMutation({
    mutationFn: () => api.notes.deletePart(noteId, item.id),
    onSuccess: invalidate,
  })

  return (
    <div className="workspace-row-card">
      <div>
        <strong>{item.itemName}</strong>
        <p className="muted">{item.itemCode ? `${item.itemCode} — ` : ''}{item.quantity} {item.unit}{item.requestNumber ? ` — طلب ${item.requestNumber}` : ''}</p>
        {item.cancelReason && <p className="muted">سبب الإلغاء: {item.cancelReason}</p>}
      </div>
      <span className="badge" data-tone={resolvePartStatusTone(item.status)}>{item.statusAr}</span>
      {canManage && !locked && (
        <select
          aria-label={`حالة ${item.itemName}`}
          value={item.status}
          onChange={(event) => updateStatus.mutate(Number(event.target.value))}
          disabled={updateStatus.isPending}
        >
          <option value={0}>مطلوبة</option>
          <option value={1}>قيد التوريد</option>
          <option value={2}>متوفرة</option>
          <option value={3}>تم الاستلام</option>
          <option value={4}>تم التركيب</option>
        </select>
      )}
      {canManage && !locked && (
        <div className="inline-action-form-buttons">
          {!showCancel && <button type="button" className="secondary" onClick={() => setShowCancel(true)}>إلغاء العنصر</button>}
          {!showCancel && <button type="button" className="secondary" onClick={() => deletePart.mutate()} disabled={deletePart.isPending}>حذف</button>}
        </div>
      )}
      {showCancel && (
        <form className="inline-action-form" onSubmit={(event) => { event.preventDefault(); cancelPart.mutate() }}>
          <input aria-label="سبب إلغاء العنصر" value={cancelReason} onChange={(event) => setCancelReason(event.target.value)} placeholder="سبب الإلغاء" required />
          <button type="submit" disabled={cancelReason.trim().length < 3 || cancelPart.isPending}>{cancelPart.isPending ? 'جاري…' : 'تأكيد'}</button>
          <button type="button" className="secondary" onClick={() => setShowCancel(false)}>تراجع</button>
        </form>
      )}
      {(updateStatus.isError || cancelPart.isError || deletePart.isError) && (
        <span className="field-error">{errorMessageOf(updateStatus.error ?? cancelPart.error ?? deletePart.error)}</span>
      )}
    </div>
  )
}

function SlaPauseSection({ data, canManage, canApprove }: Readonly<{ data: NoteWorkspaceDetail; canManage: boolean; canApprove: boolean }>) {
  const queryClient = useQueryClient()
  const [showForm, setShowForm] = useState(false)
  const [reason, setReason] = useState('')
  const [selectedPartIds, setSelectedPartIds] = useState<string[]>([])
  const sla = data.sla

  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: ['notes-workspace-detail', data.note.id] })
  }

  const requestPause = useMutation({
    mutationFn: () => api.notes.requestSlaPause(data.note.id, {
      reason,
      relatedPartsRequirementIds: selectedPartIds,
      reviewDueAtUtc: null,
      rowVersion: data.note.rowVersion,
    }),
    onSuccess: async () => { setShowForm(false); setReason(''); setSelectedPartIds([]); await invalidate() },
  })
  const approvePause = useMutation({
    mutationFn: () => api.notes.approveSlaPause(data.note.id, sla.activePauseId as string, { reason: null, rowVersion: data.note.rowVersion }),
    onSuccess: invalidate,
  })

  function togglePart(id: string) {
    setSelectedPartIds((current) => current.includes(id) ? current.filter((x) => x !== id) : [...current, id])
  }

  return (
    <div className="workspace-stack">
      <strong>تجميد SLA أثناء انتظار القطع</strong>
      {sla.isProcessingSlaPaused ? (
        <div className="workspace-row-card" data-tone="warn">
          <div>
            <strong>SLA المعالجة متوقف مؤقتًا</strong>
            <p className="muted">{sla.activePauseReason}</p>
          </div>
        </div>
      ) : (
        <>
          {sla.activePauseId && canApprove && (
            <div className="workspace-row-card" data-tone="warn">
              <div><strong>طلب تجميد بانتظار الاعتماد</strong></div>
              <button type="button" onClick={() => approvePause.mutate()} disabled={approvePause.isPending}>{approvePause.isPending ? 'جاري…' : 'اعتماد التجميد'}</button>
            </div>
          )}
          {!sla.activePauseId && canManage && (
            <div>
              <button type="button" className="secondary" onClick={() => setShowForm((v) => !v)}>
                {showForm ? 'إخفاء نموذج طلب التجميد' : 'طلب تجميد SLA'}
              </button>
            </div>
          )}
        </>
      )}
      {showForm && (
        <form className="inline-action-form-stack" onSubmit={(event) => { event.preventDefault(); requestPause.mutate() }}>
          <label htmlFor="sla-pause-reason">سبب طلب التجميد</label>
          <input id="sla-pause-reason" value={reason} onChange={(event) => setReason(event.target.value)} placeholder="بانتظار توريد من المورد" required />
          <fieldset>
            <legend>العناصر المرتبطة (يجب أن يحمل أحدها رقم طلب وجهة توريد)</legend>
            {data.partsRequirements.map((item) => (
              <label key={item.id} className="compact-check">
                <input type="checkbox" checked={selectedPartIds.includes(item.id)} onChange={() => togglePart(item.id)} />
                {item.itemName}
              </label>
            ))}
          </fieldset>
          <div className="inline-action-form-buttons">
            <button type="submit" disabled={!reason.trim() || selectedPartIds.length === 0 || requestPause.isPending}>{requestPause.isPending ? 'جاري…' : 'إرسال الطلب'}</button>
            <button type="button" className="secondary" onClick={() => setShowForm(false)}>إلغاء</button>
          </div>
          {requestPause.isError && <span className="field-error">{errorMessageOf(requestPause.error)}</span>}
        </form>
      )}
      {approvePause.isError && <span className="field-error">{errorMessageOf(approvePause.error)}</span>}
    </div>
  )
}

function EvidenceTab({ data }: Readonly<{ data: NoteWorkspaceDetail }>) {
  const queryClient = useQueryClient()
  const [file, setFile] = useState<File | null>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const canUpload = usePermission('Attachments.Upload')
  const upload = useMutation({
    mutationFn: async () => {
      if (!file) throw new Error('اختر ملفًا أولًا.')
      return api.uploadAttachment(file, 'OperationalNote', data.note.id, 'مرفق داعم للملاحظة')
    },
    onSuccess: async () => {
      setFile(null)
      if (fileInputRef.current) {
        fileInputRef.current.value = ''
      }

      await queryClient.invalidateQueries({ queryKey: ['notes-workspace-detail', data.note.id] })
    },
  })

  return (
    <div className="workspace-stack">
      {canUpload && (
        <form
          className="inline-action-form"
          onSubmit={(event) => { event.preventDefault(); upload.mutate() }}
        >
          <input
            ref={fileInputRef}
            aria-label="إضافة مرفق"
            type="file"
            onChange={(event) => setFile(event.target.files?.[0] ?? null)}
          />
          <button type="submit" disabled={!file || upload.isPending}>{upload.isPending ? 'جاري الرفع…' : 'رفع المرفق'}</button>
          {upload.isError && <span className="field-error">{upload.error instanceof Error ? upload.error.message : 'تعذر رفع المرفق.'}</span>}
        </form>
      )}
      {data.attachments.length === 0 && <WorkspaceEmptyState title="لا توجد مرفقات" />}
      {data.attachments.map((attachment) => (
        <div key={attachment.id} className="workspace-row-card">
          <strong>{attachment.originalFileName}</strong>
          <span>{attachment.contentType}</span>
          <span>{Math.ceil(attachment.sizeBytes / 1024)} KB</span>
          <span>{formatDate(attachment.uploadedAtUtc)}</span>
        </div>
      ))}
    </div>
  )
}

function ApprovalsTab({ data }: Readonly<{ data: NoteWorkspaceDetail }>) {
  if (data.decisionApprovals.length === 0) {
    return <WorkspaceEmptyState title="لا توجد طلبات اعتماد على هذه الملاحظة" />
  }

  return (
    <div className="workspace-stack">
      {data.decisionApprovals.map((approval) =>
        approval.status === NoteDecisionApprovalStatus.Pending ? (
          <PendingDecisionBanner key={approval.id} approval={approval} data={data} />
        ) : (
          <div key={approval.id} className="workspace-row-card">
            <div>
              <strong>{approval.decisionTypeAr}</strong>
              <p className="muted">
                اقترحه {approval.proposedByDisplayName ?? 'مستخدم'} — {formatDate(approval.proposedAtUtc)}
              </p>
              {approval.reviewedByDisplayName && (
                <p className="muted">
                  {approval.statusAr} بواسطة {approval.reviewedByDisplayName}
                  {approval.reviewedAtUtc ? ` — ${formatDate(approval.reviewedAtUtc)}` : ''}
                </p>
              )}
              {approval.reviewReason && <p>{approval.reviewReason}</p>}
            </div>
            <span className="badge" data-tone={approval.status === NoteDecisionApprovalStatus.Approved ? 'ok' : 'warn'}>{approval.statusAr}</span>
          </div>
        ),
      )}
    </div>
  )
}

function EscalationsTab() {
  // Not Applicable (documented, phase1a-observation-completion-report.md): there is no standalone
  // "escalate from note" domain command in this system — escalation is policy-driven
  // (EscalationPolicy/EscalationRule against SLA breach), not a manual per-note action.
  return (
    <WorkspaceEmptyState
      title="لا يوجد تصعيد يدوي مباشر من الملاحظة"
      hint="التصعيد في هذا النظام مبني على سياسات SLA تلقائية، وليس أمرًا مستقلًا من داخل الملاحظة (موثّق في docs/ux-rescue)."
    />
  )
}

function HistoryTab({ data }: Readonly<{ data: NoteWorkspaceDetail }>) {
  if (data.timeline.length === 0) return <WorkspaceEmptyState title="لا يوجد سجل زمني بعد" />
  return <ol className="workspace-timeline">{data.timeline.map((entry) => (
    <li key={`${entry.type}-${entry.id}`} data-tone={entry.tone}>
      <strong>{entry.titleAr}</strong>
      {entry.descriptionAr && <p>{entry.descriptionAr}</p>}
      <span>{entry.actorDisplayName || 'النظام'} · {formatDate(entry.occurredAtUtc)}</span>
    </li>
  ))}</ol>
}

function NoSelection() {
  return <div className="workspace-no-selection"><strong>اختر ملاحظة</strong><p className="muted">ستظهر المعالجة والتكليف والأدلة والسجل الزمني هنا دون مغادرة الصفحة.</p></div>
}

function Metric({ label, value }: Readonly<{ label: string; value: string }>) {
  return <div className="metric"><span className="muted">{label}</span><strong>{value}</strong></div>
}

function formatDate(value: string) {
  return DATE_FORMAT.format(new Date(value))
}

function formatDuration(totalSeconds: number) {
  const days = Math.floor(totalSeconds / 86400)
  const hours = Math.floor((totalSeconds % 86400) / 3600)
  if (days > 0) return `${days} يومًا و${hours} ساعة`
  const minutes = Math.floor((totalSeconds % 3600) / 60)
  if (hours > 0) return `${hours} ساعة و${minutes} دقيقة`
  return `${minutes} دقيقة`
}

function errorMessageOf(error: unknown) {
  if (error instanceof ApiError) return error.message
  if (error instanceof Error) return error.message
  return 'تعذر تنفيذ الإجراء.'
}

function shortId(value: string) {
  return value.slice(0, 8)
}

function appendFilterParam(params: URLSearchParams, key: string, value: string | number | boolean | undefined) {
  if (value !== undefined && value !== '' && value !== false) {
    params.set(key, String(value))
  }
}

function noteLocationLabel(note: Pick<NoteListItem, 'facilityId' | 'regionId'>) {
  if (note.facilityId) {
    return `سجن ${shortId(note.facilityId)}`
  }

  if (note.regionId) {
    return `منطقة ${shortId(note.regionId)}`
  }

  return 'نطاق عام'
}
