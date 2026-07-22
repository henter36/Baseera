import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, ApiError } from '../../../api/client'
import { usePermission } from '../../../auth/AuthProvider'

export function FormVersionReviewPage() {
  const canReviewPerm = usePermission('Forms.Review')
  const canRequestChanges = usePermission('Forms.RequestChanges')
  const canReject = usePermission('Forms.Reject')
  const canReview = canReviewPerm || canRequestChanges || canReject
  const canApprove = usePermission('Forms.Approve')
  const { formId, versionId } = useParams<{ formId: string; versionId: string }>()
  const [reason, setReason] = useState('')
  const [error, setError] = useState<string | null>(null)
  const qc = useQueryClient()

  const versionQuery = useQuery({
    queryKey: ['form-version', formId, versionId],
    queryFn: () => api.forms.getVersion(formId!, versionId!),
    enabled: (canReview || canApprove) && !!formId && !!versionId,
  })

  const run = useMutation({
    mutationFn: async (action: 'changes' | 'reject' | 'approve') => {
      const body = { reason, rowVersion: versionQuery.data!.rowVersion }
      if (action === 'changes') return api.forms.requestVersionChanges(formId!, versionId!, body)
      if (action === 'reject') return api.forms.rejectVersion(formId!, versionId!, body)
      return api.forms.approveLockVersion(formId!, versionId!, body)
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['form-version', formId, versionId] })
      window.location.assign(`/forms/${formId}/versions/${versionId}`)
    },
    onError: (err: ApiError) => {
      setError(err.status === 409 ? 'تعارض أو انتقال غير صالح.' : err.message)
    },
  })

  if (!(canReview || canApprove)) return <div className="error" role="alert">ليست لديك صلاحية المراجعة.</div>
  if (versionQuery.isLoading) return <div className="loading">جاري التحميل…</div>
  if (versionQuery.isError) return <div className="error" role="alert">{(versionQuery.error as ApiError).message}</div>

  return (
    <div className="panel" dir="rtl">
      <div className="page-header">
        <h1 className="page-title">مراجعة الإصدار v{versionQuery.data!.versionNumber}</h1>
        <Link to={`/forms/${formId}/versions/${versionId}`}>عودة</Link>
      </div>
      {error && <div className="error" role="alert">{error}</div>}
      <label className="field">
        السبب
        <textarea value={reason} onChange={(e) => setReason(e.target.value)} rows={3} />
      </label>
      <div className="toolbar">
        {canReview && <button type="button" className="secondary" onClick={() => run.mutate('changes')}>طلب تعديلات</button>}
        {canReview && <button type="button" className="secondary" onClick={() => run.mutate('reject')}>رفض</button>}
        {canApprove && <button type="button" onClick={() => run.mutate('approve')}>اعتماد وقفل</button>}
      </div>
    </div>
  )
}
