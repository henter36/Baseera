import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api, ApiError, type Attachment, type CorrectiveActionDetail } from '../../api/client'
import { useAuth, usePermission } from '../../auth/AuthProvider'
import {
  CorrectiveActionStatus,
  correctiveActionPriorityTone,
  correctiveActionStatusTone,
} from '../../correctiveActions/correctiveActionEnums'
import { ClassificationLevelLabelsAr, AttachmentScanStatusLabelsAr } from '../../notes/noteEnums'

type ActionKind =
  | 'submit'
  | 'assign'
  | 'startWork'
  | 'submitForVerification'
  | 'returnForRework'
  | 'verifyCompletion'
  | 'reopen'
  | 'cancel'
  | 'archive'
  | 'restore'

type ActionDef = {
  kind: ActionKind
  labelAr: string
  permission: string
  needsCompletion?: boolean
  needsAssignee?: boolean
}

const ACTIONS: ActionDef[] = [
  { kind: 'submit', labelAr: 'فتح الإجراء', permission: 'CorrectiveActions.Create' },
  { kind: 'assign', labelAr: 'تكليف / إعادة تكليف', permission: 'CorrectiveActions.Assign', needsAssignee: true },
  { kind: 'startWork', labelAr: 'بدء المعالجة', permission: 'CorrectiveActions.StartWork' },
  { kind: 'submitForVerification', labelAr: 'إرسال للتحقق', permission: 'CorrectiveActions.SubmitForVerification', needsCompletion: true },
  { kind: 'returnForRework', labelAr: 'إعادة للمعالجة', permission: 'CorrectiveActions.ReturnForRework' },
  { kind: 'verifyCompletion', labelAr: 'اعتماد الإنجاز', permission: 'CorrectiveActions.VerifyCompletion', needsCompletion: true },
  { kind: 'reopen', labelAr: 'إعادة فتح', permission: 'CorrectiveActions.Reopen' },
  { kind: 'cancel', labelAr: 'إلغاء', permission: 'CorrectiveActions.Cancel' },
  { kind: 'archive', labelAr: 'أرشفة', permission: 'CorrectiveActions.Archive' },
  { kind: 'restore', labelAr: 'استعادة', permission: 'CorrectiveActions.Restore' },
]

function formatDate(value?: string | null): string {
  if (!value) return '—'
  return new Date(value).toLocaleString('ar-SA', { timeZone: 'Asia/Riyadh' })
}

function allowedKinds(status: number): ActionKind[] {
  switch (status) {
    case CorrectiveActionStatus.Draft:
      return ['submit', 'cancel', 'archive']
    case CorrectiveActionStatus.Open:
      return ['assign', 'cancel', 'archive']
    case CorrectiveActionStatus.Assigned:
      return ['assign', 'startWork', 'cancel', 'archive']
    case CorrectiveActionStatus.InProgress:
      return ['submitForVerification', 'cancel', 'archive']
    case CorrectiveActionStatus.PendingVerification:
      return ['verifyCompletion', 'returnForRework', 'cancel', 'archive']
    case CorrectiveActionStatus.Completed:
      return ['reopen', 'archive']
    case CorrectiveActionStatus.Reopened:
      return ['assign', 'startWork', 'cancel', 'archive']
    default:
      return ['restore']
  }
}

function describeLoadError(err: ApiError): string {
  if (err.status === 403) return 'ليست لديك صلاحية عرض هذا الإجراء.'
  if (err.status === 404) return 'الإجراء غير موجود أو خارج نطاقك.'
  return err.message || 'تعذر تحميل الإجراء.'
}

type ActionInput = {
  reason: string
  completionSummary: string
  assignedToUserId: string
  assignedToDepartmentId: string
  dueAtUtc: string
}

function validateActionInput(action: ActionDef, input: ActionInput): string | null {
  if (!input.reason.trim()) return 'السبب مطلوب.'
  if (action.needsCompletion && !input.completionSummary.trim()) return 'ملخص الإنجاز مطلوب.'
  if (!action.needsAssignee) return null
  if (!input.assignedToUserId && !input.assignedToDepartmentId) return 'يجب تحديد مستخدم أو إدارة واحدة فقط.'
  if (input.assignedToUserId && input.assignedToDepartmentId) return 'لا يمكن تحديد مستخدم وإدارة معًا.'
  return null
}

