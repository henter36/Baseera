import { useState } from 'react'
import { VersionCompare } from '../../../forms/designer/VersionCompare'
import type { FormSchemaDocument } from '../../../forms/designer/schemaTypes'

type StudioConflictBannerProps = {
  localSchema: FormSchemaDocument
  serverSchema: FormSchemaDocument | null
  isLoadingServerSchema: boolean
  onLoadLatest: () => void
  onSaveAsNewVersion: () => void
  isSavingAsNewVersion: boolean
}

export function StudioConflictBanner({
  localSchema,
  serverSchema,
  isLoadingServerSchema,
  onLoadLatest,
  onSaveAsNewVersion,
  isSavingAsNewVersion,
}: Readonly<StudioConflictBannerProps>) {
  const [showCompare, setShowCompare] = useState(false)

  return (
    <div className="error" role="alert">
      <div>تم تعديل المسودة من مستخدم آخر منذ آخر تحميل لهذه الصفحة.</div>
      <div className="toolbar">
        <button type="button" onClick={onLoadLatest}>تحميل النسخة الأحدث</button>
        <button type="button" className="secondary" onClick={() => setShowCompare((v) => !v)} disabled={isLoadingServerSchema}>
          {showCompare ? 'إخفاء المقارنة' : 'مقارنة التغييرات'}
        </button>
        <button type="button" className="secondary" disabled={isSavingAsNewVersion} onClick={onSaveAsNewVersion}>
          {isSavingAsNewVersion ? 'جارٍ الحفظ…' : 'حفظ نسخة جديدة بتعديلاتي'}
        </button>
      </div>
      {showCompare && serverSchema && (
        <VersionCompare beforeSchema={serverSchema} afterSchema={localSchema} beforeLabelAr="النسخة على الخادم" afterLabelAr="تعديلاتي غير المحفوظة" />
      )}
    </div>
  )
}
