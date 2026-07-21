import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, ApiError, type FormDetail } from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import { FormDefinitionStatus } from '../../forms/formEnums'
import { type FormTransitionFormValues, formTransitionSchema } from '../../forms/formSchema'
import { useForm } from 'react-hook-form'

function formatDate(value?: string | null): string {
  if (!value) return '—'
  return new Date(value).toLocaleString('ar-SA')
}

const REVIEW_ACTIONS = [
  { key: 'RequestChanges', label: 'طلب تعديلات', permission: 'Forms.Review', allowed: 'RequestChanges' },
  { key: 'Approve', label: 'اعتماد', permission: 'Forms.Approve', allowed: 'Approve' },
  { key: 'Reject', label: 'رفض', permission: 'Forms.Review', allowed: 'Reject' },
] as const

export function FormReviewPage() {
  const canReview = usePermission('Forms.Review')
  const canApprove = usePermission('Forms.Approve')
  const { id } = useParams<{ id: string }>()
  const queryClient = useQueryClient()
  const [serverError, setServerError] = useState<string | null>(null)
  const [conflict, setConflict] = useState(false)
  const [pendingAction, setPendingAction] = useState<string | null>(null)

  const canAccess = canReview || canApprove

  const formQuery = useQuery({
    queryKey: ['form', id],
    queryFn: () => api.forms.get(id!),
    enabled: canAccess && !!id,
  })

  const decisionsQuery = useQuery({
    queryKey: ['form-review-decisions', id],
    queryFn: () => api.forms.reviewDecisions(id!),
    enabled: canAccess && !!id,
  })

  const { register, handleSubmit, reset, formState: { errors } } = useForm<FormTransitionFormValues>({
    resolver: zodResolver(formTransitionSchema),
    defaultValues: { reason: '' },
  })

  const runReviewAction = async (form: FormDetail, action: string, reason: string) => {
    setServerError(null)
    setConflict(false)
    setPendingAction(action)
    try {
      const body = { reason, rowVersion: form.rowVersion }
      switch (action) {
        case 'RequestChanges':
          await api.forms.requestChanges(form.id, body)
          break
        case 'Approve':
          await api.forms.approve(form.id, body)
          break
        case 'Reject':
          await api.forms.reject(form.id, body)
          break
        default:
          break
      }
      reset()
      await queryClient.invalidateQueries({ queryKey: ['form', id] })
      await queryClient.invalidateQueries({ queryKey: ['form-review-decisions', id] })
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.status === 409) {
          setConflict(true)
          setServerError('تم تغيير النموذج بواسطة مستخدم آخر. أعد تحميل الصفحة.')
        } else if (err.status === 403) {
          setServerError('ليست لديك صلاحية تنفيذ هذا الإجراء.')
        } else {
          setServerError(err.message)
        }
      } else {
        setServerError('تعذر تنفيذ الإجراء.')
      }
    } finally {
      setPendingAction(null)
    }
  }

  if (!canAccess) {
    return <div className="error" role="alert">ليست لديك صلاحية مراجعة النماذج.</div>
  }

  if (formQuery.isLoading || decisionsQuery.isLoading) {
    return <div className="loading">جاري التحميل…</div>
  }

  if (formQuery.isError) {
    const err = formQuery.error as ApiError
    const message = err.status === 404 ? 'النموذج غير موجود أو خارج نطاقك.' : err.message
    return (
      <div className="error" role="alert">
        <span>{message}</span>
        <button type="button" className="secondary" onClick={() => formQuery.refetch()}>إعادة المحاولة</button>
      </div>
    )
  }

  const form = formQuery.data
  if (!form) return <div className="empty">النموذج غير موجود.</div>

  const inReview = form.status === FormDefinitionStatus.InReview
  const availableActions = REVIEW_ACTIONS.filter((a) => {
    if (!form.allowedActions.includes(a.allowed)) return false
    if (a.permission === 'Forms.Approve') return canApprove
    return canReview
  })

  return (
    <div className="panel" dir="rtl">
      <div className="page-header">
        <div>
          <h1 className="page-title">مراجعة النموذج {form.code}</h1>
          <p className="muted">{form.nameAr} — {form.statusAr}</p>
        </div>
        <Link to={`/forms/${form.id}`}><button type="button" className="secondary">العودة للتفاصيل</button></Link>
      </div>

      {!inReview && (
        <div className="empty">النموذج ليس في حالة «قيد المراجعة» حاليًا.</div>
      )}

      {inReview && availableActions.length > 0 && (
        <div className="panel-section">
          <h2 className="section-title">قرار المراجعة</h2>
          <form
            onSubmit={handleSubmit(() => {
              /* actions handled per-button */
            })}
          >
            <label className="field field-wide">
              <span>السبب *</span>
              <textarea aria-label="سبب القرار" rows={3} {...register('reason')} />
              {errors.reason && <span className="field-error">{errors.reason.message}</span>}
            </label>
            <div className="toolbar">
              {availableActions.map((action) => (
                <button
                  key={action.key}
                  type="button"
                  disabled={!!pendingAction || conflict}
                  onClick={handleSubmit((values) => void runReviewAction(form, action.key, values.reason))}
                >
                  {pendingAction === action.key ? 'جارٍ التنفيذ…' : action.label}
                </button>
              ))}
            </div>
          </form>
          {serverError && (
            <div className="error" role="alert">
              <span>{serverError}</span>
              {conflict && (
                <button type="button" className="secondary" onClick={() => { setConflict(false); void formQuery.refetch() }}>
                  إعادة تحميل
                </button>
              )}
            </div>
          )}
        </div>
      )}

      <div className="panel-section">
        <h2 className="section-title">سجل قرارات المراجعة</h2>
        {decisionsQuery.isError && (
          <div className="error" role="alert">تعذر تحميل سجل المراجعة.</div>
        )}
        {decisionsQuery.data?.length === 0 && (
          <div className="empty">لا توجد قرارات مراجعة مسجّلة.</div>
        )}
        {decisionsQuery.data && decisionsQuery.data.length > 0 && (
          <table>
            <thead>
              <tr>
                <th>القرار</th>
                <th>من</th>
                <th>إلى</th>
                <th>المراجع</th>
                <th>التاريخ</th>
                <th>السبب</th>
              </tr>
            </thead>
            <tbody>
              {decisionsQuery.data.map((d) => (
                <tr key={d.id}>
                  <td>{d.decisionAr}</td>
                  <td>{d.fromStatusAr}</td>
                  <td>{d.toStatusAr}</td>
                  <td>{d.reviewedByDisplayName || '—'}</td>
                  <td>{formatDate(d.reviewedAtUtc)}</td>
                  <td>{d.reason || '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  )
}
