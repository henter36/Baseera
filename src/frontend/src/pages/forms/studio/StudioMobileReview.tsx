import { useState } from 'react'
import { flattenFields } from '../../../forms/designer/fieldDependencies'
import type { FormPageSchema, FormSchemaDocument } from '../../../forms/designer/schemaTypes'

function MobileReviewPageTitleField({
  pageId,
  titleAr,
  onCommit,
}: Readonly<{ pageId: string; titleAr: string; onCommit: (pageId: string, title: string) => void }>) {
  const [draft, setDraft] = useState(titleAr)
  const [syncedTitleAr, setSyncedTitleAr] = useState(titleAr)
  if (titleAr !== syncedTitleAr) {
    setSyncedTitleAr(titleAr)
    setDraft(titleAr)
  }
  return (
    <label className="field field-wide">
      <span>عنوان الصفحة</span>
      <input
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        onBlur={() => { if (draft.trim()) onCommit(pageId, draft.trim()) }}
      />
    </label>
  )
}

function MobileReviewFieldLabelField({
  pageId,
  fieldId,
  labelAr,
  onCommit,
}: Readonly<{ pageId: string; fieldId: string; labelAr: string; onCommit: (pageId: string, fieldId: string, label: string) => void }>) {
  const [draft, setDraft] = useState(labelAr)
  const [syncedLabelAr, setSyncedLabelAr] = useState(labelAr)
  if (labelAr !== syncedLabelAr) {
    setSyncedLabelAr(labelAr)
    setDraft(labelAr)
  }
  return (
    <label className="field field-wide">
      <span>عنوان الحقل</span>
      <input
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        onBlur={() => { if (draft.trim()) onCommit(pageId, fieldId, draft.trim()) }}
      />
    </label>
  )
}

function MobileReviewPageSection({
  page,
  onRenamePageTitle,
  onRenameFieldLabel,
}: Readonly<{
  page: FormPageSchema
  onRenamePageTitle: (pageId: string, title: string) => void
  onRenameFieldLabel: (pageId: string, fieldId: string, label: string) => void
}>) {
  const fields = flattenFields({ schemaFormatVersion: 1, pages: [page] })
  return (
    <div className="panel-section">
      <MobileReviewPageTitleField pageId={page.id} titleAr={page.titleAr} onCommit={onRenamePageTitle} />
      {fields.map(({ field }) => (
        <MobileReviewFieldLabelField key={field.id} pageId={page.id} fieldId={field.id} labelAr={field.labelAr} onCommit={onRenameFieldLabel} />
      ))}
    </div>
  )
}

function MobileReviewStatusSummary({ errorCount, warningCount }: Readonly<{ errorCount: number; warningCount: number }>) {
  const errorMessage = errorCount > 0
    ? <span className="error" role="alert">{errorCount} أخطاء تمنع المراجعة.</span>
    : <span className="muted">لا توجد أخطاء مانعة.</span>

  return (
    <div className="panel-section" aria-live="polite">
      {errorMessage}
      {warningCount > 0 && <span className="warn"> {warningCount} تحذيرات.</span>}
    </div>
  )
}

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
  return (
    <div className="studio-mobile" dir="rtl">
      <output className="studio-mobile-banner" aria-live="polite">
        الهيكلة المتقدمة (الصفحات والأقسام والشروط والصيغ) تتطلب شاشة أكبر (حاسوب أو جهاز لوحي).
        يمكنك من الجوال مراجعة النموذج وتعديل العناوين والنصوص البسيطة فقط.
      </output>

      <MobileReviewStatusSummary errorCount={errorCount} warningCount={warningCount} />

      {schema.pages.map((page) => (
        <MobileReviewPageSection key={page.id} page={page} onRenamePageTitle={onRenamePageTitle} onRenameFieldLabel={onRenameFieldLabel} />
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
