import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import {
  api,
  ApiError,
  type Attachment,
  type NoteAssignment,
  type NoteDetail,
  type NoteStatusHistoryEntry,
} from '../../api/client'
import { useAuth, usePermission } from '../../auth/AuthProvider'
import {
  AttachmentScanStatusLabelsAr,
  ClassificationLevelLabelsAr,
  ScopeTypeLabelsAr,
  severityTone,
  statusTone,
} from '../../notes/noteEnums'
import { type NoteActionDef, getAllowedActions } from '../../notes/noteWorkflow'

function formatDate(value?: string | null): string {
  if (!value) return '—'
  return new Date(value).toLocaleString('ar-SA')
}

function ScopeLabel({ note }: Readonly<{ note: NoteDetail }>) {
  const regionQuery = useQuery({
    queryKey: ['region', note.regionId],
    queryFn: () => api.regions().then((r) => r.items.find((x) => x.id === note.regionId)),
    enabled: !!note.regionId,
  })
  const facilityQuery = useQuery({
    queryKey: ['facility', note.facilityId],
    queryFn: () => api.facilities().then((r) => r.items.find((x) => x.id === note.facilityId)),
    enabled: !!note.facilityId,
  })

  const parts = [ScopeTypeLabelsAr[note.scopeType] ?? note.scopeType]
  if (regionQuery.data) parts.push(regionQuery.data.nameAr)
  else if (note.regionId) parts.push(`منطقة (${note.regionId.slice(0, 8)}…)`)
  if (facilityQuery.data) parts.push(facilityQuery.data.nameAr)
  else if (note.facilityId) parts.push(`سجن (${note.facilityId.slice(0, 8)}…)`)
  if (note.facilityUnitId) parts.push(`وحدة (${note.facilityUnitId.slice(0, 8)}…)`)

  return <span>{parts.join(' — ')}</span>
}

function AssignActionFields({
  reason,
  setReason,
  assignedToUserId,
  setAssignedToUserId,
  assignedToDepartmentId,
  setAssignedToDepartmentId,
  dueAtUtc,
  setDueAtUtc,
}: Readonly<{
  reason: string
  setReason: (v: string) => void
  assignedToUserId: string
  setAssignedToUserId: (v: string) => void
  assignedToDepartmentId: string
  setAssignedToDepartmentId: (v: string) => void
  dueAtUtc: string
  setDueAtUtc: (v: string) => void
}>) {
  const [userSearch, setUserSearch] = useState('')
  const usersQuery = useQuery({
    queryKey: ['assign-users', userSearch],
    queryFn: () => api.users(userSearch),
    enabled: userSearch.length > 0,
  })

  return (
    <div className="form-grid">
      <label className="field">
        <span>بحث مستخدم للتكليف</span>
        <input aria-label="بحث مستخدم للتكليف" value={userSearch} onChange={(e) => setUserSearch(e.target.value)} placeholder="اسم المستخدم" />
      </label>
      <label className="field">
        <span>المستخدم المكلَّف</span>
        <select
          aria-label="المستخدم المكلَّف"
          value={assignedToUserId}
          onChange={(e) => {
            setAssignedToUserId(e.target.value)
            if (e.target.value) setAssignedToDepartmentId('')
          }}
        >
          <option value="">بدون تحديد مستخدم</option>
          {usersQuery.data?.items.map((u) => (
            <option key={u.id} value={u.id}>{u.displayNameAr}</option>
          ))}
        </select>
      </label>
      <label className="field">
        <span>أو معرف الإدارة المكلَّفة (UUID)</span>
        <input
          aria-label="معرف الإدارة المكلَّفة"
          value={assignedToDepartmentId}
          onChange={(e) => {
            setAssignedToDepartmentId(e.target.value)
            if (e.target.value) setAssignedToUserId('')
          }}
        />
      </label>
      <label className="field">
        <span>تاريخ استحقاق التكليف</span>
        <input aria-label="تاريخ استحقاق التكليف" type="datetime-local" value={dueAtUtc} onChange={(e) => setDueAtUtc(e.target.value)} />
      </label>
      <label className="field field-wide">
        <span>سبب التكليف *</span>
        <textarea aria-label="سبب التكليف" rows={2} value={reason} onChange={(e) => setReason(e.target.value)} />
      </label>
    </div>
  )
}

