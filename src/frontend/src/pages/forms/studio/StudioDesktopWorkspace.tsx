import type { DragEndEvent } from '@dnd-kit/core'
import type { FormVersionValidateResult, FormSchemaValidationIssue } from '../../../api/client'
import { findDependents } from '../../../forms/designer/fieldDependencies'
import { FormPreviewPanel } from '../../../forms/designer/FormPreviewPanel'
import { redoHistory, undoHistory, type HistoryState } from '../../../forms/designer/historyStore'
import type { FormFieldSchema, FormFieldType, FormPageSchema, FormSchemaDocument } from '../../../forms/designer/schemaTypes'
import {
  addSection as addSectionOp,
  deleteField as deleteFieldOp,
  deletePage as deletePageOp,
  deleteSection as deleteSectionOp,
  duplicateField as duplicateFieldOp,
  duplicatePage as duplicatePageOp,
  duplicateSection as duplicateSectionOp,
  renameSectionTitle,
} from '../../../forms/designer/studioSchemaOps'
import type { AutosaveStatus } from '../../../forms/designer/useFormDesignerAutosave'
import { classifyIssues, type ClassifiedIssue, ValidationPanel, type IssueLocation } from '../../../forms/designer/ValidationPanel'
import { StudioCanvas } from './StudioCanvas'
import { StudioConflictBanner } from './StudioConflictBanner'
import { StudioInspector } from './StudioInspector'
import { StudioReviewPanel } from './StudioReviewPanel'
import { StudioSidePanel } from './StudioSidePanel'
import { StudioTopBar } from './StudioTopBar'
import { countPageErrors, resolveFieldIssueTone, type ReviewDecisionAction } from './studioWorkspaceHelpers'

type PreviewMode = 'desktop' | 'tablet' | 'mobile'

type StudioDesktopWorkspaceProps = {
  formId: string
  versionId: string
  formNameAr: string
  formStatusAr: string
  versionNumber: number
  versionStatusAr: string
  allowedActions: string[]
  autosaveStatus: AutosaveStatus
  autosaveError: string | null
  canEdit: boolean
  canReviewOrApprove: boolean
  history: HistoryState
  setHistory: (updater: (h: HistoryState | null) => HistoryState | null) => void
  schema: FormSchemaDocument
  page: FormPageSchema | undefined
  selectedPageId: string | null
  selectedFieldId: string | null
  selectedField: FormFieldSchema | undefined
  setSelectedPageId: (id: string) => void
  setSelectedFieldId: (id: string) => void
  rawIssues: FormSchemaValidationIssue[]
  lastValidation: FormVersionValidateResult | null
  previewMode: PreviewMode | null
  setPreviewMode: (mode: PreviewMode | null) => void
  layoutMode: 'desktop' | 'tablet'
  rightTab: 'library' | 'outline'
  setRightTab: (tab: 'library' | 'outline') => void
  leftPanelOpen: boolean
  setLeftPanelOpen: (updater: (v: boolean) => boolean) => void
  inspectorPanelOpen: boolean
  setInspectorPanelOpen: (updater: (v: boolean) => boolean) => void
  recentTypes: FormFieldType[]
  handleAddField: (type: FormFieldType) => void
  handleAddPage: () => void
  guardedApplySchema: (next: FormSchemaDocument) => void
  onDragEnd: (event: DragEndEvent, page: FormPageSchema | undefined) => void
  moveFieldKeyboard: (fieldId: string, direction: -1 | 1, page: FormPageSchema | undefined) => void
  renameFieldLabel: (pageId: string, fieldId: string, label: string) => void
  renamePage: (pageId: string, title: string) => void
  navigateToElement: (location: IssueLocation) => void
  conflictServerSchema: FormSchemaDocument | null
  onReloadAfterConflict: () => void
  onSaveAsNewVersion: () => void
  isSavingAsNewVersion: boolean
  onValidate: () => void
  onRequestReview: () => void
  isRequestingReview: boolean
  onReviewDecision: (action: ReviewDecisionAction, reason: string) => void
  isDecisionPending: boolean
  decisionError: string | null
}

