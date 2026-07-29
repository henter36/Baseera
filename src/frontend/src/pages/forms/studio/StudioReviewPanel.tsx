import { useState } from 'react'
import { Link } from 'react-router'
import type { FormVersionValidateResult } from '../../../api/client'
import { hasAllowedAction } from '../../../forms/designer/designerHelpers'

type StudioReviewPanelProps = {
  formId: string
  versionId: string
  formStatusAr: string
  versionStatusAr: string
  versionAllowedActions: string[]
  lastValidation: FormVersionValidateResult | null
  hasBlockingErrors: boolean
  isSubmitting: boolean
  onSubmitForReview: () => void
  onReviewDecision: (action: 'RequestChanges' | 'Reject' | 'ApproveAndLock', reason: string) => void
  isDecisionPending: boolean
  decisionError: string | null
}

export function StudioReviewPanel({
  formId,
  versionId,
  formStatusAr,
  versionStatusAr,
  versionAllowedActions,
  lastValidation,
  hasBlockingErrors,
  isSubmitting,
  onSubmitForReview,
  onReviewDecision,
  isDecisionPending,
  decisionError,
}: Readonly<StudioReviewPanelProps>) {
  const [reason, setReason] = useState('')
  const canSubmit = hasAllowedAction(versionAllowedActions, 'SubmitForReview')
  const canRequestChanges = hasAllowedAction(versionAllowedActions, 'RequestChanges')
  const canReject = hasAllowedAction(versionAllowedActions, 'Reject')
  const canApprove = hasAllowedAction(versionAllowedActions, 'ApproveAndLock')
  const isLocked = versionAllowedActions.includes('ViewSnapshot') && !canSubmit && !canRequestChanges && !canReject && !canApprove

  return (
    <div className="panel-section">
      <h2 className="section-title">حالة النموذج والمراجعة</h2>
      <p className="muted">حالة النموذج: {formStatusAr} — حالة الإصدار: {versionStatusAr}</p>

      {canSubmit && (
        <>
          {lastValidation && (
            <dl className="detail-grid">
              <dt>الصفحات</dt><dd>{lastValidation.pageCount}</dd>
              <dt>الأقسام</dt><dd>{lastValidation.sectionCount}</dd>
              <dt>الحقول</dt><dd>{lastValidation.fieldCount}</dd>
              <dt>الشروط</dt><dd>{lastValidation.conditionCount}</dd>
              <dt>الحقول المحسوبة (الصيغ)</dt><dd>{lastValidation.calculatedFieldCount}</dd>
              <dt>التحذيرات المتبقية</dt><dd>{lastValidation.issues.filter((i) => i.severity !== 0).length}</dd>
            </dl>
          )}
          <button type="button" disabled={isSubmitting || hasBlockingErrors} onClick={onSubmitForReview}>
            {isSubmitting ? 'جارٍ الإرسال…' : 'طلب المراجعة'}
          </button>
          {hasBlockingErrors && <p className="muted">أصلح أخطاء التحقق المانعة قبل طلب المراجعة.</p>}
        </>
      )}

      {(canRequestChanges || canReject || canApprove) && (
        <div className="panel-section">
          <h3 className="section-title">قرار المراجعة</h3>
          <label className="field field-wide">
            <span>السبب {(canRequestChanges || canReject) ? '*' : ''}</span>
            <textarea rows={3} value={reason} onChange={(e) => setReason(e.target.value)} disabled={isDecisionPending} />
          </label>
          <div className="toolbar">
            {canRequestChanges && (
              <button type="button" className="secondary" disabled={isDecisionPending || reason.trim().length === 0} onClick={() => onReviewDecision('RequestChanges', reason.trim())}>
                إعادة للمصمم (سبب إلزامي)
              </button>
            )}
            {canReject && (
              <button type="button" className="secondary" disabled={isDecisionPending || reason.trim().length === 0} onClick={() => onReviewDecision('Reject', reason.trim())}>
                رفض
              </button>
            )}
            {canApprove && (
              <button type="button" disabled={isDecisionPending} onClick={() => onReviewDecision('ApproveAndLock', reason.trim())}>
                اعتماد وقفل
              </button>
            )}
          </div>
          {decisionError && <div className="error" role="alert">{decisionError}</div>}
        </div>
      )}

      {isLocked && (
        <div className="panel-section">
          <p className="muted">هذا الإصدار معتمد ومقفل؛ التعديل عليه ينشئ إصدار مسودة جديدًا.</p>
          <Link to={`/form-campaigns/new?formId=${formId}&versionId=${versionId}`}>
            <button type="button">الانتقال إلى الجدولة والنشر</button>
          </Link>
        </div>
      )}
    </div>
  )
}