function validateActionInputs(
  action: NoteActionDef,
  reason: string,
  closureSummary: string,
  assignedToUserId: string,
  assignedToDepartmentId: string,
): string | null {
  if (action.requiresReason && !reason.trim()) return 'السبب مطلوب.'
  if (action.requiresClosureSummary && !closureSummary.trim()) return 'ملخص الإغلاق مطلوب.'
  if (action.isAssign && !assignedToUserId && !assignedToDepartmentId) {
    return 'يجب تحديد مستخدم أو إدارة واحدة فقط للتكليف.'
  }
  if (action.isAssign && assignedToUserId && assignedToDepartmentId) {
    return 'يجب تحديد مستخدم أو إدارة واحدة فقط للتكليف، لا كليهما.'
  }
  return null
}

type ActionFields = {
  reason: string
  closureSummary: string
  assignedToUserId: string
  assignedToDepartmentId: string
  dueAtUtc: string
}

async function performNoteAction(note: NoteDetail, action: NoteActionDef, fields: ActionFields): Promise<void> {
  const { reason, closureSummary, assignedToUserId, assignedToDepartmentId, dueAtUtc } = fields
  const rowVersion = note.rowVersion
  const isoDue = dueAtUtc ? new Date(dueAtUtc).toISOString() : undefined
  switch (action.kind) {
    case 'submit':
      await api.notes.submit(note.id, { reason, rowVersion })
      break
    case 'cancel':
      await api.notes.cancel(note.id, { reason, rowVersion })
      break
    case 'assign':
    case 'reassign':
      await api.notes.assign(note.id, {
        assignedToUserId: assignedToUserId || null,
        assignedToDepartmentId: assignedToDepartmentId || null,
        dueAtUtc: isoDue ?? null,
        reason,
        rowVersion,
      })
      break
    case 'startWork':
      await api.notes.startWork(note.id, { reason: reason || null, rowVersion })
      break
    case 'submitForVerification':
      await api.notes.submitForVerification(note.id, { reason: reason || null, rowVersion })
      break
    case 'returnForRework':
      await api.notes.returnForRework(note.id, { reason, rowVersion })
      break
    case 'verifyClosure':
      await api.notes.verifyClosure(note.id, { reason, closureSummary, rowVersion })
      break
    case 'reopen':
      await api.notes.reopen(note.id, { reason, rowVersion })
      break
    default:
      break
  }
}

function describeActionError(err: unknown): { message: string; conflict: boolean } {
  if (err instanceof ApiError) {
    if (err.status === 409) {
      return { message: 'تم تغيير الملاحظة بواسطة مستخدم آخر. أعد تحميل الصفحة قبل المحاولة مرة أخرى.', conflict: true }
    }
    if (err.status === 403) return { message: 'ليست لديك صلاحية تنفيذ هذا الإجراء.', conflict: false }
    if (err.status === 404) return { message: 'الملاحظة غير موجودة أو خارج نطاقك.', conflict: false }
    return { message: err.message, conflict: false }
  }
  return { message: 'تعذر تنفيذ الإجراء.', conflict: false }
}

