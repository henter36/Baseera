import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { useNavigate, useParams, useSearchParams } from 'react-router'
import { api, ApiError, type FormSchemaValidationIssue, type FormVersionValidateResult } from '../../../api/client'
import { usePermission } from '../../../auth/AuthProvider'
import { formatApiError, hasAllowedAction, updateFieldInSchema, useDesignerSchema } from '../../../forms/designer/designerHelpers'
import { findDependents } from '../../../forms/designer/fieldDependencies'
import { FormPreviewPanel } from '../../../forms/designer/FormPreviewPanel'
import { createHistory, redoHistory, undoHistory, type HistoryState } from '../../../forms/designer/historyStore'
import {
  FormFieldTypeLabelsAr,
  FormFieldTypes,
  createEmptySchema,
  type FormFieldSchema,
  type FormFieldType,
  type FormSchemaDocument,
} from '../../../forms/designer/schemaTypes'
import {
  addSection,
  canDeletePage as canDeletePageOp,
  deleteField as deleteFieldOp,
  deletePage as deletePageOp,
  deleteSection as deleteSectionOp,
  duplicateField as duplicateFieldOp,
  duplicatePage as duplicatePageOp,
  duplicateSection as duplicateSectionOp,
  renamePageTitle,
  renameSectionTitle,
} from '../../../forms/designer/studioSchemaOps'
import { useFormDesignerAutosave } from '../../../forms/designer/useFormDesignerAutosave'
import { useResponsiveStudioLayout } from '../../../forms/designer/useResponsiveStudioLayout'
import { useUnsavedChangesGuard } from '../../../forms/designer/useUnsavedChangesGuard'
import { classifyIssues, ValidationPanel } from '../../../forms/designer/ValidationPanel'
import { StudioCanvas } from './StudioCanvas'
import { StudioConflictBanner } from './StudioConflictBanner'
import { StudioFieldLibrary } from './StudioFieldLibrary'
import { StudioInspector } from './StudioInspector'
import { StudioMobileReview } from './StudioMobileReview'
import { StudioOutline } from './StudioOutline'
import { StudioReviewPanel } from './StudioReviewPanel'
import { StudioStartFlow } from './StudioStartFlow'
import { StudioTopBar } from './StudioTopBar'

const RECENT_TYPES_STORAGE_KEY = 'baseera.studio.recentFieldTypes'

function newField(type: FormFieldType): FormFieldSchema {
  const id = crypto.randomUUID()
  return {
    id,
    key: `field_${id.slice(0, 8)}`,
    type,
    labelAr: FormFieldTypeLabelsAr[type] ?? 'حقل',
    order: 0,
    layoutWidth: 0,
    isRequired: false,
    validationRules: [],
    isReadOnly: type === FormFieldTypes.CalculatedNumber || type === FormFieldTypes.CalculatedText,
    isCalculated: type === FormFieldTypes.CalculatedNumber || type === FormFieldTypes.CalculatedText,
    choice: type === FormFieldTypes.SingleChoice || type === FormFieldTypes.MultipleChoice
      ? { options: [{ value: 'a', labelAr: 'خيار أ', order: 0, isActive: true }], allowOther: false }
      : null,
  }
}

function parseSchema(json: string | undefined): FormSchemaDocument {
  if (!json) return createEmptySchema()
  try {
    const parsed = JSON.parse(json) as FormSchemaDocument
    return parsed.pages?.length ? parsed : createEmptySchema()
  } catch {
    return createEmptySchema()
  }
}

function loadRecentTypes(): FormFieldType[] {
  try {
    const raw = window.localStorage.getItem(RECENT_TYPES_STORAGE_KEY)
    return raw ? (JSON.parse(raw) as FormFieldType[]) : []
  } catch {
    return []
  }
}

function saveRecentTypes(types: FormFieldType[]) {
  try {
    window.localStorage.setItem(RECENT_TYPES_STORAGE_KEY, JSON.stringify(types.slice(0, 6)))
  } catch {
    // Non-critical preference; ignore storage failures (private mode, quota, etc.).
  }
}