/** The full desktop/tablet studio: top bar, conflict banner, and either the preview panel or the
 * editor surface (field library/outline + canvas + inspector + validation + review). Split out of
 * StudioWorkspace so this render tree's many small conditionals (preview toggle, tablet panel
 * toggles, undo/redo availability, etc.) count toward this component's own complexity budget
 * instead of the orchestrator's. */
export function StudioDesktopWorkspace({
  formId,
  versionId,
  formNameAr,
  formStatusAr,
  versionNumber,
  versionStatusAr,
  allowedActions,
  autosaveStatus,
  autosaveError,
  canEdit,
  canReviewOrApprove,
  history,
  setHistory,
  schema,
  page,
  selectedPageId,
  selectedFieldId,
  selectedField,
  setSelectedPageId,
  setSelectedFieldId,
  rawIssues,
  lastValidation,
  previewMode,
  setPreviewMode,
  layoutMode,
  rightTab,
  setRightTab,
  leftPanelOpen,
  setLeftPanelOpen,
  inspectorPanelOpen,
  setInspectorPanelOpen,
  recentTypes,
  handleAddField,
  handleAddPage,
  guardedApplySchema,
  onDragEnd,
  moveFieldKeyboard,
  renameFieldLabel,
  renamePage,
  navigateToElement,
  conflictServerSchema,
  onReloadAfterConflict,
  onSaveAsNewVersion,
  isSavingAsNewVersion,
  onValidate,
  onRequestReview,
  isRequestingReview,
  onReviewDecision,
  isDecisionPending,
  decisionError,
}: Readonly<StudioDesktopWorkspaceProps>) {
  const canSubmitReview = allowedActions.includes('SubmitForReview')
  const canUndo = canEdit && history.past.length > 0
  const canRedo = canEdit && history.future.length > 0
  const { errors, warnings } = classifyIssues(schema, rawIssues)
  const panelOpen = layoutMode === 'desktop' ? undefined : leftPanelOpen

  return (
    <div className="panel studio-shell" dir="rtl">
      <StudioTopBar
        formNameAr={formNameAr}
        formStatusAr={formStatusAr}
        versionNumber={versionNumber}
        versionStatusAr={versionStatusAr}
        autosaveStatus={autosaveStatus}
        autosaveError={autosaveError}
        canUndo={canUndo}
        canRedo={canRedo}
        canValidate
        canPreview
        canRequestReview={canSubmitReview}
        isRequestingReview={isRequestingReview}
        formId={formId}
        onUndo={() => setHistory((h) => (h ? undoHistory(h) : h))}
        onRedo={() => setHistory((h) => (h ? redoHistory(h) : h))}
        onValidate={onValidate}
        onTogglePreview={() => setPreviewMode(previewMode ? null : 'desktop')}
        onRequestReview={onRequestReview}
        onReloadAfterConflict={onReloadAfterConflict}
      />

      {autosaveStatus === 'conflict' && (
        <StudioConflictBanner
          localSchema={schema}
          serverSchema={conflictServerSchema}
          isLoadingServerSchema={!conflictServerSchema}
          onLoadLatest={onReloadAfterConflict}
          onSaveAsNewVersion={onSaveAsNewVersion}
          isSavingAsNewVersion={isSavingAsNewVersion}
        />
      )}

      {previewMode ? (
        <FormPreviewPanel schema={schema} mode={previewMode} onModeChange={setPreviewMode} onClose={() => setPreviewMode(null)} />
      ) : (
        <StudioDesktopEditor
          formId={formId}
          versionId={versionId}
          formStatusAr={formStatusAr}
          versionStatusAr={versionStatusAr}
          allowedActions={allowedActions}
          layoutMode={layoutMode}
          leftPanelOpen={leftPanelOpen}
          setLeftPanelOpen={setLeftPanelOpen}
          inspectorPanelOpen={inspectorPanelOpen}
          setInspectorPanelOpen={setInspectorPanelOpen}
          panelOpen={panelOpen}
          rightTab={rightTab}
          setRightTab={setRightTab}
          canEdit={canEdit}
          canReviewOrApprove={canReviewOrApprove}
          handleAddField={handleAddField}
          handleAddPage={handleAddPage}
          recentTypes={recentTypes}
          schema={schema}
          page={page}
          selectedPageId={selectedPageId}
          selectedFieldId={selectedFieldId}
          selectedField={selectedField}
          setSelectedPageId={setSelectedPageId}
          setSelectedFieldId={setSelectedFieldId}
          errors={errors}
          warnings={warnings}
          issues={rawIssues}
          lastValidation={lastValidation}
          guardedApplySchema={guardedApplySchema}
          onDragEnd={onDragEnd}
          moveFieldKeyboard={moveFieldKeyboard}
          renameFieldLabel={renameFieldLabel}
          renamePage={renamePage}
          navigateToElement={navigateToElement}
          isRequestingReview={isRequestingReview}
          onRequestReview={onRequestReview}
          onReviewDecision={onReviewDecision}
          isDecisionPending={isDecisionPending}
          decisionError={decisionError}
        />
      )}
    </div>
  )
}