function ActionPanel({ note, action, onDone }: Readonly<{ note: NoteDetail; action: NoteActionDef; onDone: () => void }>) {
  const [reason, setReason] = useState('')
  const [closureSummary, setClosureSummary] = useState('')
  const [assignedToUserId, setAssignedToUserId] = useState('')
  const [assignedToDepartmentId, setAssignedToDepartmentId] = useState('')
  const [dueAtUtc, setDueAtUtc] = useState('')
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [conflict, setConflict] = useState(false)

  const run = async () => {
    setError(null)
    setConflict(false)
    const validationError = validateActionInputs(action, reason, closureSummary, assignedToUserId, assignedToDepartmentId)
    if (validationError) {
      setError(validationError)
      return
    }

    setPending(true)
    try {
      await performNoteAction(note, action, { reason, closureSummary, assignedToUserId, assignedToDepartmentId, dueAtUtc })
      onDone()
    } catch (err) {
      const { message, conflict: isConflict } = describeActionError(err)
      setConflict(isConflict)
      setError(message)
    } finally {
      setPending(false)
    }
  }

  return (
    <div className="panel-section action-panel">
      <h3 className="section-title">{action.labelAr}</h3>
      {action.isAssign && (
        <AssignActionFields
          reason={reason}
          setReason={setReason}
          assignedToUserId={assignedToUserId}
          setAssignedToUserId={setAssignedToUserId}
          assignedToDepartmentId={assignedToDepartmentId}
          setAssignedToDepartmentId={setAssignedToDepartmentId}
          dueAtUtc={dueAtUtc}
          setDueAtUtc={setDueAtUtc}
        />
      )}
      {!action.isAssign && action.requiresReason && (
        <label className="field field-wide">
          <span>السبب *</span>
          <textarea aria-label={`سبب ${action.labelAr}`} rows={2} value={reason} onChange={(e) => setReason(e.target.value)} />
        </label>
      )}
      {!action.isAssign && !action.requiresReason && (
        <label className="field field-wide">
          <span>ملاحظة (اختياري)</span>
          <textarea aria-label={`ملاحظة ${action.labelAr}`} rows={2} value={reason} onChange={(e) => setReason(e.target.value)} />
        </label>
      )}
      {action.requiresClosureSummary && (
        <label className="field field-wide">
          <span>ملخص الإغلاق *</span>
          <textarea aria-label="ملخص الإغلاق" rows={2} value={closureSummary} onChange={(e) => setClosureSummary(e.target.value)} />
        </label>
      )}

      {error && (
        <div className="error" role="alert">
          <span>{error}</span>
        </div>
      )}

      <div className="form-actions">
        <button type="button" disabled={pending} onClick={run}>
          {pending ? 'جارٍ التنفيذ…' : 'تأكيد'}
        </button>
        {conflict && (
          <button type="button" className="secondary" onClick={onDone}>
            إعادة تحميل
          </button>
        )}
      </div>
    </div>
  )
}

function ArchivePanel({ note, onDone }: Readonly<{ note: NoteDetail; onDone: () => void }>) {
  const [reason, setReason] = useState('')
  const [pending, setPending] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [isError, setIsError] = useState(false)

  return (
    <div className="panel-section action-panel">
      <h3 className="section-title">أرشفة الملاحظة</h3>
      <p className="muted">
        بعد الأرشفة، ستختفي الملاحظة من القائمة والبحث. احفظ نسخة السجل (RowVersion) التالية لاستعادتها لاحقًا: <code>{note.rowVersion}</code>
      </p>
      <label className="field field-wide">
        <span>سبب الأرشفة *</span>
        <textarea aria-label="سبب الأرشفة" rows={2} value={reason} onChange={(e) => setReason(e.target.value)} />
      </label>
      {message && <div className={isError ? 'error' : 'muted'} role={isError ? 'alert' : 'status'}>{message}</div>}
      <div className="form-actions">
        <button
          type="button"
          className="secondary"
          disabled={pending || !reason.trim()}
          onClick={async () => {
            setPending(true)
            setMessage(null)
            try {
              await api.notes.archive(note.id, { reason, rowVersion: note.rowVersion })
              setIsError(false)
              setMessage('تمت الأرشفة بنجاح.')
              onDone()
            } catch (err) {
              setIsError(true)
              setMessage(err instanceof Error ? err.message : 'تعذرت الأرشفة.')
            } finally {
              setPending(false)
            }
          }}
        >
          {pending ? 'جارٍ الأرشفة…' : 'أرشفة'}
        </button>
      </div>
    </div>
  )
}

