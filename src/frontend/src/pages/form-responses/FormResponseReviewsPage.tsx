import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'
import { useState } from 'react'
import { api, type FormResponseReviewAction } from '../../api/client'
import { FormAssignmentWorkStatusLabelsAr } from './responseLabels'

function resolveMutationError(error: unknown): string {
  const status = (error as { status?: number })?.status
  if (status === 404 || status === 403) return 'لا يمكن تنفيذ الإجراء على هذا الرد.'
  if (status === 409) return 'تعارض في النسخة. أعد تحميل الصفحة ثم حاول مجددًا.'
  if (status === 422) return 'تعذر التحقق من الطلب.'
  if (!navigator.onLine) return 'انقطع الاتصال. حاول مجددًا عند عودة الشبكة.'
  return 'فشل تنفيذ إجراء المراجعة.'
}

export function FormResponseReviewsPage() {
  const query = useQuery({
    queryKey: ['form-response-reviews'],
    queryFn: () => api.formResponses.reviews({ page: 1, pageSize: 50 }),
  })

  return (
    <div className="page" dir="rtl">
      <header className="page-header">
        <h1>صندوق مراجعة الردود</h1>
      </header>
      {query.isLoading && <p>جاري التحميل…</p>}
      {query.isError && <p className="error" role="alert">تعذر التحميل.</p>}
      {query.data?.items.length === 0 && <p className="muted">لا توجد ردود بانتظار المراجعة.</p>}
      {query.data && query.data.items.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>الحملة</th>
              <th>الموقع</th>
              <th>الحالة</th>
              <th>المستوى</th>
              <th>إجراء</th>
            </tr>
          </thead>
          <tbody>
            {query.data.items.map((item) => (
              <tr key={item.responseId ?? item.assignmentId}>
                <td>{item.campaignNameAr}</td>
                <td>{item.facilityNameAr}</td>
                <td>{FormAssignmentWorkStatusLabelsAr[item.workStatus]}</td>
                <td>{item.currentReviewLevel}/{item.requiredApprovalLevels}</td>
                <td>
                  {item.responseId ? (
                    <Link to={`/form-responses/${item.responseId}/review`}>مراجعة</Link>
                  ) : null}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}

export function FormResponseReviewDetailPage() {
  const { responseId = '' } = useParams()
  const queryClient = useQueryClient()
  const query = useQuery({
    queryKey: ['form-response-review', responseId],
    queryFn: () => api.formResponses.getReview(responseId),
    enabled: Boolean(responseId),
    refetchOnWindowFocus: false,
  })
  const [reason, setReason] = useState('')

  const actionMutation = useMutation({
    mutationFn: async (action: FormResponseReviewAction) => {
      const rowVersion = query.data?.workspace.rowVersion
      if (!rowVersion) throw new Error('missing row version')
      if (action === 'start') await api.formResponses.startReview(responseId, { rowVersion })
      if (action === 'return') await api.formResponses.returnResponse(responseId, { reason, rowVersion })
      if (action === 'approve') await api.formResponses.approve(responseId, { reason, rowVersion })
      if (action === 'reject') await api.formResponses.reject(responseId, { reason, rowVersion })
      if (action === 'close') await api.formResponses.close(responseId, { reason, rowVersion })
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['form-response-review', responseId] })
    },
  })

  if (query.isLoading) return <p>جاري التحميل…</p>
  if (query.isError || !query.data) return <p className="error" role="alert">تعذر فتح المراجعة.</p>
  const w = query.data.workspace

  return (
    <div className="page" dir="rtl">
      <header className="page-header">
        <h1>مراجعة: {w.campaignNameAr}</h1>
        <p className="muted">{w.facilityNameAr}</p>
        <Link to="/form-response-reviews">رجوع</Link>
      </header>
      <section>
        <h2>الإجابات</h2>
        <pre aria-label="إجابات الإرسال">{w.latestSubmission?.canonicalAnswersJson ?? w.draftAnswersJson}</pre>
      </section>
      <label className="field">
        <span>السبب</span>
        <textarea value={reason} onChange={(e) => setReason(e.target.value)} aria-label="سبب القرار" />
      </label>
      {actionMutation.isError && (
        <p className="error" role="alert">{resolveMutationError(actionMutation.error)}</p>
      )}
      <div className="actions">
        <button type="button" disabled={actionMutation.isPending} onClick={() => actionMutation.mutate('start')}>بدء المراجعة</button>
        <button type="button" disabled={actionMutation.isPending || !reason.trim()} onClick={() => actionMutation.mutate('return')}>إعادة</button>
        <button type="button" disabled={actionMutation.isPending} onClick={() => actionMutation.mutate('approve')}>اعتماد</button>
        <button type="button" disabled={actionMutation.isPending || !reason.trim()} onClick={() => actionMutation.mutate('reject')}>رفض</button>
        <button type="button" disabled={actionMutation.isPending} onClick={() => actionMutation.mutate('close')}>إغلاق</button>
      </div>
    </div>
  )
}