type StudioDesktopEditorProps = {
  formId: string
  versionId: string
  formStatusAr: string
  versionStatusAr: string
  allowedActions: string[]
  layoutMode: 'desktop' | 'tablet'
  leftPanelOpen: boolean
  setLeftPanelOpen: (updater: (v: boolean) => boolean) => void
  inspectorPanelOpen: boolean
  setInspectorPanelOpen: (updater: (v: boolean) => boolean) => void
  panelOpen: boolean | undefined
  rightTab: 'library' | 'outline'
  setRightTab: (tab: 'library' | 'outline') => void
  canEdit: boolean
  canReviewOrApprove: boolean
  handleAddField: (type: FormFieldType) => void
  handleAddPage: () => void
  recentTypes: FormFieldType[]
  schema: FormSchemaDocument
  page: FormPageSchema | undefined
  selectedPageId: string | null
  selectedFieldId: string | null
  selectedField: FormFieldSchema | undefined
  setSelectedPageId: (id: string) => void
  setSelectedFieldId: (id: string) => void
  errors: ClassifiedIssue[]
  warnings: ClassifiedIssue[]
  issues: FormSchemaValidationIssue[]
  lastValidation: FormVersionValidateResult | null
  guardedApplySchema: (next: FormSchemaDocument) => void
  onDragEnd: (event: DragEndEvent, page: FormPageSchema | undefined) => void
  moveFieldKeyboard: (fieldId: string, direction: -1 | 1, page: FormPageSchema | undefined) => void
  renameFieldLabel: (pageId: string, fieldId: string, label: string) => void
  renamePage: (pageId: string, title: string) => void
  navigateToElement: (location: IssueLocation) => void
  isRequestingReview: boolean
  onRequestReview: () => void
  onReviewDecision: (action: ReviewDecisionAction, reason: string) => void
  isDecisionPending: boolean
  decisionError: string | null
}

/** The editor surface shown whenever the studio is not in preview mode: field library/outline,
 * canvas, inspector, validation panel, and the review panel. Kept separate from
 * StudioDesktopWorkspace so the preview-mode toggle above stays a single, simple ternary there. */
