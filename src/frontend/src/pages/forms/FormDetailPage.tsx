import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, ApiError, type FormDetail } from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import {
  ClassificationLevelLabelsAr,
  ScopeTypeLabelsAr,
  classificationTone,
  formStatusTone,
} from '../../forms/formEnums'

function formatDate(value?: string | null): string {
  if (!value) return '—'
  return new Date(value).toLocaleString('ar-SA')
}

function formLoadErrorMessage(error: ApiError): string {
  if (error.status === 403) {
    return 'ليست لديك صلاحية عرض هذا النموذج.'
  }

  if (error.status === 404) {
    return 'النموذج غير موجود أو خارج نطاقك.'
  }

  return error.message || 'تعذر تحميل النموذج.'
}

function ScopeLabel({ form }: Readonly<{ form: FormDetail }>) {
  const regionQuery = useQuery({
    queryKey: ['form-region', form.regionId],
    queryFn: () => api.regions().then((r) => r.items.find((x) => x.id === form.regionId)),
    enabled: !!form.regionId,
  })
  const facilityQuery = useQuery({
    queryKey: ['form-facility', form.facilityId],
    queryFn: () => api.facilities().then((r) => r.items.find((x) => x.id === form.facilityId)),
    enabled: !!form.facilityId,
  })

  const parts = [ScopeTypeLabelsAr[form.scopeType] ?? form.scopeType]
  if (regionQuery.data) parts.push(regionQuery.data.nameAr)
  if (facilityQuery.data) parts.push(facilityQuery.data.nameAr)
  if (form.facilityUnitId) parts.push(`وحدة (${form.facilityUnitId.slice(0, 8)}…)`)
  return <span>{parts.join(' — ')}</span>
}

const ACTION_LABELS: Record<string, string> = {
  UpdateDraft: 'تعديل المسودة',
  SubmitForReview: 'إرسال للمراجعة',
  RequestChanges: 'طلب تعديلات',
  Approve: 'اعتماد',
  Reject: 'رفض',
  Archive: 'أرشفة',
  Restore: 'استعادة',
}

