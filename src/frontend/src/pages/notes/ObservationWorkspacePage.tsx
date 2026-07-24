import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import {
  api,
  ApiError,
  type NoteListFilters,
  type NoteListItem,
  type NoteWorkspaceAllowedAction,
  type NoteWorkspaceDetail,
} from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import { NoteSeverityLabelsAr, NoteStatusLabelsAr, enumOptions, severityTone, statusTone } from '../../notes/noteEnums'
import { listQueryErrorMessage } from '../../shared/listPageUtils'

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
  REOPEN: 'إعادة فتح',
  CANCEL: 'إلغاء',
}

const INLINE_ACTIONS = new Set<NoteWorkspaceAllowedAction>([
  'SUBMIT',
  'START_WORK',
  'REQUEST_VERIFICATION',
  'REJECT_VERIFICATION',
  'REOPEN',
  'CANCEL',
])

const TABS = [
  ['summary', 'الملخص'],
  ['actions', 'الإجراءات'],
  ['assignments', 'التكليفات'],
  ['resources', 'الموارد'],
  ['verification', 'التحقق'],
  ['rca', 'RCA/CAPA'],
  ['attachments', 'المرفقات'],
  ['links', 'الروابط'],
  ['decisions', 'القرارات'],
  ['timeline', 'Timeline'],
] as const

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
  const [overdueOnly, setOverdueOnly] = useState(searchParams.get('overdueOnly') === 'true')
  const [requiresMyAction, setRequiresMyAction] = useState(searchParams.get('requiresMyAction') === 'true')
  const [requiresRouting, setRequiresRouting] = useState(searchParams.get('requiresRouting') === 'true')
  const [page, setPage] = useState(Number(searchParams.get('page') ?? '1') || 1)
  const [sortBy] = useState(searchParams.get('sortBy') ?? 'createdAtUtc')
  const [sortDesc] = useState(searchParams.get('sortDesc') !== 'false')
  const [selectedId, setSelectedId] = useState(searchParams.get('noteId') ?? '')
  const [listCollapsed, setListCollapsed] = useState(false)
  const [activeTab, setActiveTab] = useState<(typeof TABS)[number][0]>('summary')
  const listScrollRef = useRef<HTMLDivElement | null>(null)
  const searchDebounceMountedRef = useRef(false)

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
      overdueOnly: overdueOnly || undefined,
      requiresMyAction: requiresMyAction || undefined,
      requiresRouting: requiresRouting || undefined,
      sortBy,
      sortDesc,
    }),
    [page, debouncedSearch, status, severity, regionId, facilityId, overdueOnly, requiresMyAction, requiresRouting, sortBy, sortDesc],
  )

  useEffect(() => {
    const next = new URLSearchParams()
    Object.entries(filters).forEach(([key, value]) => {
      appendFilterParam(next, key, value)
    })
    if (selectedId) next.set('noteId', selectedId)
    setSearchParams(next, { replace: true })
  }, [filters, selectedId, setSearchParams])

  const regionsQuery = useQuery({ queryKey: ['workspace-regions'], queryFn: () => api.regions(), enabled: canView })
  const facilitiesQuery = useQuery({
    queryKey: ['workspace-facilities', regionId],
    queryFn: () => api.facilities(regionId || undefined),
    enabled: canView,
  })
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

  if (!canView) {
    return <div className="error" role="alert">ليست لديك صلاحية عرض مساحة عمل الملاحظات.</div>
  }

  const notes = listQuery.data?.notes.items ?? []
  const totalCount = listQuery.data?.notes.totalCount ?? 0
  const totalPages = listQuery.data ? Math.max(1, Math.ceil(totalCount / PAGE_SIZE)) : 1
  const errorMessage = listQueryErrorMessage(listQuery.error, 'ليست لديك صلاحية عرض الملاحظات.', 'تعذر تحميل مساحة عمل الملاحظات.')

  const selectNote = (id: string) => {
    setSelectedId(id)
    setActiveTab('summary')
  }

  return (
    <div className="observation-workspace">
      <header className="workspace-topbar">
        <div>
          <h1 className="page-title">مساحة عمل الملاحظات</h1>
          <p className="muted">قائمة، تفاصيل، إجراءات، تكليفات، تحقق، مرفقات وسجل زمني من صفحة واحدة.</p>
        </div>
        <div className="workspace-topbar-actions">
          <button type="button" className="secondary" onClick={() => setListCollapsed((v) => !v)}>
            {listCollapsed ? 'إظهار القائمة' : 'طي القائمة'}
          </button>
          {canCreate && (
            <Link to="/notes/new">
              <button type="button">ملاحظة جديدة</button>
            </Link>
          )}
        </div>
      </header>

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
        <select aria-label="المنطقة" value={regionId} onChange={(e) => { setPage(1); setRegionId(e.target.value); setFacilityId('') }}>
          <option value="">كل المناطق</option>
          {regionsQuery.data?.items.map((region) => <option key={region.id} value={region.id}>{region.nameAr}</option>)}
        </select>
        <select aria-label="السجن" value={facilityId} onChange={(e) => { setPage(1); setFacilityId(e.target.value) }}>
          <option value="">كل السجون</option>
          {facilitiesQuery.data?.items.map((facility) => <option key={facility.id} value={facility.id}>{facility.nameAr}</option>)}
        </select>
        <label className="compact-check"><input type="checkbox" checked={overdueOnly} onChange={(e) => { setPage(1); setOverdueOnly(e.target.checked) }} /> المتأخرة</label>
        <label className="compact-check"><input type="checkbox" checked={requiresMyAction} onChange={(e) => { setPage(1); setRequiresMyAction(e.target.checked) }} /> بانتظار إجراء مني</label>
        <label className="compact-check"><input type="checkbox" checked={requiresRouting} onChange={(e) => { setPage(1); setRequiresRouting(e.target.checked) }} /> تحتاج توجيه</label>
      </section>

      {listQuery.isError && <div className="error" role="alert"><span>{errorMessage}</span><button type="button" className="secondary" onClick={() => listQuery.refetch()}>إعادة المحاولة</button></div>}

      <div className={`workspace-grid ${listCollapsed ? 'is-collapsed' : ''} ${selectedId ? 'has-selection' : ''}`}>
        <aside className="workspace-list-pane" aria-label="قائمة الملاحظات">
          <div className="workspace-list-header">
            <strong>الملاحظات</strong>
            <span className="muted">{totalCount} نتيجة</span>
          </div>
          <div className="workspace-list" ref={listScrollRef}>
            {listQuery.isLoading && Array.from({ length: 5 }).map((_, index) => <div key={index} className="observation-card-skeleton" />)}
            {!listQuery.isLoading && notes.length === 0 && <div className="empty">لا توجد ملاحظات مطابقة ضمن نطاقك.</div>}
            {notes.map((note) => (
              <ObservationCard key={note.id} note={note} selected={selectedId === note.id} onSelect={() => selectNote(note.id)} />
            ))}
          </div>
          <div className="pagination compact-pagination">
            <button type="button" className="secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>السابق</button>
            <span className="muted">صفحة {page} من {totalPages}</span>
            <button type="button" className="secondary" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>التالي</button>
          </div>
        </aside>

        <main className="workspace-detail-pane" aria-live="polite">
          {!selectedId && <NoSelection />}
          {selectedId && detailQuery.isLoading && <div className="detail-skeleton" />}
          {selectedId && detailQuery.isError && (
            <div className="error" role="alert">
              <span>{detailQuery.error instanceof ApiError ? detailQuery.error.message : 'تعذر تحميل تفاصيل الملاحظة.'}</span>
              <button type="button" className="secondary" onClick={() => detailQuery.refetch()}>إعادة المحاولة</button>
            </div>
          )}
          {detailQuery.data && (
            <WorkspaceDetail
              key={detailQuery.data.note.id}
              data={detailQuery.data}
              activeTab={activeTab}
              onTabChange={setActiveTab}
              onBack={() => setSelectedId('')}
            />
          )}
        </main>
      </div>
    </div>
  )
}