async function executeCorrectiveAction(action: ActionDef, item: CorrectiveActionDetail, input: ActionInput): Promise<void> {
  const base = { reason: input.reason, rowVersion: item.rowVersion }
  switch (action.kind) {
    case 'submit':
      await api.correctiveActions.submit(item.id, base)
      return
    case 'assign':
      await api.correctiveActions.assign(item.id, {
        assignedToUserId: input.assignedToUserId || null,
        assignedToDepartmentId: input.assignedToDepartmentId || null,
        dueAtUtc: input.dueAtUtc ? new Date(input.dueAtUtc).toISOString() : null,
        ...base,
      })
      return
    case 'startWork':
      await api.correctiveActions.startWork(item.id, base)
      return
    case 'submitForVerification':
      await api.correctiveActions.submitForVerification(item.id, { ...base, completionSummary: input.completionSummary })
      return
    case 'returnForRework':
      await api.correctiveActions.returnForRework(item.id, base)
      return
    case 'verifyCompletion':
      await api.correctiveActions.verifyCompletion(item.id, { ...base, completionSummary: input.completionSummary })
      return
    case 'reopen':
      await api.correctiveActions.reopen(item.id, base)
      return
    case 'cancel':
      await api.correctiveActions.cancel(item.id, base)
      return
    case 'archive':
      await api.correctiveActions.archive(item.id, base)
      return
    case 'restore':
      await api.correctiveActions.restore(item.id, base)
      return
  }
}

function describeTransitionError(err: unknown): { message: string; conflict: boolean } {
  if (err instanceof ApiError && err.status === 409) {
    return { message: 'حدث تعارض في RowVersion أو انتقال غير صالح. أعد تحميل البيانات.', conflict: true }
  }
  if (err instanceof ApiError && err.status === 404) return { message: 'الإجراء غير موجود أو خارج نطاقك.', conflict: false }
  if (err instanceof ApiError && err.status === 403) return { message: 'ليست لديك صلاحية تنفيذ هذا الانتقال.', conflict: false }
  return { message: err instanceof Error ? err.message : 'تعذر تنفيذ الانتقال.', conflict: false }
}

function ActionPanel({ action, item, onDone }: Readonly<{ action: ActionDef; item: CorrectiveActionDetail; onDone: () => void }>) {
  const [reason, setReason] = useState('')
  const [completionSummary, setCompletionSummary] = useState('')
  const [assignedToUserId, setAssignedToUserId] = useState('')
  const [assignedToDepartmentId, setAssignedToDepartmentId] = useState('')
  const [dueAtUtc, setDueAtUtc] = useState('')
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [conflict, setConflict] = useState(false)

  const run = async () => {
    setError(null)
    setConflict(false)

    const input = { reason, completionSummary, assignedToUserId, assignedToDepartmentId, dueAtUtc }
    const validationError = validateActionInput(action, input)
    if (validationError) {
      setError(validationError)
      return
    }

    setPending(true)
    try {
      await executeCorrectiveAction(action, item, input)
      onDone()
    } catch (err) {
      const result = describeTransitionError(err)
      setConflict(result.conflict)
      setError(result.message)
    } finally {
      setPending(false)
    }
  }

  return (
    <div className="panel-section action-panel">
      <h3 className="section-title">{action.labelAr}</h3>
      {action.needsAssignee && (
        <div className="form-grid">
          <label className="field">
            <span>معرف المستخدم</span>
            <input aria-label="معرف المستخدم المكلّف" value={assignedToUserId} onChange={(e) => { setAssignedToUserId(e.target.value); if (e.target.value) setAssignedToDepartmentId('') }} />
          </label>
          <label className="field">
            <span>معرف الإدارة</span>
            <input aria-label="معرف الإدارة المكلّفة" value={assignedToDepartmentId} onChange={(e) => { setAssignedToDepartmentId(e.target.value); if (e.target.value) setAssignedToUserId('') }} />
          </label>
          <label className="field">
            <span>تاريخ استحقاق التكليف</span>
            <input aria-label="تاريخ استحقاق التكليف" type="datetime-local" value={dueAtUtc} onChange={(e) => setDueAtUtc(e.target.value)} />
          </label>
        </div>
      )}
      <label className="field field-wide">
        <span>السبب *</span>
        <textarea aria-label={`سبب ${action.labelAr}`} rows={2} value={reason} onChange={(e) => setReason(e.target.value)} />
      </label>
      {action.needsCompletion && (
        <label className="field field-wide">
          <span>ملخص الإنجاز *</span>
          <textarea aria-label="ملخص الإنجاز" rows={3} value={completionSummary} onChange={(e) => setCompletionSummary(e.target.value)} />
        </label>
      )}
      {error && <div className="error" role="alert">{error}</div>}
      <div className="form-actions">
        <button type="button" disabled={pending} onClick={run}>{pending ? 'جارٍ التنفيذ…' : 'تأكيد'}</button>
        {conflict && <button type="button" className="secondary" onClick={onDone}>إعادة تحميل</button>}
      </div>
    </div>
  )
}