export function FormDetailPage() {
  const canView = usePermission('Forms.View')
  const canManageAccess = usePermission('Forms.ManageAccess')
  const canReview = usePermission('Forms.Review')
  const canApprove = usePermission('Forms.Approve')
  const canReviewOrApprove = canReview || canApprove
  const { id } = useParams<{ id: string }>()
  const queryClient = useQueryClient()

  const [actionReason, setActionReason] = useState('')
  const [actionError, setActionError] = useState<string | null>(null)
  const [actionConflict, setActionConflict] = useState(false)
  const [pendingAction, setPendingAction] = useState<string | null>(null)

  const formQuery = useQuery({
    queryKey: ['form', id],
    queryFn: () => api.forms.get(id!),
    enabled: canView && !!id,
  })

  const retentionQuery = useQuery({
    queryKey: ['form-retention', id],
    queryFn: () => api.forms.retentionStatus(id!),
    enabled: canView && !!id && !!formQuery.data,
  })

  if (!canView) {
    return <div className="error" role="alert">ليست لديك صلاحية عرض النماذج.</div>
  }

  if (formQuery.isLoading) return <div className="loading">جاري التحميل…</div>

  if (formQuery.isError) {
    const err = formQuery.error as ApiError
    const message = formLoadErrorMessage(err)
    return (
      <div className="error" role="alert">
        <span>{message}</span>
        <button type="button" className="secondary" onClick={() => formQuery.refetch()}>إعادة المحاولة</button>
      </div>
    )
  }

  const form = formQuery.data
  if (!form) return <div className="empty">النموذج غير موجود.</div>

  const runAction = async (action: string) => {
    if (!actionReason.trim()) {
      setActionError('السبب مطلوب.')
      return
    }
    setActionError(null)
    setActionConflict(false)
    setPendingAction(action)
    try {
      const body = { reason: actionReason, rowVersion: form.rowVersion }
      switch (action) {
        case 'SubmitForReview':
          await api.forms.submitReview(form.id, body)
          break
        case 'Archive':
          await api.forms.archive(form.id, body)
          break
        case 'Restore':
          await api.forms.restore(form.id, body)
          break
        default:
          break
      }
      setActionReason('')
      await queryClient.invalidateQueries({ queryKey: ['form', id] })
      await queryClient.invalidateQueries({ queryKey: ['form-retention', id] })
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.status === 409) {
          setActionConflict(true)
          setActionError('تم تغيير النموذج بواسطة مستخدم آخر. أعد تحميل الصفحة.')
        } else if (err.status === 403) {
          setActionError('ليست لديك صلاحية تنفيذ هذا الإجراء.')
        } else {
          setActionError(err.message)
        }
      } else {
        setActionError('تعذر تنفيذ الإجراء.')
      }
    } finally {
      setPendingAction(null)
    }
  }

  const workflowActions = form.allowedActions.filter((a) =>
    ['SubmitForReview', 'Archive', 'Restore'].includes(a),
  )

  return (
    <div className="panel" dir="rtl">
      <div className="page-header">
        <div>
          <h1 className="page-title">{form.isSensitiveRedacted ? '[محتوى مقيّد]' : form.nameAr}</h1>
          <p className="muted">{form.code}</p>
        </div>
        <div className="toolbar">
          {form.allowedActions.includes('UpdateDraft') && (
            <Link to={`/forms/${form.id}/edit`}><button type="button" className="secondary">تعديل</button></Link>
          )}
          {canReviewOrApprove && form.status === 1 && (
            <Link to={`/forms/${form.id}/review`}><button type="button">المراجعة</button></Link>
          )}
          {canManageAccess && (
            <Link to={`/forms/${form.id}/access`}><button type="button" className="secondary">إدارة الوصول</button></Link>
          )}
        </div>
      </div>

      <div className="detail-grid">
        <div>
          <span className="muted">الحالة</span>
          <div><span className="badge" data-tone={formStatusTone(form.status)}>{form.statusAr}</span></div>
        </div>
        <div>
          <span className="muted">التصنيف</span>
          <div>
            <span className="badge" data-tone={classificationTone(form.classification)}>
              {ClassificationLevelLabelsAr[form.classification]}
            </span>
          </div>
        </div>
        <div>
          <span className="muted">النطاق</span>
          <div><ScopeLabel form={form} /></div>
        </div>
        <div>
          <span className="muted">أُنشئ بواسطة</span>
          <div>{form.createdByDisplayName || '—'}</div>
        </div>
        <div>
          <span className="muted">تاريخ الإنشاء</span>
          <div>{formatDate(form.createdAtUtc)}</div>
        </div>
        <div>
          <span className="muted">آخر تعديل</span>
          <div>{formatDate(form.updatedAtUtc)}</div>
        </div>
      </div>

      {!form.isSensitiveRedacted && (
        <div className="panel-section">
          <h2 className="section-title">الوصف</h2>
          <p>{form.description}</p>
        </div>
      )}

      {retentionQuery.data && (
        <div className="panel-section">
          <h2 className="section-title">الاحتفاظ</h2>
          <p className="muted">
            {retentionQuery.data.isRetentionApplicable
              ? `مدة الاحتفاظ: ${retentionQuery.data.retentionDays} يوم — ينتهي: ${formatDate(retentionQuery.data.expiresAtUtc)}`
              : 'سياسة الاحتفاظ غير مطبّقة على هذا النموذج.'}
            {retentionQuery.data.isEligibleForArchive && ' — مؤهل للأرشفة.'}
          </p>
        </div>
      )}

      {workflowActions.length > 0 && (
        <div className="panel-section">
          <h2 className="section-title">إجراءات سير العمل</h2>
          <label className="field field-wide">
            <span>السبب *</span>
            <textarea aria-label="سبب الإجراء" rows={2} value={actionReason} onChange={(e) => setActionReason(e.target.value)} />
          </label>
          <div className="toolbar">
            {workflowActions.map((action) => (
              <button
                key={action}
                type="button"
                disabled={!!pendingAction || actionConflict}
                onClick={() => void runAction(action)}
              >
                {pendingAction === action ? 'جارٍ التنفيذ…' : ACTION_LABELS[action] ?? action}
              </button>
            ))}
          </div>
          {actionError && (
            <div className="error" role="alert">
              <span>{actionError}</span>
              {actionConflict && (
                <button type="button" className="secondary" onClick={() => { setActionConflict(false); void formQuery.refetch() }}>
                  إعادة تحميل
                </button>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  )
}