function ObservationCard({ note, selected, onSelect }: Readonly<{ note: NoteListItem; selected: boolean; onSelect: () => void }>) {
  const locationLabel = noteLocationLabel(note)
  return (
    <button
      type="button"
      className={`observation-card ${selected ? 'selected' : ''} ${note.isOverdue ? 'overdue' : ''}`}
      onClick={onSelect}
      aria-pressed={selected}
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
  activeTab,
  onTabChange,
  onBack,
}: Readonly<{
  data: NoteWorkspaceDetail
  activeTab: (typeof TABS)[number][0]
  onTabChange: (tab: (typeof TABS)[number][0]) => void
  onBack: () => void
}>) {
  return (
    <article className="workspace-detail">
      <button type="button" className="secondary mobile-back" onClick={onBack}>رجوع إلى القائمة</button>
      <header className="workspace-detail-header">
        <div>
          <div className="observation-card-row">
            <span className="mono ref">{data.note.referenceNumber}</span>
            <span className="badge" data-tone={statusTone(data.note.status)}>{data.note.statusAr}</span>
            <span className="badge" data-tone={severityTone(data.note.severity)}>{data.note.severityAr}</span>
            {data.summary.currentBlockerAr && <span className="badge" data-tone="warn">{data.summary.currentBlockerAr}</span>}
          </div>
          <h2>{data.note.title}</h2>
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
      <ActionBar data={data} />
      <nav className="workspace-tabs" aria-label="تبويبات مساحة العمل">
        {TABS.map(([key, label]) => (
          <button key={key} type="button" className={activeTab === key ? 'active' : undefined} onClick={() => onTabChange(key)}>
            {label}
          </button>
        ))}
      </nav>
      <section className="workspace-tab-panel">
        {activeTab === 'summary' && <SummaryTab data={data} />}
        {activeTab === 'actions' && <ActionsTab data={data} />}
        {activeTab === 'assignments' && <AssignmentsTab data={data} />}
        {activeTab === 'resources' && <EmptyOperationalTab title="الموارد" text="طلبات الموارد وقطع الغيار ستستخدم نموذجًا مستقلاً في المرحلة التالية. لا يتم استخدام بيانات ثابتة هنا." />}
        {activeTab === 'verification' && <VerificationTab data={data} />}
        {activeTab === 'rca' && <ActionsTab data={data} />}
        {activeTab === 'attachments' && <AttachmentsTab data={data} />}
        {activeTab === 'links' && <EmptyOperationalTab title="الروابط" text="روابط الأصول والمخاطر والمشاريع والعقود والحوادث تحتاج كيانات ربط مخصصة قبل عرضها." />}
        {activeTab === 'decisions' && <EmptyOperationalTab title="القرارات" text="Decision Timeline موثق كامتداد لاحق بكيان مستقل حتى لا يختلط مع AuditLog." />}
        {activeTab === 'timeline' && <TimelineTab data={data} />}
      </section>
    </article>
  )
}

