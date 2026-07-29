import { useState } from 'react'
import { flattenFields } from '../../../forms/designer/fieldDependencies'
import type { FormSchemaDocument } from '../../../forms/designer/schemaTypes'

type StudioMobileReviewProps = {
  schema: FormSchemaDocument
  errorCount: number
  warningCount: number
  onRenameFieldLabel: (pageId: string, fieldId: string, label: string) => void
  onRenamePageTitle: (pageId: string, title: string) => void
  onTogglePreview: () => void
  canRequestReview: boolean
  onRequestReview: () => void
  isRequestingReview: boolean
}

export function StudioMobileReview({
  schema,
  errorCount,
  warningCount,
  onRenameFieldLabel,
  onRenamePageTitle,
  onTogglePreview,
  canRequestReview,
  onRequestReview,
  isRequestingReview,
}: Readonly<StudioMobileReviewProps>) {
  const [draftValues, setDraftValues] = useState<Record<string, string>>({})

  return (
    <div className="studio-mobile" dir="rtl">
      <div className="studio-mobile-banner" role="status">
        الهيكلة المتقدمة (الصفحات والأقسام والشروط والصيغ) تتطلب شاشة أكبر (حاسوب أو جهاز لوحي).
        يمكنك من الجوال مراجعة النموذج وتعديل العناوين والنصوص البسيطة فقط.
      </div>

      <div className="panel-section" aria-live="polite">
        {errorCount > 0 ? (
          <span className="error" role="alert">{errorCount} أخطاء تمنع المراجعة.</span>
        ) : (
          <span className="muted">لا توجد أخطاء مانعة.</span>
        )}
        {warningCount > 0 && <span className="warn"> {warningCount} تحذيرات.</span>}
      </div>

      {schema.pages.map((page) => (
        <div className="panel-section" key={page.id}>
          <label className="field field-wide">
            <span>عنوان الصفحة</span>
            <input
              value={draftValues[`page-${page.id}`] ?? page.titleAr}
              onChange={(e) => setDraftValues((v) => ({ ...v, [`page-${page.id}`]: e.target.value }))}
              onBlur={(e) => { if (e.target.value.trim()) onRenamePageTitle(page.id, e.target.value.trim()) }}
            />
          </label>
          {flattenFields({ ...schema, pages: [page] }).map(({ field }) => (
            <label className="field field-wide" key={field.id}>
              <span>عنوان الحقل</span>
              <input
                value={draftValues[`field-${field.id}`] ?? field.labelAr}
                onChange={(e) => setDraftValues((v) => ({ ...v, [`field-${field.id}`]: e.target.value }))}
                onBlur={(e) => { if (e.target.value.trim()) onRenameFieldLabel(page.id, field.id, e.target.value.trim()) }}
              />
            </label>
          ))}
        </div>
      ))}

      <div className="toolbar">
        <button type="button" className="secondary" onClick={onTogglePreview}>معاينة</button>
        {canRequestReview && (
          <button type="button" disabled={isRequestingReview || errorCount > 0} onClick={onRequestReview}>
            {isRequestingReview ? 'جارٍ الإرسال…' : 'طلب المراجعة'}
          </button>
        )}
      </div>
    </div>
  )
}