function StudioDesktopEditor({
  formId,
  versionId,
  formStatusAr,
  versionStatusAr,
  allowedActions,
  layoutMode,
  leftPanelOpen,
  setLeftPanelOpen,
  inspectorPanelOpen,
  setInspectorPanelOpen,
  panelOpen,
  rightTab,
  setRightTab,
  canEdit,
  canReviewOrApprove,
  handleAddField,
  handleAddPage,
  recentTypes,
  schema,
  page,
  selectedPageId,
  selectedFieldId,
  selectedField,
  setSelectedPageId,
  setSelectedFieldId,
  errors,
  warnings,
  issues,
  lastValidation,
  guardedApplySchema,
  onDragEnd,
  moveFieldKeyboard,
  renameFieldLabel,
  renamePage,
  navigateToElement,
  isRequestingReview,
  onRequestReview,
  onReviewDecision,
  isDecisionPending,
  decisionError,
}: Readonly<StudioDesktopEditorProps>) {
  const guardedOnDragEnd = canEdit ? onDragEnd : () => undefined
  const guardedMoveFieldKeyboard = canEdit ? moveFieldKeyboard : () => undefined

  return (
    <>
      {layoutMode === 'tablet' && (
        <div className="studio-panel-toggle-row">
          <button type="button" className="secondary" onClick={() => setLeftPanelOpen((v) => !v)} aria-expanded={leftPanelOpen}>مكتبة الحقول / المخطط</button>
          <button type="button" className="secondary" onClick={() => setInspectorPanelOpen((v) => !v)} aria-expanded={inspectorPanelOpen}>Inspector</button>
        </div>
      )}

      <div className="studio-body">
        <StudioSidePanel
          rightTab={rightTab}
          onChangeTab={setRightTab}
          canEdit={canEdit}
          onAddField={handleAddField}
          recentTypes={recentTypes}
          schema={schema}
          selectedFieldId={selectedFieldId}
          errorCountByPageId={(pageId) => countPageErrors(errors, pageId)}
          onSelectField={(pageId, fieldId) => { setSelectedPageId(pageId); setSelectedFieldId(fieldId) }}
          panelOpen={panelOpen}
        />

        <StudioCanvas
          schema={schema}
          page={page}
          selectedPageId={selectedPageId}
          selectedFieldId={selectedFieldId}
          fieldIssueTone={(fieldId) => resolveFieldIssueTone(errors, warnings, fieldId)}
          fieldDependents={(key) => findDependents(schema, key)}
          onSelectPage={setSelectedPageId}
          onSelectField={setSelectedFieldId}
          onAddPage={handleAddPage}
          onDuplicatePage={(pageId) => guardedApplySchema(duplicatePageOp(schema, pageId))}
          onDeletePage={(pageId) => guardedApplySchema(deletePageOp(schema, pageId))}
          canDeletePage={canEdit && schema.pages.length > 1}
          onRenamePageTitle={renamePage}
          onAddSection={(pageId) => guardedApplySchema(addSectionOp(schema, pageId))}
          onDuplicateSection={(pageId, sectionId) => guardedApplySchema(duplicateSectionOp(schema, pageId, sectionId))}
          onDeleteSection={(pageId, sectionId) => guardedApplySchema(deleteSectionOp(schema, pageId, sectionId))}
          onRenameSectionTitle={(sectionId, title) => guardedApplySchema(renameSectionTitle(schema, sectionId, title))}
          onDragEnd={guardedOnDragEnd}
          onMoveField={guardedMoveFieldKeyboard}
          onDuplicateField={(pageId, sectionId, fieldId) => guardedApplySchema(duplicateFieldOp(schema, pageId, sectionId, fieldId))}
          onDeleteField={(pageId, sectionId, fieldId) => guardedApplySchema(deleteFieldOp(schema, pageId, sectionId, fieldId))}
          onRenameFieldLabel={renameFieldLabel}
        />

        <div className="studio-inspector-panel" data-panel-open={panelOpen}>
          <StudioInspector schema={schema} page={page} selectedField={selectedField} issues={issues} onApplySchema={guardedApplySchema} />
        </div>
      </div>

      {(errors.length > 0 || warnings.length > 0) && (
        <ValidationPanel schema={schema} issues={issues} onNavigateToElement={navigateToElement} />
      )}

      {(canEdit || canReviewOrApprove) && (
        <StudioReviewPanel
          formId={formId}
          versionId={versionId}
          formStatusAr={formStatusAr}
          versionStatusAr={versionStatusAr}
          versionAllowedActions={allowedActions}
          lastValidation={lastValidation}
          hasBlockingErrors={errors.length > 0}
          isSubmitting={isRequestingReview}
          onSubmitForReview={onRequestReview}
          onReviewDecision={onReviewDecision}
          isDecisionPending={isDecisionPending}
          decisionError={decisionError}
        />
      )}
    </>
  )
}