export function FormDesignerStudioPage() {
  const { formId } = useParams<{ formId?: string }>()
  const navigate = useNavigate()
  const layoutMode = useResponsiveStudioLayout()

  if (!formId) {
    return (
      <div className="panel studio-shell" dir="rtl">
        <h1 className="page-title">استوديو النماذج — نموذج جديد</h1>
        <StudioStartFlow onCreated={(newFormId, versionId) => navigate(`/forms/designer/${newFormId}?versionId=${versionId}`, { replace: true })} />
      </div>
    )
  }

  return <StudioForExistingForm formId={formId} layoutMode={layoutMode} />
}

function StudioForExistingForm({ formId, layoutMode }: Readonly<{ formId: string; layoutMode: 'desktop' | 'tablet' | 'mobile' }>) {
  const canDesign = usePermission('Forms.UpdateDraft')
  const canViewHistory = usePermission('Forms.ViewVersionHistory')
  const navigate = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()
  const versionIdParam = searchParams.get('versionId')

  const formQuery = useQuery({
    queryKey: ['form', formId],
    queryFn: () => api.forms.get(formId),
    enabled: canViewHistory,
  })
  const versionsQuery = useQuery({
    queryKey: ['form-versions', formId],
    queryFn: () => api.forms.listVersions(formId),
    enabled: canViewHistory && !versionIdParam,
  })

  useEffect(() => {
    if (versionIdParam || !versionsQuery.data) return
    const editable = versionsQuery.data.find((v) => v.status === 0 || v.status === 2)
    const resolved = editable ?? versionsQuery.data[0]
    if (resolved) {
      setSearchParams((prev) => { prev.set('versionId', resolved.id); return prev }, { replace: true })
    }
  }, [versionIdParam, versionsQuery.data, setSearchParams])

  if (!canViewHistory) {
    return <div className="error" role="alert">ليست لديك صلاحية عرض هذا النموذج.</div>
  }

  if (formQuery.isLoading || (!versionIdParam && versionsQuery.isLoading)) {
    return <div className="loading">جاري تحميل الاستوديو…</div>
  }

  if (formQuery.isError) {
    return <div className="error" role="alert">{formatApiError(formQuery.error as ApiError)}</div>
  }

  if (!versionIdParam) {
    if (versionsQuery.data && versionsQuery.data.length === 0 && canDesign) {
      return <CreateFirstVersion formId={formId} onCreated={(versionId) => navigate(`/forms/designer/${formId}?versionId=${versionId}`, { replace: true })} />
    }
    return <div className="loading">جاري تحديد الإصدار…</div>
  }

  return <StudioWorkspace formId={formId} versionId={versionIdParam} form={formQuery.data!} layoutMode={layoutMode} />
}

