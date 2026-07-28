import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import {
  api,
  ApiError,
  type EligibleUser,
  type NoteListFilters,
  type NoteListItem,
  type NoteWorkspaceAllowedAction,
  type NoteWorkspaceDetail,
} from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import { NoteSeverityLabelsAr, NoteStatusLabelsAr, enumOptions, severityTone, statusTone } from '../../notes/noteEnums'
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
}

// Actions with a simple "reason only" inline form.
const SIMPLE_INLINE_ACTIONS = new Set<NoteWorkspaceAllowedAction>([
  'SUBMIT',
  'START_WORK',
  'REQUEST_VERIFICATION',
  'REJECT_VERIFICATION',
  'REOPEN',
  'CANCEL',
])

const SECTIONS = [
  ['summary', 'الملخص'],
  ['processing', 'المعالجة'],
  ['assignment', 'التكليف'],
  ['evidence', 'الأدلة'],
  ['history', 'السجل الزمني'],
] as const

type SectionKey = (typeof SECTIONS)[number][0]

const SECTION_KEYS = new Set<SectionKey>(
  SECTIONS.map(([key]) => key),
)

function resolveSectionKey(value: string | null): SectionKey {
  if (value && SECTION_KEYS.has(value as SectionKey)) {
    return value as SectionKey
  }

  return 'summary'
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
    pushNextUrlUpdateRef.current = true
    setSelectedId('')
    window.setTimeout(() => selectedCardRef.current?.focus(), 0)
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
            {data.summary.currentBlockerAr && <span className="badge" data-tone="warn">{data.summary.currentBlockerAr}</span>}
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

      <nav className="workspace-prev-next" aria-label="التنقل بين الملاحظات">
        <button type="button" className="secondary" disabled={!previousNote} onClick={() => previousNote && onNavigate(previousNote.id)} title="alt+→">
          ‹ السابقة
        </button>
        {position && <span className="muted">{position.index + 1} من {position.total} (ضمن النتائج المحملة)</span>}
        <button type="button" className="secondary" disabled={!nextNote} onClick={() => nextNote && onNavigate(nextNote.id)} title="alt+←">
          التالية ›
        </button>
      </nav>

      <ActionBar data={data} />
      <nav className="workspace-tabs" aria-label="أقسام الملاحظة">
        {SECTIONS.map(([key, label]) => (
          <button key={key} type="button" className={activeSection === key ? 'active' : undefined} onClick={() => onSectionChange(key)}>
            {label}
          </button>
        ))}
      </nav>
      <section className="workspace-tab-panel">
        {activeSection === 'summary' && <SummaryTab data={data} />}
        {activeSection === 'processing' && <ProcessingTab data={data} />}
        {activeSection === 'assignment' && <AssignmentTab data={data} />}
        {activeSection === 'evidence' && <EvidenceTab data={data} />}
        {activeSection === 'history' && <HistoryTab data={data} />}
      </section>
    </article>
  )
}

function primaryAndSecondaryActions(allowedActions: NoteWorkspaceAllowedAction[]) {
  const [primary, ...secondary] = allowedActions
  return { primary, secondary }
}

function ActionBar({ data }: Readonly<{ data: NoteWorkspaceDetail }>) {
  const queryClient = useQueryClient()
  const [reason, setReason] = useState('')
  const [closureSummary, setClosureSummary] = useState('')
  const [assigneeUserId, setAssigneeUserId] = useState('')
  const [activeAction, setActiveAction] = useState<NoteWorkspaceAllowedAction | ''>('')
  const { primary, secondary } = primaryAndSecondaryActions(data.allowedActions)

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
          {secondary.map((action) => renderButton(action, 'secondary'))}
        </div>
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
      <Metric label="آخر إجراء" value={data.timeline[0]?.titleAr || '—'} />
      <Metric label="تاريخ الإنشاء" value={formatDate(data.note.createdAtUtc)} />
      <Metric label="تاريخ الاستحقاق" value={data.note.dueAtUtc ? formatDate(data.note.dueAtUtc) : '—'} />
    </div>
  )
}

function ProcessingTab({ data }: Readonly<{ data: NoteWorkspaceDetail }>) {
  const verificationStatus = verificationStatusLabel(data)
  return (
    <div className="workspace-stack">
      <div className="workspace-summary-grid">
        <Metric label="حالة المعالجة" value={data.note.statusAr} />
        <Metric label="حالة التحقق" value={verificationStatus} />
        <Metric label="الإجراءات المفتوحة" value={String(data.summary.openCorrectiveActions)} />
        <Metric label="ملخص الإغلاق" value={data.note.closureSummary || '—'} />
      </div>
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

function verificationStatusLabel(data: NoteWorkspaceDetail) {
  if (data.summary.waitingVerification) {
    return 'بانتظار التحقق'
  }

  if (data.note.closedAtUtc) {
    return 'مغلق بعد التحقق'
  }

  return 'غير مطلوب حاليًا'
}