function ActionBar({ data }: Readonly<{ data: NoteWorkspaceDetail }>) {
  const queryClient = useQueryClient()
  const [reason, setReason] = useState('')
  const [activeAction, setActiveAction] = useState<NoteWorkspaceAllowedAction | ''>('')
  const runAction = useMutation({
    mutationFn: async (action: NoteWorkspaceAllowedAction) => {
      if (action === 'SUBMIT') return api.notes.submit(data.note.id, { reason, rowVersion: data.note.rowVersion })
      if (action === 'START_WORK') return api.notes.startWork(data.note.id, { reason, rowVersion: data.note.rowVersion })
      if (action === 'REQUEST_VERIFICATION') return api.notes.submitForVerification(data.note.id, { reason, rowVersion: data.note.rowVersion })
      if (action === 'REJECT_VERIFICATION') return api.notes.returnForRework(data.note.id, { reason, rowVersion: data.note.rowVersion })
      if (action === 'REOPEN') return api.notes.reopen(data.note.id, { reason, rowVersion: data.note.rowVersion })
      if (action === 'CANCEL') return api.notes.cancel(data.note.id, { reason, rowVersion: data.note.rowVersion })
      throw new Error('هذا الإجراء غير مدعوم كعملية فورية.')
    },
    onSuccess: async () => {
      setReason('')
      setActiveAction('')
      await queryClient.invalidateQueries({ queryKey: ['notes-workspace'] })
      await queryClient.invalidateQueries({ queryKey: ['notes-workspace-detail', data.note.id] })
    },
  })

  return (
    <div className="workspace-actionbar">
      {data.allowedActions.map((action) => {
        if (action === 'ADD_ACTION') return <Link key={action} to={`/notes/${data.note.id}/corrective-actions/new`}><button type="button" className="secondary">{ACTION_LABELS[action]}</button></Link>
        if (action === 'ASSIGN' || action === 'REASSIGN') return <Link key={action} to={`/notes/${data.note.id}`}><button type="button" className="secondary">{ACTION_LABELS[action]}</button></Link>
        if (INLINE_ACTIONS.has(action)) return <button key={action} type="button" className={activeAction === action ? undefined : 'secondary'} onClick={() => setActiveAction(action)}>{ACTION_LABELS[action]}</button>
        return <button key={action} type="button" className="secondary" disabled title="يتطلب نموذجًا مخصصًا">{ACTION_LABELS[action]}</button>
      })}
      {activeAction && (
        <form className="inline-action-form" onSubmit={(event) => { event.preventDefault(); runAction.mutate(activeAction) }}>
          <input aria-label="سبب الإجراء" value={reason} onChange={(event) => setReason(event.target.value)} placeholder="سبب الإجراء" />
          <button type="submit" disabled={reason.trim().length < 3 || runAction.isPending}>{runAction.isPending ? 'جاري…' : 'تنفيذ'}</button>
          <button type="button" className="secondary" onClick={() => setActiveAction('')}>إلغاء</button>
          {runAction.isError && <span className="field-error">{runAction.error instanceof Error ? runAction.error.message : 'تعذر تنفيذ الإجراء.'}</span>}
        </form>
      )}
    </div>
  )
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
      <Metric label="الإجراءات المفتوحة" value={String(data.summary.openCorrectiveActions)} />
    </div>
  )
}

