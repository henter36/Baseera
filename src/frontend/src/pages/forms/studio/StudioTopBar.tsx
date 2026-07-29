import { Link } from 'react-router'
import type { AutosaveStatus } from '../../../forms/designer/useFormDesignerAutosave'

const STATUS_LABELS_AR: Record<AutosaveStatus, string> = {
  idle: 'تم الحفظ',
  saved: 'تم الحفظ',
  dirty: 'توجد تغييرات غير محفوظة',
  saving: 'جارٍ الحفظ',
  error: 'فشل الحفظ',
  conflict: 'تعارض إصدار',
}

function statusTone(status: AutosaveStatus): 'ok' | 'warn' | 'danger' {
  if (status === 'error' || status === 'conflict') return 'danger'
  if (status === 'dirty' || status === 'saving') return 'warn'
  return 'ok'
}

type StudioTopBarProps = {
  formNameAr: string
  formStatusAr: string
  versionNumber: number
  versionStatusAr: string
  autosaveStatus: AutosaveStatus
  autosaveError: string | null
  canUndo: boolean
  canRedo: boolean
  canValidate: boolean
  canPreview: boolean
  canRequestReview: boolean
  isRequestingReview: boolean
  formId: string
  onUndo: () => void
  onRedo: () => void
  onValidate: () => void
  onTogglePreview: () => void
  onRequestReview: () => void
  onReloadAfterConflict: () => void
}

export function StudioTopBar({
  formNameAr,
  formStatusAr,
  versionNumber,
  versionStatusAr,
  autosaveStatus,
  autosaveError,
  canUndo,
  canRedo,
  canValidate,
  canPreview,
  canRequestReview,
  isRequestingReview,
  formId,
  onUndo,
  onRedo,
  onValidate,
  onTogglePreview,
  onRequestReview,
  onReloadAfterConflict,
}: Readonly<StudioTopBarProps>) {
  return (
    <div className="studio-topbar">
      <div className="studio-topbar-row">
        <div className="studio-topbar-identity">
          <h1 className="page-title">{formNameAr}</h1>
          <span className="muted">مسودة النموذج — {formStatusAr} · الإصدار v{versionNumber} — {versionStatusAr}</span>
        </div>
        <Link to={`/forms/${formId}/versions`} className="secondary">الإصدارات</Link>
      </div>

      <div className="studio-topbar-row">
        <div className="toolbar">
          <button type="button" className="secondary" disabled={!canUndo} onClick={onUndo}>التراجع</button>
          <button type="button" className="secondary" disabled={!canRedo} onClick={onRedo}>الإعادة</button>
          <button type="button" className="secondary" disabled={!canValidate} onClick={onValidate}>التحقق</button>
          <button type="button" className="secondary" disabled={!canPreview} onClick={onTogglePreview}>المعاينة</button>
          {canRequestReview && (
            <button type="button" disabled={isRequestingReview} onClick={onRequestReview}>
              {isRequestingReview ? 'جارٍ الإرسال…' : 'طلب المراجعة'}
            </button>
          )}
        </div>

        <div aria-live="polite" className="studio-status-pill" data-tone={statusTone(autosaveStatus)}>
          حالة الحفظ: {STATUS_LABELS_AR[autosaveStatus]}
          {autosaveError ? ` — ${autosaveError}` : ''}
          {autosaveStatus === 'conflict' && (
            <button type="button" className="secondary" onClick={onReloadAfterConflict}>إعادة التحميل</button>
          )}
        </div>
      </div>
    </div>
  )
}