function AttachmentsPanel({ actionId }: Readonly<{ actionId: string }>) {
  const query = useQuery({ queryKey: ['corrective-action-attachments', actionId], queryFn: () => api.correctiveActions.attachments(actionId) })
  const [message, setMessage] = useState<string | null>(null)

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
      {message && <div className="error" role="alert">{message}</div>}
      {query.isLoading && <div className="loading">جاري تحميل المرفقات…</div>}
      {query.data?.length === 0 && <div className="empty">لا توجد مرفقات لهذا الإجراء.</div>}
      {query.data && query.data.length > 0 && (
        <table>
          <thead><tr><th>الملف</th><th>الحجم</th><th>حالة الفحص</th><th></th></tr></thead>
          <tbody>
            {query.data.map((a) => (
              <tr key={a.id}>
                <td>{a.originalFileName}</td>
                <td>{a.sizeBytes}</td>
                <td><span className="badge" data-tone={a.scanStatus === 1 ? 'ok' : 'warn'}>{AttachmentScanStatusLabelsAr[a.scanStatus] ?? a.scanStatus}</span></td>
                <td>
                  {a.scanStatus === 1 && !a.isSensitiveRedacted ? (
                    <button type="button" className="secondary" onClick={() => download(a)}>تنزيل</button>
                  ) : (
                    <span className="muted">لا يمكن تنزيل المرفق قبل حالة الفحص السليمة.</span>
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

export function CorrectiveActionDetailPage() {
  const canView = usePermission('CorrectiveActions.View')
  const canUpdate = usePermission('CorrectiveActions.Update')
  const { hasPermission } = useAuth()
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [selectedAction, setSelectedAction] = useState<ActionDef | null>(null)

  const query = useQuery({ queryKey: ['corrective-action', id], queryFn: () => api.correctiveActions.get(id!), enabled: canView && !!id })
  const historyQuery = useQuery({ queryKey: ['corrective-action-history', id], queryFn: () => api.correctiveActions.history(id!), enabled: canView && !!id && !!query.data })
  const assignmentsQuery = useQuery({ queryKey: ['corrective-action-assignments', id], queryFn: () => api.correctiveActions.assignments(id!), enabled: canView && !!id && !!query.data })

  if (!canView) return <div className="error" role="alert">ليست لديك صلاحية عرض الإجراءات التصحيحية.</div>
  if (query.isLoading) return <div className="loading">جاري التحميل…</div>
  if (query.isError) return (
    <div className="error" role="alert">
      <span>{describeLoadError(query.error as ApiError)}</span>
      <button type="button" className="secondary" onClick={() => query.refetch()}>إعادة المحاولة</button>
    </div>
  )
  if (!query.data) return <div className="empty">الإجراء غير موجود.</div>

  const item = query.data
  const actions = ACTIONS
    .filter((a) => allowedKinds(item.status).includes(a.kind))
    .filter((a) => hasPermission(a.permission))
  const canEdit = canUpdate && item.status !== CorrectiveActionStatus.Completed && item.status !== CorrectiveActionStatus.Cancelled
  const refreshAll = async () => {
    setSelectedAction(null)
    await queryClient.invalidateQueries({ queryKey: ['corrective-action', id] })
    await queryClient.invalidateQueries({ queryKey: ['corrective-action-history', id] })
    await queryClient.invalidateQueries({ queryKey: ['corrective-action-assignments', id] })
  }

  return (
    <div className="panel">
      <div className="page-header">
        <div>
          <h1 className="page-title">{item.referenceNumber}</h1>
          <p className="muted">{item.title}</p>
        </div>
        <div style={{ display: 'flex', gap: '0.5rem' }}>
          {canEdit && <Link to={`/corrective-actions/${item.id}/edit`}><button type="button" className="secondary">تعديل</button></Link>}
          <button type="button" className="secondary" onClick={() => navigate('/corrective-actions')}>رجوع للقائمة</button>
        </div>
      </div>

      {item.isSensitiveRedacted && <div className="error" role="alert">هذا المحتوى محجوب لأنه يتطلب صلاحية CorrectiveActions.ViewSensitive.</div>}

      <div className="detail-grid">
        <div><span className="muted">الحالة</span><div><span className="badge" data-tone={correctiveActionStatusTone(item.status)}>{item.statusAr}</span></div></div>
        <div><span className="muted">الأولوية</span><div><span className="badge" data-tone={correctiveActionPriorityTone(item.priority)}>{item.priorityAr}</span></div></div>
        <div><span className="muted">التصنيف الأمني</span><div>{ClassificationLevelLabelsAr[item.classification] ?? item.classification}</div></div>
        <div><span className="muted">الملاحظة المرتبطة</span><div><Link to={`/notes/${item.operationalNoteId}`}>{item.operationalNoteReferenceNumber || item.operationalNoteId}</Link></div></div>
        <div><span className="muted">الإدارة المالكة</span><div>{item.ownerDepartmentId || '—'}</div></div>
        <div><span className="muted">تاريخ الاستحقاق</span><div>{formatDate(item.dueAtUtc)} {item.isOverdue && <span className="badge" data-tone="danger">متأخر {item.overdueDays ?? 0} يوم</span>}</div></div>
        <div><span className="muted">تاريخ الإنشاء</span><div>{formatDate(item.createdAtUtc)}</div></div>
        <div><span className="muted">اكتمل في</span><div>{formatDate(item.completedAtUtc)}</div></div>
      </div>

      <div className="panel-section">
        <h2 className="section-title">الوصف وملخص الإنجاز</h2>
        <p>{item.description}</p>
        {item.completionSummary && <p><strong>ملخص الإنجاز: </strong>{item.completionSummary}</p>}
      </div>

      <div className="panel-section">
        <h2 className="section-title">التكليف الحالي</h2>
        {item.currentAssignment ? (
          <div className="detail-grid">
            <div><span className="muted">المكلّف</span><div>{item.currentAssignment.assignedToUserDisplayName || item.currentAssignment.assignedToDepartmentName || '—'}</div></div>
            <div><span className="muted">بواسطة</span><div>{item.currentAssignment.assignedByDisplayName || '—'}</div></div>
            <div><span className="muted">تاريخ التكليف</span><div>{formatDate(item.currentAssignment.assignedAtUtc)}</div></div>
            <div><span className="muted">سبب التكليف</span><div>{item.currentAssignment.reason}</div></div>
          </div>
        ) : <div className="empty">لا يوجد تكليف حالي.</div>}
      </div>

      <div className="panel-section">
        <h2 className="section-title">سجل التكليفات</h2>
        {assignmentsQuery.isLoading && <div className="loading">جاري التحميل…</div>}
        {assignmentsQuery.data?.length === 0 && <div className="empty">لا توجد تكليفات.</div>}
        {assignmentsQuery.data && assignmentsQuery.data.length > 0 && (
          <table>
            <thead><tr><th>المكلّف</th><th>بواسطة</th><th>تاريخ التكليف</th><th>انتهى</th><th>حالي</th></tr></thead>
            <tbody>{assignmentsQuery.data.map((a) => (
              <tr key={a.id}>
                <td>{a.assignedToUserDisplayName || a.assignedToDepartmentName || '—'}</td>
                <td>{a.assignedByDisplayName || '—'}</td>
                <td>{formatDate(a.assignedAtUtc)}</td>
                <td>{formatDate(a.endedAtUtc)}</td>
                <td>{a.isCurrent ? <span className="badge" data-tone="ok">حالي</span> : '—'}</td>
              </tr>
            ))}</tbody>
          </table>
        )}
      </div>

      <div className="panel-section">
        <h2 className="section-title">الخط الزمني للحالة</h2>
        {historyQuery.isLoading && <div className="loading">جاري التحميل…</div>}
        {historyQuery.data?.length === 0 && <div className="empty">لا توجد أحداث بعد.</div>}
        {historyQuery.data && historyQuery.data.length > 0 && (
          <ul className="timeline">
            {historyQuery.data.map((h) => (
              <li key={h.id}>
                <span className="badge" data-tone={correctiveActionStatusTone(h.toStatus)}>{h.toStatusAr}</span>
                <span className="muted"> — {formatDate(h.changedAtUtc)} — {h.changedByDisplayName || '—'}</span>
                {h.reason && <div className="muted">{h.reason}</div>}
              </li>
            ))}
          </ul>
        )}
      </div>

      {actions.length > 0 && (
        <div className="panel-section">
          <h2 className="section-title">الانتقالات المتاحة</h2>
          <div className="toolbar">
            {actions.map((action) => (
              <button key={action.kind} type="button" className="secondary" onClick={() => setSelectedAction(action)}>{action.labelAr}</button>
            ))}
          </div>
          {selectedAction && <ActionPanel action={selectedAction} item={item} onDone={refreshAll} />}
        </div>
      )}

      <AttachmentsPanel actionId={item.id} />
    </div>
  )
}
