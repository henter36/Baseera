import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { useNavigate, useParams, useSearchParams } from 'react-router'
import { api, ApiError, type FormSchemaValidationIssue, type FormVersionValidateResult } from '../../../api/client'
import { usePermission } from '../../../auth/AuthProvider'
import { formatApiError, hasAllowedAction, updateFieldInSchema, useDesignerSchema } from '../../../forms/designer/designerHelpers'
import type { HistoryState } from '../../../forms/designer/historyStore'
import type { FormFieldType, FormSchemaDocument } from '../../../forms/designer/schemaTypes'
import { renamePageTitle } from '../../../forms/designer/studioSchemaOps'
import { useFormDesignerAutosave } from '../../../forms/designer/useFormDesignerAutosave'
import { useResponsiveStudioLayout } from '../../../forms/designer/useResponsiveStudioLayout'
import { useUnsavedChangesGuard } from '../../../forms/designer/useUnsavedChangesGuard'
import type { IssueLocation } from '../../../forms/designer/ValidationPanel'
import { StudioDesktopWorkspace } from './StudioDesktopWorkspace'
import { StudioMobileWorkspace } from './StudioMobileWorkspace'
import { StudioStartFlow } from './StudioStartFlow'
import { useStudioFieldCommands } from './useStudioFieldCommands'
import {
  loadRecentTypes,
  resolveErrorMessage,
  runVersionReviewDecision,
  saveCurrentSchemaAsNewVersion,
  saveRecentTypes,
  serializeSchemaOrNull,
  syncConflictServerSchema,
  syncDesignerStateFromVersion,
  type ReviewDecisionAction,
} from './studioWorkspaceHelpers'

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
    if (versionsQuery.isError) {
      return <div className="error" role="alert">{formatApiError(versionsQuery.error as ApiError)}</div>
    }
    if (versionsQuery.data?.length === 0) {
      if (canDesign) {
        return <CreateFirstVersion formId={formId} onCreated={(versionId) => navigate(`/forms/designer/${formId}?versionId=${versionId}`, { replace: true })} />
      }
      return <div className="error" role="alert">لا يوجد إصدار متاح لهذا النموذج، وليست لديك صلاحية إنشاء واحد.</div>
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
  // `mutation` is a react-query object whose identity changes every render; a ref to its
  // latest `mutate` lets the mount-only effect below have an empty, honest dependency array
  // instead of suppressing the exhaustive-deps lint rule.
  const mutateRef = useRef(mutation.mutate)
  mutateRef.current = mutation.mutate
  const triggeredRef = useRef(false)
  useEffect(() => {
    if (triggeredRef.current) return
    triggeredRef.current = true
    mutateRef.current()
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

  const { handleAddField, handleAddPage } = useStudioFieldCommands({
    canEdit,
    schema,
    page,
    selectedFieldId,
    addField,
    applySchema,
    setSelectedFieldId,
    setSelectedPageId,
    recordRecentType: (type) => setRecentTypes((prev) => {
      const next = [type, ...prev.filter((t) => t !== type)].slice(0, 6)
      saveRecentTypes(next)
      return next
    }),
  })

  useEffect(() => {
    syncDesignerStateFromVersion({
      versionData: versionQuery.data,
      versionId,
      initializedVersionIdRef,
      forceReseedRef,
      setHistory,
      setSelectedPageId,
      setSelectedFieldId,
      setRowVersion,
      markSavedBaseline,
    })
  }, [versionQuery.data, versionId, markSavedBaseline])

  const handleReloadAfterConflict = () => {
    forceReseedRef.current = true
    setConflictServerSchema(null)
    void versionQuery.refetch()
  }

  useEffect(() => {
    syncConflictServerSchema({ status, formId, versionId, setConflictServerSchema })
  }, [status, formId, versionId])

  const saveAsNewVersionMutation = useMutation({
    mutationFn: () => saveCurrentSchemaAsNewVersion(formId, schema),
    onMutate: () => setIsSavingAsNewVersion(true),
    onSettled: () => setIsSavingAsNewVersion(false),
    onSuccess: (created) => navigate(`/forms/designer/${formId}?versionId=${created.id}`, { replace: true }),
  })

  const validateMutation = useMutation({
    mutationFn: () => api.forms.validateVersion(formId, versionId, { schemaJson: serializeSchemaOrNull(schema), rowVersion }),
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
    mutationFn: ({ action, reason }: { action: ReviewDecisionAction; reason: string }) =>
      runVersionReviewDecision(formId, versionId, action, reason, versionQuery.data!.rowVersion),
    onSuccess: () => {
      setDecisionError(null)
      void qc.invalidateQueries({ queryKey: ['form-version', formId, versionId] })
      void qc.invalidateQueries({ queryKey: ['form', formId] })
    },
    onError: (err) => setDecisionError(resolveErrorMessage(err, 'تعذر تنفيذ القرار.')),
  })

  if (versionQuery.isLoading || !history || !schema) {
    return <div className="loading">جاري تحميل الاستوديو…</div>
  }

  if (versionQuery.isError) {
    return <div className="error" role="alert">{formatApiError(versionQuery.error as ApiError)}</div>
  }

  const navigateToElement = (location: IssueLocation) => {
    if (location.pageId) setSelectedPageId(location.pageId)
    if (location.fieldId) setSelectedFieldId(location.fieldId)
    setPreviewMode(null)
  }

  const renameFieldLabel = (pageId: string, fieldId: string, label: string) =>
    guardedApplySchema(updateFieldInSchema(schema, pageId, fieldId, { labelAr: label }))
  const renamePage = (pageId: string, title: string) => guardedApplySchema(renamePageTitle(schema, pageId, title))

  if (layoutMode === 'mobile') {
    return (
      <StudioMobileWorkspace
        formNameAr={form.nameAr}
        formStatusAr={form.statusAr}
        versionNumber={versionQuery.data!.versionNumber}
        versionStatusAr={versionQuery.data!.statusAr}
        autosaveStatus={status}
        autosaveError={error}
        canValidate={canEdit}
        formId={formId}
        schema={schema}
        issues={issues}
        previewMode={previewMode}
        onTogglePreview={() => setPreviewMode(previewMode ? null : 'mobile')}
        onClosePreview={() => setPreviewMode(null)}
        onChangePreviewMode={setPreviewMode}
        onValidate={() => validateMutation.mutate()}
        onReloadAfterConflict={handleReloadAfterConflict}
        onRenameFieldLabel={renameFieldLabel}
        onRenamePageTitle={renamePage}
        canRequestReview={hasAllowedAction(allowedActions, 'SubmitForReview')}
        onRequestReview={() => submitMutation.mutate()}
        isRequestingReview={submitMutation.isPending}
      />
    )
  }

  return (
    <StudioDesktopWorkspace
      formId={formId}
      versionId={versionId}
      formNameAr={form.nameAr}
      formStatusAr={form.statusAr}
      versionNumber={versionQuery.data!.versionNumber}
      versionStatusAr={versionQuery.data!.statusAr}
      allowedActions={allowedActions}
      autosaveStatus={status}
      autosaveError={error}
      canEdit={canEdit}
      canReviewOrApprove={canReviewOrApprove}
      history={history}
      setHistory={setHistory}
      schema={schema}
      page={page}
      selectedPageId={selectedPageId}
      selectedFieldId={selectedFieldId}
      selectedField={selectedField}
      setSelectedPageId={setSelectedPageId}
      setSelectedFieldId={setSelectedFieldId}
      rawIssues={issues}
      lastValidation={lastValidation}
      previewMode={previewMode}
      setPreviewMode={setPreviewMode}
      layoutMode={layoutMode}
      rightTab={rightTab}
      setRightTab={setRightTab}
      leftPanelOpen={leftPanelOpen}
      setLeftPanelOpen={setLeftPanelOpen}
      inspectorPanelOpen={inspectorPanelOpen}
      setInspectorPanelOpen={setInspectorPanelOpen}
      recentTypes={recentTypes}
      handleAddField={handleAddField}
      handleAddPage={handleAddPage}
      guardedApplySchema={guardedApplySchema}
      onDragEnd={onDragEnd}
      moveFieldKeyboard={moveFieldKeyboard}
      renameFieldLabel={renameFieldLabel}
      renamePage={renamePage}
      navigateToElement={navigateToElement}
      conflictServerSchema={conflictServerSchema}
      onReloadAfterConflict={handleReloadAfterConflict}
      onSaveAsNewVersion={() => saveAsNewVersionMutation.mutate()}
      isSavingAsNewVersion={isSavingAsNewVersion}
      onValidate={() => validateMutation.mutate()}
      onRequestReview={() => submitMutation.mutate()}
      isRequestingReview={submitMutation.isPending}
      onReviewDecision={(action, reason) => decisionMutation.mutate({ action, reason })}
      isDecisionPending={decisionMutation.isPending}
      decisionError={decisionError}
    />
  )
}
