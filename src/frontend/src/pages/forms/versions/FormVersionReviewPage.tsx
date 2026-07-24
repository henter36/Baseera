import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { api, ApiError } from '../../../api/client'
import { usePermission } from '../../../auth/AuthProvider'
import { formatApiError, hasAllowedAction } from '../../../forms/designer/designerHelpers'

export function FormVersionReviewPage() {
  const canViewHistory = usePermission('Forms.ViewVersionHistory')
  const { formId, versionId } = useParams<{ formId: string; versionId: string }>()
  const [reason, setReason] = useState('')
  const [error, setError] = useState<string | null>(null)
  const qc = useQueryClient()
  const navigate = useNavigate()

  const versionQuery = useQuery({
    queryKey: ['form-version', formId, versionId],
    queryFn: () => api.forms.getVersion(formId!, versionId!),
    enabled: canViewHistory && !!formId && !!versionId,
  })

  const run = useMutation({
    mutationFn: async (action: 'changes' | 'reject' | 'approve') => {
      const body = { reason: reason.trim(), rowVersion: versionQuery.data!.rowVersion }
      if (action === 'changes') {
        return api.forms.requestVersionChanges(formId!, versionId!, body)
      }

      if (action === 'reject') {
        return api.forms.rejectVersion(formId!, versionId!, body)
      }

      return api.forms.approveLockVersion(formId!, versionId!, body)
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['form-version', formId, versionId] })
      void navigate(`/forms/${formId}/versions/${versionId}`)
    },
    onError: (err: ApiError) => {
      setError(formatApiError(err))
    },
  })

  const allowedActions = versionQuery.data?.allowedActions ?? []
  const canRequestChanges = hasAllowedAction(allowedActions, 'RequestChanges')
  const canReject = hasAllowedAction(allowedActions, 'Reject')
  const canApprove = hasAllowedAction(allowedActions, 'ApproveAndLock')
  const trimmedReason = reason.trim()
  const disabled = run.isPending || !versionQuery.data

  if (!canViewHistory) {
    return <div className="error" role="alert">ليست لديك صلاحية عرض سجل الإصدارات.</div>
  }

  if (versionQuery.isLoading) {
    return <div className="loading">جاري التحميل…</div>
  }

  if (versionQuery.isError) {
    return <div className="error" role="alert">{formatApiError(versionQuery.error as ApiError)}</div>
  }

  return (
    <div className="panel" dir="rtl">
      <div className="page-header">
        <h1 className="page-title">مراجعة الإصدار v{versionQuery.data!.versionNumber}</h1>
        <Link to={`/forms/${formId}/versions/${versionId}`}>عودة</Link>
      </div>
      {error && <div className="error" role="alert">{error}</div>}
      <label className="field">
        السبب <span className="muted">(مطلوب لطلب التعديلات والرفض)</span>
        <textarea value={reason} onChange={(e) => setReason(e.target.value)} rows={3} disabled={disabled} />
      </label>
      <div className="toolbar">
        {canRequestChanges && (
          <button
            type="button"
            className="secondary"
            disabled={disabled || trimmedReason.length === 0}
            onClick={() => run.mutate('changes')}
          >
            طلب تعديلات
          </button>
        )}
        {canReject && (
          <button
            type="button"
            className="secondary"
            disabled={disabled || trimmedReason.length === 0}
            onClick={() => run.mutate('reject')}
          >
            رفض
          </button>
        )}
        {canApprove && (
          <button type="button" disabled={disabled} onClick={() => run.mutate('approve')}>
            اعتماد وقفل
          </button>
        )}
      </div>
    </div>
  )
}
