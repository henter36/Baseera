import { FormPreviewPanel } from '../../../forms/designer/FormPreviewPanel'
import type { FormSchemaDocument } from '../../../forms/designer/schemaTypes'
import type { AutosaveStatus } from '../../../forms/designer/useFormDesignerAutosave'
import { StudioMobileReview } from './StudioMobileReview'
import { StudioTopBar } from './StudioTopBar'

type PreviewMode = 'desktop' | 'tablet' | 'mobile'

type StudioMobileWorkspaceProps = {
  formNameAr: string
  formStatusAr: string
  versionNumber: number
  versionStatusAr: string
  autosaveStatus: AutosaveStatus
  autosaveError: string | null
  canValidate: boolean
  formId: string
  schema: FormSchemaDocument
  errorCount: number
  warningCount: number
  previewMode: PreviewMode | null
  onTogglePreview: () => void
  onClosePreview: () => void
  onChangePreviewMode: (mode: PreviewMode) => void
  onValidate: () => void
  onReloadAfterConflict: () => void
  onRenameFieldLabel: (pageId: string, fieldId: string, label: string) => void
  onRenamePageTitle: (pageId: string, title: string) => void
  canRequestReview: boolean
  onRequestReview: () => void
  isRequestingReview: boolean
}

/** Mobile is a deliberately narrower surface (review + simple text edits only, no canvas/DnD/
 * conditions/formulas) — see phase2a-form-designer-scope.md. Kept as its own component so the
 * desktop/tablet studio's render logic doesn't carry this branch's complexity. */
export function StudioMobileWorkspace({
  formNameAr,
  formStatusAr,
  versionNumber,
  versionStatusAr,
  autosaveStatus,
  autosaveError,
  canValidate,
  formId,
  schema,
  errorCount,
  warningCount,
  previewMode,
  onTogglePreview,
  onClosePreview,
  onChangePreviewMode,
  onValidate,
  onReloadAfterConflict,
  onRenameFieldLabel,
  onRenamePageTitle,
  canRequestReview,
  onRequestReview,
  isRequestingReview,
}: Readonly<StudioMobileWorkspaceProps>) {
  return (
    <div className="panel" dir="rtl">
      <StudioTopBar
        formNameAr={formNameAr}
        formStatusAr={formStatusAr}
        versionNumber={versionNumber}
        versionStatusAr={versionStatusAr}
        autosaveStatus={autosaveStatus}
        autosaveError={autosaveError}
        canUndo={false}
        canRedo={false}
        canValidate={canValidate}
        canPreview
        canRequestReview={false}
        isRequestingReview={false}
        formId={formId}
        onUndo={() => undefined}
        onRedo={() => undefined}
        onValidate={onValidate}
        onTogglePreview={onTogglePreview}
        onRequestReview={() => undefined}
        onReloadAfterConflict={onReloadAfterConflict}
      />
      {previewMode ? (
        <FormPreviewPanel schema={schema} mode={previewMode} onModeChange={onChangePreviewMode} onClose={onClosePreview} />
      ) : (
        <StudioMobileReview
          schema={schema}
          errorCount={errorCount}
          warningCount={warningCount}
          onRenameFieldLabel={onRenameFieldLabel}
          onRenamePageTitle={onRenamePageTitle}
          onTogglePreview={onTogglePreview}
          canRequestReview={canRequestReview}
          onRequestReview={onRequestReview}
          isRequestingReview={isRequestingReview}
        />
      )}
    </div>
  )
}