function CreateFirstVersion({ formId, onCreated }: Readonly<{ formId: string; onCreated: (versionId: string) => void }>) {
  const mutation = useMutation({
    mutationFn: () => api.forms.createVersion(formId),
    onSuccess: (version) => onCreated(version.id),
  })
  const triggeredRef = useRef(false)
  useEffect(() => {
    if (triggeredRef.current) return
    triggeredRef.current = true
    mutation.mutate()
    // Intentionally fires once on mount only — `mutation` is a react-query object whose
    // identity changes every render, so including it here would re-trigger the create call.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])
  if (mutation.isError) {
    return <div className="error" role="alert">{formatApiError(mutation.error as ApiError)}</div>
  }
  return <div className="loading">جاري إنشاء أول إصدار…</div>
}

function StudioWorkspace({
  formId,
  versionId,
  form,
  layoutMode,
}: Readonly<{ formId: string; versionId: string; form: { nameAr: string; statusAr: string }; layoutMode: 'desktop' | 'tablet' | 'mobile' }>) {
  const canDesign = usePermission('Forms.UpdateDraft')
  const canRequestChangesPerm = usePermission('Forms.RequestChanges')
  const canRejectPerm = usePermission('Forms.Reject')
  const canApprovePerm = usePermission('Forms.Approve')
  const canReviewOrApprove = canRequestChangesPerm || canRejectPerm || canApprovePerm
  const qc = useQueryClient()
  const navigate = useNavigate()

  const [history, setHistory] = useState<HistoryState | null>(null)
  const [selectedPageId, setSelectedPageId] = useState<string | null>(null)
  const [selectedFieldId, setSelectedFieldId] = useState<string | null>(null)
  const [issues, setIssues] = useState<FormSchemaValidationIssue[]>([])
  const [lastValidation, setLastValidation] = useState<FormVersionValidateResult | null>(null)
  const [previewMode, setPreviewMode] = useState<'desktop' | 'tablet' | 'mobile' | null>(null)
  const [rowVersion, setRowVersion] = useState('')
  const [rightTab, setRightTab] = useState<'library' | 'outline'>('library')
  const [leftPanelOpen, setLeftPanelOpen] = useState(false)
  const [inspectorPanelOpen, setInspectorPanelOpen] = useState(false)
  const [recentTypes, setRecentTypes] = useState<FormFieldType[]>(() => loadRecentTypes())
  const [decisionError, setDecisionError] = useState<string | null>(null)
  const [conflictServerSchema, setConflictServerSchema] = useState<FormSchemaDocument | null>(null)
  const [isSavingAsNewVersion, setIsSavingAsNewVersion] = useState(false)
  const initializedVersionIdRef = useRef<string | null>(null)
  const forceReseedRef = useRef(false)

  const versionQuery = useQuery({
    queryKey: ['form-version', formId, versionId],
    queryFn: () => api.forms.getVersion(formId, versionId),
    enabled: !!formId && !!versionId,
  })

  const schema = history?.present
  const page = schema?.pages.find((p) => p.id === selectedPageId) ?? schema?.pages[0]
  const selectedField = page?.sections.flatMap((s) => s.fields).find((f) => f.id === selectedFieldId)
  const allowedActions = versionQuery.data?.allowedActions ?? []
  const canEdit = canDesign && hasAllowedAction(allowedActions, 'SaveSchema')

  const { status, error, flush, markSavedBaseline } = useFormDesignerAutosave({
    formId,
    versionId,
    schema,
    rowVersion,
    onRowVersionChange: setRowVersion,
    enabled: canEdit,
  })

  useUnsavedChangesGuard(status === 'dirty' || status === 'saving')

  const { applySchema, onDragEnd, moveFieldKeyboard, addField } = useDesignerSchema(history, setHistory, () => undefined)
  const guardedApplySchema = (next: FormSchemaDocument) => { if (canEdit) applySchema(next) }

  useEffect(() => {
    if (!versionQuery.data) return
    const isNewVersion = initializedVersionIdRef.current !== versionId
    const shouldReseed = isNewVersion || forceReseedRef.current
    if (!shouldReseed) {
      setRowVersion(versionQuery.data.rowVersion)
      return
    }

    const nextSchema = parseSchema(versionQuery.data.draftSchemaJson)
    setHistory(createHistory(nextSchema))
    setSelectedPageId(nextSchema.pages[0]?.id ?? null)
    setSelectedFieldId(null)
    setRowVersion(versionQuery.data.rowVersion)
    markSavedBaseline(JSON.stringify(nextSchema))
    initializedVersionIdRef.current = versionId
    forceReseedRef.current = false
  }, [versionQuery.data, versionId, markSavedBaseline])

  const handleReloadAfterConflict = () => {
    forceReseedRef.current = true
    setConflictServerSchema(null)
    void versionQuery.refetch()
  }

  useEffect(() => {
    if (status !== 'conflict') { setConflictServerSchema(null); return }
    void api.forms.getVersion(formId, versionId).then((latest) => setConflictServerSchema(parseSchema(latest.draftSchemaJson))).catch(() => undefined)
  }, [status, formId, versionId])

  const saveAsNewVersionMutation = useMutation({
    mutationFn: async () => {
      const created = await api.forms.createVersion(formId, {})
      if (schema) {
        await api.forms.saveSchema(formId, created.id, { schemaJson: JSON.stringify(schema), rowVersion: created.rowVersion })
      }
      return created
    },
    onMutate: () => setIsSavingAsNewVersion(true),
    onSettled: () => setIsSavingAsNewVersion(false),
    onSuccess: (created) => navigate(`/forms/designer/${formId}?versionId=${created.id}`, { replace: true }),
  })

  const validateMutation = useMutation({
    mutationFn: () => api.forms.validateVersion(formId, versionId, { schemaJson: schema ? JSON.stringify(schema) : null, rowVersion }),
    onSuccess: (result) => { setIssues(result.issues); setLastValidation(result) },
  })

  const submitMutation = useMutation({
    mutationFn: async () => {
      const latestRowVersion = await flush()
      await api.forms.submitVersionReview(formId, versionId, { rowVersion: latestRowVersion, reason: 'إرسال للمراجعة' })
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['form-version', formId, versionId] })
      void qc.invalidateQueries({ queryKey: ['form', formId] })
    },
  })

  const decisionMutation = useMutation({
    mutationFn: async ({ action, reason }: { action: 'RequestChanges' | 'Reject' | 'ApproveAndLock'; reason: string }) => {
      const body = { reason, rowVersion: versionQuery.data!.rowVersion }
      if (action === 'RequestChanges') return api.forms.requestVersionChanges(formId, versionId, body)
      if (action === 'Reject') return api.forms.rejectVersion(formId, versionId, body)
      return api.forms.approveLockVersion(formId, versionId, body)
    },
    onSuccess: () => {
      setDecisionError(null)
      void qc.invalidateQueries({ queryKey: ['form-version', formId, versionId] })
      void qc.invalidateQueries({ queryKey: ['form', formId] })
    },
    onError: (err) => setDecisionError(err instanceof ApiError ? formatApiError(err) : 'تعذر تنفيذ القرار.'),
  })

  const recordRecentType = (type: FormFieldType) => {
    setRecentTypes((prev) => {
      const next = [type, ...prev.filter((t) => t !== type)].slice(0, 6)
      saveRecentTypes(next)
      return next
    })
  }

  const handleAddField = (type: FormFieldType) => {
    if (!canEdit || !page) return
    const targetSection = page.sections.find((s) => s.fields.some((f) => f.id === selectedFieldId)) ?? page.sections[0]
    if (!targetSection) return
    const fieldId = addField(newField(type), page, targetSection)
    if (fieldId) setSelectedFieldId(fieldId)
    recordRecentType(type)
  }

  const handleAddPage = () => {
    if (!canEdit || !schema) return
    const id = crypto.randomUUID()
    applySchema({
      ...schema,
      pages: [
        ...schema.pages,
        { id, key: `page_${id.slice(0, 8)}`, titleAr: `صفحة ${schema.pages.length + 1}`, order: schema.pages.length, sections: [{ id: crypto.randomUUID(), key: `section_${crypto.randomUUID().slice(0, 8)}`, titleAr: 'قسم', order: 0, fields: [] }] },
      ],
    })
    setSelectedPageId(id)
  }

  if (versionQuery.isLoading || !history || !schema) {
    return <div className="loading">جاري تحميل الاستوديو…</div>
  }

  if (versionQuery.isError) {
    return <div className="error" role="alert">{formatApiError(versionQuery.error as ApiError)}</div>
  }

  const { errors, warnings } = classifyIssues(schema, issues)
  const errorCountByPageId = (pageId: string) =>
    errors.filter((i) => i.location.pageId === pageId).length

  const fieldIssueTone = (fieldId: string) => ({
    hasError: errors.some((i) => i.location.fieldId === fieldId),
    hasWarning: warnings.some((i) => i.location.fieldId === fieldId),
  })

  const navigateToElement = (location: { pageId: string | null; fieldId: string | null }) => {
    if (location.pageId) setSelectedPageId(location.pageId)
    if (location.fieldId) setSelectedFieldId(location.fieldId)
    setPreviewMode(null)
  }

  if (layoutMode === 'mobile') {
    return (
      <div className="panel" dir="rtl">
        <StudioTopBar
          formNameAr={form.nameAr}
          formStatusAr={form.statusAr}
          versionNumber={versionQuery.data!.versionNumber}
          versionStatusAr={versionQuery.data!.statusAr}
          autosaveStatus={status}
          autosaveError={error}
          canUndo={false}
          canRedo={false}
          canValidate={canEdit}
          canPreview
          canRequestReview={false}
          isRequestingReview={false}
          formId={formId}
          onUndo={() => undefined}
          onRedo={() => undefined}
          onValidate={() => validateMutation.mutate()}
          onTogglePreview={() => setPreviewMode(previewMode ? null : 'mobile')}
          onRequestReview={() => undefined}
          onReloadAfterConflict={handleReloadAfterConflict}
        />
        {previewMode ? (
          <FormPreviewPanel schema={schema} mode={previewMode} onModeChange={setPreviewMode} onClose={() => setPreviewMode(null)} />
        ) : (
          <StudioMobileReview
            schema={schema}
            errorCount={errors.length}
            warningCount={warnings.length}
            onRenameFieldLabel={(pageId, fieldId, label) => canEdit && applySchema({ ...schema, pages: schema.pages.map((p) => (p.id !== pageId ? p : { ...p, sections: p.sections.map((s) => ({ ...s, fields: s.fields.map((f) => (f.id === fieldId ? { ...f, labelAr: label } : f)) })) })) })}
            onRenamePageTitle={(pageId, title) => canEdit && applySchema(renamePageTitle(schema, pageId, title))}
            onTogglePreview={() => setPreviewMode(previewMode ? null : 'mobile')}
            canRequestReview={hasAllowedAction(allowedActions, 'SubmitForReview')}
            onRequestReview={() => submitMutation.mutate()}
            isRequestingReview={submitMutation.isPending}
          />
        )}
      </div>
    )
  }

  return (
    <div className="panel studio-shell" dir="rtl">
      <StudioTopBar
        formNameAr={form.nameAr}
        formStatusAr={form.statusAr}
        versionNumber={versionQuery.data!.versionNumber}
        versionStatusAr={versionQuery.data!.statusAr}
        autosaveStatus={status}
        autosaveError={error}
        canUndo={canEdit && history.past.length > 0}
        canRedo={canEdit && history.future.length > 0}
        canValidate
        canPreview
        canRequestReview={hasAllowedAction(allowedActions, 'SubmitForReview')}
        isRequestingReview={submitMutation.isPending}
        formId={formId}
        onUndo={() => setHistory((h) => (h ? undoHistory(h) : h))}
        onRedo={() => setHistory((h) => (h ? redoHistory(h) : h))}
        onValidate={() => validateMutation.mutate()}
        onTogglePreview={() => setPreviewMode(previewMode ? null : 'desktop')}
        onRequestReview={() => submitMutation.mutate()}
        onReloadAfterConflict={handleReloadAfterConflict}
      />

      {status === 'conflict' && (
        <StudioConflictBanner
          localSchema={schema}
          serverSchema={conflictServerSchema}
          isLoadingServerSchema={!conflictServerSchema}
          onLoadLatest={handleReloadAfterConflict}
          onSaveAsNewVersion={() => saveAsNewVersionMutation.mutate()}
          isSavingAsNewVersion={isSavingAsNewVersion}
        />
      )}

      {previewMode ? (
        <FormPreviewPanel schema={schema} mode={previewMode} onModeChange={setPreviewMode} onClose={() => setPreviewMode(null)} />
      ) : (
        <>
          {layoutMode === 'tablet' && (
            <div className="studio-panel-toggle-row">
              <button type="button" className="secondary" onClick={() => setLeftPanelOpen((v) => !v)} aria-expanded={leftPanelOpen}>مكتبة الحقول / المخطط</button>
              <button type="button" className="secondary" onClick={() => setInspectorPanelOpen((v) => !v)} aria-expanded={inspectorPanelOpen}>Inspector</button>
            </div>
          )}

          <div className="studio-body">
            <div className="studio-side" data-panel-open={layoutMode === 'desktop' ? undefined : leftPanelOpen}>
              <div className="studio-side-tabs" role="tablist">
                <button type="button" role="tab" aria-selected={rightTab === 'library'} className={rightTab === 'library' ? undefined : 'secondary'} onClick={() => setRightTab('library')}>مكتبة الحقول</button>
                <button type="button" role="tab" aria-selected={rightTab === 'outline'} className={rightTab === 'outline' ? undefined : 'secondary'} onClick={() => setRightTab('outline')}>مخطط النموذج</button>
              </div>
              {rightTab === 'library' ? (
                canEdit ? (
                  <StudioFieldLibrary onAddField={handleAddField} recentTypes={recentTypes} />
                ) : (
                  <div className="muted">هذا الإصدار للقراءة فقط ولا يمكن إضافة حقول جديدة إليه.</div>
                )
              ) : (
                <StudioOutline schema={schema} selectedFieldId={selectedFieldId} errorCountByPageId={errorCountByPageId} onSelectField={(pageId, fieldId) => { setSelectedPageId(pageId); setSelectedFieldId(fieldId) }} />
              )}
            </div>

            <StudioCanvas
              schema={schema}
              page={page}
              selectedPageId={selectedPageId}
              selectedFieldId={selectedFieldId}
              fieldIssueTone={fieldIssueTone}
              fieldDependents={(key) => findDependents(schema, key)}
              onSelectPage={setSelectedPageId}
              onSelectField={setSelectedFieldId}
              onAddPage={handleAddPage}
              onDuplicatePage={(pageId) => guardedApplySchema(duplicatePageOp(schema, pageId))}
              onDeletePage={(pageId) => guardedApplySchema(deletePageOp(schema, pageId))}
              canDeletePage={canEdit && canDeletePageOp(schema)}
              onRenamePageTitle={(pageId, title) => guardedApplySchema(renamePageTitle(schema, pageId, title))}
              onAddSection={(pageId) => guardedApplySchema(addSection(schema, pageId))}
              onDuplicateSection={(pageId, sectionId) => guardedApplySchema(duplicateSectionOp(schema, pageId, sectionId))}
              onDeleteSection={(pageId, sectionId) => guardedApplySchema(deleteSectionOp(schema, pageId, sectionId))}
              onRenameSectionTitle={(sectionId, title) => guardedApplySchema(renameSectionTitle(schema, sectionId, title))}
              onDragEnd={canEdit ? onDragEnd : () => undefined}
              onMoveField={canEdit ? moveFieldKeyboard : () => undefined}
              onDuplicateField={(pageId, sectionId, fieldId) => guardedApplySchema(duplicateFieldOp(schema, pageId, sectionId, fieldId))}
              onDeleteField={(pageId, sectionId, fieldId) => guardedApplySchema(deleteFieldOp(schema, pageId, sectionId, fieldId))}
              onRenameFieldLabel={(pageId, fieldId, label) => guardedApplySchema(updateFieldInSchema(schema, pageId, fieldId, { labelAr: label }))}
            />

            <div className="studio-inspector-panel" data-panel-open={layoutMode === 'desktop' ? undefined : inspectorPanelOpen}>
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
              formStatusAr={form.statusAr}
              versionStatusAr={versionQuery.data!.statusAr}
              versionAllowedActions={allowedActions}
              lastValidation={lastValidation}
              hasBlockingErrors={errors.length > 0}
              isSubmitting={submitMutation.isPending}
              onSubmitForReview={() => submitMutation.mutate()}
              onReviewDecision={(action, reason) => decisionMutation.mutate({ action, reason })}
              isDecisionPending={decisionMutation.isPending}
              decisionError={decisionError}
            />
          )}
        </>
      )}
    </div>
  )
}