function AttachmentsPanel({ noteId }: Readonly<{ noteId: string }>) {
  const canUpload = usePermission('Attachments.Upload')
  const queryClient = useQueryClient()
  const [reason, setReason] = useState('مستند متعلق بالملاحظة')
  const [file, setFile] = useState<File | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [uploading, setUploading] = useState(false)

  const attachmentsQuery = useQuery({
    queryKey: ['note-attachments', noteId],
    queryFn: () => api.notes.attachments(noteId),
  })

  const download = async (attachment: Attachment) => {
    try {
      const { blob, fileName } = await api.downloadAttachment(attachment.id)
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = fileName || attachment.originalFileName
      document.body.appendChild(link)
      link.click()
      link.remove()
      URL.revokeObjectURL(url)
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'تعذر تنزيل المرفق.')
    }
  }

  return (
    <div className="panel-section">
      <h2 className="section-title">المرفقات</h2>
      <p className="muted">تنزيل المرفقات مسموح فقط بعد اكتمال فحص الملفات بنجاح (سليم).</p>

      {canUpload && (
        <div className="toolbar">
          <input aria-label="سبب الرفع" value={reason} onChange={(e) => setReason(e.target.value)} placeholder="سبب الرفع" />
          <input aria-label="ملف المرفق" type="file" onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
          <button
            type="button"
            disabled={uploading || !file}
            onClick={async () => {
              if (!file) return
              setUploading(true)
              setMessage(null)
              try {
                await api.uploadAttachment(file, 'OperationalNote', noteId, reason)
                setFile(null)
                setMessage('تم رفع المرفق بنجاح.')
                await queryClient.invalidateQueries({ queryKey: ['note-attachments', noteId] })
              } catch (err) {
                setMessage(err instanceof Error ? err.message : 'تعذر رفع المرفق.')
              } finally {
                setUploading(false)
              }
            }}
          >
            {uploading ? 'جارٍ الرفع…' : 'رفع'}
          </button>
        </div>
      )}

      {message && <output className="muted">{message}</output>}

      {attachmentsQuery.isLoading && <div className="loading">جاري التحميل…</div>}
      {attachmentsQuery.data?.length === 0 && <div className="empty">لا توجد مرفقات لهذه الملاحظة.</div>}
      {attachmentsQuery.data && attachmentsQuery.data.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>الملف</th>
              <th>حالة الفحص</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {attachmentsQuery.data.map((a) => (
              <tr key={a.id}>
                <td>{a.originalFileName}</td>
                <td>
                  <span className="badge" data-tone={a.scanStatus === 1 ? 'ok' : 'muted'}>
                    {AttachmentScanStatusLabelsAr[a.scanStatus] ?? a.scanStatus}
                  </span>
                </td>
                <td>
                  {a.scanStatus === 1 ? (
                    <button type="button" className="secondary" onClick={() => download(a)}>تنزيل</button>
                  ) : (
                    <span className="muted">التنزيل متاح بعد اكتمال الفحص فقط</span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}

function describeNoteLoadError(err: ApiError): string {
  if (err.status === 403) return 'ليست لديك صلاحية عرض هذه الملاحظة.'
  if (err.status === 404) return 'الملاحظة غير موجودة أو خارج نطاقك.'
  return err.message || 'تعذر تحميل الملاحظة.'
}

function NoteSummaryGrid({ note }: Readonly<{ note: NoteDetail }>) {
  return (
    <div className="detail-grid">
      <div><span className="muted">الحالة</span><div><span className="badge" data-tone={statusTone(note.status)}>{note.statusAr}</span></div></div>
      <div><span className="muted">مستوى الخطورة</span><div><span className="badge" data-tone={severityTone(note.severity)}>{note.severityAr}</span></div></div>
      <div><span className="muted">التصنيف</span><div>{note.categoryAr}</div></div>
      <div><span className="muted">مستوى التصنيف الأمني</span><div>{ClassificationLevelLabelsAr[note.classification] ?? note.classification}</div></div>
      <div><span className="muted">المصدر</span><div>{note.sourceAr}{note.sourceReference ? ` — ${note.sourceReference}` : ''}</div></div>
      <div><span className="muted">النطاق</span><div><ScopeLabel note={note} /></div></div>
      <div><span className="muted">الإدارة المسؤولة</span><div>{note.ownerDepartmentId ? note.ownerDepartmentId : '—'}</div></div>
      <div><span className="muted">المُبلِّغ</span><div>{note.reportedByDisplayName || '—'}</div></div>
      <div><span className="muted">تاريخ الإبلاغ</span><div>{formatDate(note.reportedAtUtc)}</div></div>
      <div>
        <span className="muted">تاريخ الاستحقاق</span>
        <div>
          {formatDate(note.dueAtUtc)}
          {note.isOverdue && <span className="badge" data-tone="danger" style={{ marginRight: '0.35rem' }}>متأخرة</span>}
        </div>
      </div>
      <div><span className="muted">بدء العمل</span><div>{formatDate(note.workStartedAtUtc)}</div></div>
      <div><span className="muted">أُرسلت للتحقق</span><div>{formatDate(note.submittedForVerificationAtUtc)}</div></div>
      <div><span className="muted">تاريخ الإغلاق</span><div>{formatDate(note.closedAtUtc)}</div></div>
      <div><span className="muted">تاريخ إعادة الفتح</span><div>{formatDate(note.reopenedAtUtc)}</div></div>
    </div>
  )
}

function NoteDescriptionSection({ note }: Readonly<{ note: NoteDetail }>) {
  return (
    <div className="panel-section">
      <h2 className="section-title">الوصف</h2>
      <p>{note.description}</p>
      {note.closureSummary && (
        <>
          <h3 className="section-title">ملخص الإغلاق</h3>
          <p>{note.closureSummary}</p>
        </>
      )}
      {note.reopenReason && (
        <>
          <h3 className="section-title">سبب إعادة الفتح</h3>
          <p>{note.reopenReason}</p>
        </>
      )}
    </div>
  )
}

function CurrentAssignmentSection({ note }: Readonly<{ note: NoteDetail }>) {
  return (
    <div className="panel-section">
      <h2 className="section-title">التكليف الحالي</h2>
      {note.currentAssignment ? (
        <div className="detail-grid">
          <div><span className="muted">المكلَّف</span><div>{note.currentAssignment.assignedToUserDisplayName || note.currentAssignment.assignedToDepartmentName || '—'}</div></div>
          <div><span className="muted">بواسطة</span><div>{note.currentAssignment.assignedByDisplayName || '—'}</div></div>
          <div><span className="muted">تاريخ التكليف</span><div>{formatDate(note.currentAssignment.assignedAtUtc)}</div></div>
          <div><span className="muted">تاريخ الاستحقاق</span><div>{formatDate(note.currentAssignment.dueAtUtc)}</div></div>
          <div><span className="muted">السبب</span><div>{note.currentAssignment.reason}</div></div>
        </div>
      ) : (
        <div className="empty">لا يوجد تكليف حالي.</div>
      )}
    </div>
  )
}

function AssignmentsHistorySection({
  assignments,
  isLoading,
}: Readonly<{ assignments?: NoteAssignment[]; isLoading: boolean }>) {
  return (
    <div className="panel-section">
      <h2 className="section-title">سجل التكليفات</h2>
      {isLoading && <div className="loading">جاري التحميل…</div>}
      {assignments?.length === 0 && <div className="empty">لا توجد تكليفات سابقة.</div>}
      {assignments && assignments.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>المكلَّف</th>
              <th>بواسطة</th>
              <th>تاريخ التكليف</th>
              <th>انتهى</th>
              <th>حالي</th>
            </tr>
          </thead>
          <tbody>
            {assignments.map((a) => (
              <tr key={a.id}>
                <td>{a.assignedToUserDisplayName || a.assignedToDepartmentName || '—'}</td>
                <td>{a.assignedByDisplayName || '—'}</td>
                <td>{formatDate(a.assignedAtUtc)}</td>
                <td>{formatDate(a.endedAtUtc)}</td>
                <td>{a.isCurrent ? <span className="badge" data-tone="ok">حالي</span> : '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}

function StatusTimelineSection({
  history,
  isLoading,
}: Readonly<{ history?: NoteStatusHistoryEntry[]; isLoading: boolean }>) {
  return (
    <div className="panel-section">
      <h2 className="section-title">الخط الزمني للحالة</h2>
      {isLoading && <div className="loading">جاري التحميل…</div>}
      {history?.length === 0 && <div className="empty">لا توجد أحداث بعد.</div>}
      {history && history.length > 0 && (
        <ul className="timeline">
          {history.map((h) => (
            <li key={h.id}>
              <span className="badge" data-tone={statusTone(h.toStatus)}>{h.toStatusAr}</span>
              <span className="muted"> — {formatDate(h.changedAtUtc)} — {h.changedByDisplayName || '—'}</span>
              {h.reason && <div className="muted">{h.reason}</div>}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

function NoteActionsSection({
  note,
  allowedActions,
  canArchive,
  selectedAction,
  showArchive,
  onSelectAction,
  onShowArchive,
  onDone,
}: Readonly<{
  note: NoteDetail
  allowedActions: NoteActionDef[]
  canArchive: boolean
  selectedAction: NoteActionDef | null
  showArchive: boolean
  onSelectAction: (action: NoteActionDef) => void
  onShowArchive: () => void
  onDone: () => void
}>) {
  if (allowedActions.length === 0 && !canArchive) {
    return null
  }

  return (
    <div className="panel-section">
      <h2 className="section-title">الإجراءات المتاحة</h2>
      <div className="toolbar">
        {allowedActions.map((action) => (
          <button key={action.kind} type="button" className="secondary" onClick={() => onSelectAction(action)}>
            {action.labelAr}
          </button>
        ))}
        {canArchive && (
          <button type="button" className="secondary" onClick={onShowArchive}>
            أرشفة
          </button>
        )}
      </div>

      {selectedAction && <ActionPanel note={note} action={selectedAction} onDone={onDone} />}
      {showArchive && <ArchivePanel note={note} onDone={onDone} />}
    </div>
  )
}

export function NoteDetailPage() {
  const canView = usePermission('Notes.View')
  const canUpdate = usePermission('Notes.Update')
  const canArchive = usePermission('Notes.Archive')
  const { hasPermission } = useAuth()
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [selectedAction, setSelectedAction] = useState<NoteActionDef | null>(null)
  const [showArchive, setShowArchive] = useState(false)

  const noteQuery = useQuery({
    queryKey: ['note', id],
    queryFn: () => api.notes.get(id!),
    enabled: canView && !!id,
  })

  const historyQuery = useQuery({
    queryKey: ['note-history', id],
    queryFn: () => api.notes.history(id!),
    enabled: canView && !!id && !!noteQuery.data,
  })

  const assignmentsQuery = useQuery({
    queryKey: ['note-assignments', id],
    queryFn: () => api.notes.assignments(id!),
    enabled: canView && !!id && !!noteQuery.data,
  })

  if (!canView) {
    return <div className="error" role="alert">ليست لديك صلاحية عرض الملاحظات.</div>
  }

  if (noteQuery.isLoading) {
    return <div className="loading">جاري التحميل…</div>
  }

  if (noteQuery.isError) {
    const message = describeNoteLoadError(noteQuery.error as ApiError)
    return (
      <div className="error" role="alert">
        <span>{message}</span>
        <button type="button" className="secondary" onClick={() => noteQuery.refetch()}>إعادة المحاولة</button>
      </div>
    )
  }

  const note = noteQuery.data
  if (!note) {
    return <div className="empty">الملاحظة غير موجودة.</div>
  }

  const refreshAll = async () => {
    setSelectedAction(null)
    setShowArchive(false)
    await queryClient.invalidateQueries({ queryKey: ['note', id] })
    await queryClient.invalidateQueries({ queryKey: ['note-history', id] })
    await queryClient.invalidateQueries({ queryKey: ['note-assignments', id] })
  }

  const allowedActions = getAllowedActions(note.status, !!note.currentAssignment).filter((a) => hasPermission(a.permission))
  const canEdit = canUpdate && note.status !== 5 && note.status !== 7

  return (
    <div className="panel">
      <div className="page-header">
        <div>
          <h1 className="page-title">{note.referenceNumber}</h1>
          <p className="muted">{note.title}</p>
        </div>
        <div style={{ display: 'flex', gap: '0.5rem' }}>
          {canEdit && <Link to={`/notes/${note.id}/edit`}><button type="button" className="secondary">تعديل</button></Link>}
          <button type="button" className="secondary" onClick={() => navigate('/notes')}>رجوع للقائمة</button>
        </div>
      </div>

      {note.isSensitiveRedacted && (
        <div className="error" role="alert">هذا المحتوى محجوب لأنه يتطلب صلاحية Notes.ViewSensitive.</div>
      )}

      <NoteSummaryGrid note={note} />
      <NoteDescriptionSection note={note} />
      <CurrentAssignmentSection note={note} />
      <AssignmentsHistorySection assignments={assignmentsQuery.data} isLoading={assignmentsQuery.isLoading} />
      <StatusTimelineSection history={historyQuery.data} isLoading={historyQuery.isLoading} />
      <NoteActionsSection
        note={note}
        allowedActions={allowedActions}
        canArchive={canArchive}
        selectedAction={selectedAction}
        showArchive={showArchive}
        onSelectAction={(action) => { setSelectedAction(action); setShowArchive(false) }}
        onShowArchive={() => { setShowArchive(true); setSelectedAction(null) }}
        onDone={refreshAll}
      />

      <AttachmentsPanel noteId={note.id} />
    </div>
  )
}
