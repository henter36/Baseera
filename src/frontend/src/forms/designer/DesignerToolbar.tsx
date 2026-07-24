import { Link } from 'react-router'
import type { AutosaveStatus } from './useFormDesignerAutosave'
import { saveStatusLabel } from './designerHelpers'

type DesignerToolbarProps = {
  versionNumber: number
  canUndo: boolean
  canRedo: boolean
  canSubmit: boolean
  isSubmitting: boolean
  status: AutosaveStatus
  error: string | null
  formId: string
  onUndo: () => void
  onRedo: () => void
  onValidate: () => void
  onTogglePreview: () => void
  onSubmit: () => void
  onReload: () => void
}

export function DesignerToolbar({
  versionNumber,
  canUndo,
  canRedo,
  canSubmit,
  isSubmitting,
  status,
  error,
  formId,
  onUndo,
  onRedo,
  onValidate,
  onTogglePreview,
  onSubmit,
  onReload,
}: Readonly<DesignerToolbarProps>) {
  return (
    <>
      <div className="page-header">
        <h1 className="page-title">مصمم النموذج — v{versionNumber}</h1>
        <div className="toolbar">
          <button type="button" className="secondary" disabled={!canUndo} onClick={onUndo}>تراجع</button>
          <button type="button" className="secondary" disabled={!canRedo} onClick={onRedo}>إعادة</button>
          <button type="button" className="secondary" onClick={onValidate}>تحقق</button>
          <button type="button" className="secondary" onClick={onTogglePreview}>معاينة</button>
          {canSubmit && (
            <button type="button" onClick={onSubmit} disabled={isSubmitting || status === 'saving'}>
              إرسال للمراجعة
            </button>
          )}
          <Link to={`/forms/${formId}/versions`} className="secondary">الإصدارات</Link>
        </div>
      </div>
      <div className="muted" aria-live="polite">
        الحالة: {saveStatusLabel(status)}
        {error ? ` — ${error}` : ''}
        {status === 'conflict' && (
          <button type="button" className="secondary" onClick={onReload}>إعادة التحميل</button>
        )}
      </div>
    </>
  )
}