function ActionsTab({ data }: Readonly<{ data: NoteWorkspaceDetail }>) {
  if (data.correctiveActions.items.length === 0) return <div className="empty">لا توجد إجراءات مستقلة مرتبطة بهذه الملاحظة.</div>
  return <div className="workspace-stack">{data.correctiveActions.items.map((action) => (
    <div key={action.id} className="workspace-row-card">
      <div><strong>{action.title}</strong><p className="muted">{action.descriptionSnippet || 'لا يوجد وصف مختصر.'}</p></div>
      <span className="badge" data-tone={action.isOverdue ? 'danger' : 'muted'}>{action.statusAr}</span>
      <span>{action.currentAssigneeDisplay || 'بلا مسؤول'}</span>
      <span>{action.dueAtUtc ? formatDate(action.dueAtUtc) : 'دون استحقاق'}</span>
    </div>
  ))}</div>
}

function AssignmentsTab({ data }: Readonly<{ data: NoteWorkspaceDetail }>) {
  if (data.assignments.length === 0) return <div className="empty">لا توجد تكليفات مسجلة.</div>
  return <div className="workspace-stack">{data.assignments.map((assignment) => (
    <div key={assignment.id} className="workspace-row-card">
      <div><strong>{assignment.assignedToUserDisplayName || assignment.assignedToDepartmentName}</strong><p className="muted">{assignment.reason}</p></div>
      <span>{assignment.isCurrent ? 'حالي' : 'سابق'}</span>
      <span>{assignment.acceptedAtUtc ? 'مقبول' : 'بانتظار القبول'}</span>
      <span>{formatDate(assignment.assignedAtUtc)}</span>
    </div>
  ))}</div>
}

function VerificationTab({ data }: Readonly<{ data: NoteWorkspaceDetail }>) {
  const verificationStatus = verificationStatusLabel(data)
  return (
    <div className="workspace-stack">
      <Metric label="حالة التحقق" value={verificationStatus} />
      <Metric label="فصل الواجبات" value={data.note.severity >= 3 ? 'مفعل للملاحظات الحرجة في الخادم' : 'حسب السياسة'} />
      <Metric label="ملخص الإغلاق" value={data.note.closureSummary || '—'} />
    </div>
  )
}

function AttachmentsTab({ data }: Readonly<{ data: NoteWorkspaceDetail }>) {
  if (data.attachments.length === 0) return <div className="empty">لا توجد مرفقات.</div>
  return <div className="workspace-stack">{data.attachments.map((attachment) => (
    <div key={attachment.id} className="workspace-row-card">
      <strong>{attachment.originalFileName}</strong>
      <span>{attachment.contentType}</span>
      <span>{Math.ceil(attachment.sizeBytes / 1024)} KB</span>
      <span>{formatDate(attachment.uploadedAtUtc)}</span>
    </div>
  ))}</div>
}

function TimelineTab({ data }: Readonly<{ data: NoteWorkspaceDetail }>) {
  return <ol className="workspace-timeline">{data.timeline.map((entry) => (
    <li key={`${entry.type}-${entry.id}`} data-tone={entry.tone}>
      <strong>{entry.titleAr}</strong>
      {entry.descriptionAr && <p>{entry.descriptionAr}</p>}
      <span>{entry.actorDisplayName || 'النظام'} · {formatDate(entry.occurredAtUtc)}</span>
    </li>
  ))}</ol>
}

function EmptyOperationalTab({ title, text }: Readonly<{ title: string; text: string }>) {
  return <div className="empty"><strong>{title}</strong><p>{text}</p></div>
}

function NoSelection() {
  return <div className="workspace-no-selection"><strong>اختر ملاحظة</strong><p className="muted">ستظهر الإجراءات والتكليفات والتحقق والـTimeline هنا دون مغادرة الصفحة.</p></div>
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
