import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api, ApiError } from '../../../api/client'
import { usePermission } from '../../../auth/AuthProvider'
import { DesignerCanvas } from '../../../forms/designer/DesignerCanvas'
import { DesignerPalette } from '../../../forms/designer/DesignerPalette'
import { DesignerPropertiesPanel } from '../../../forms/designer/DesignerPropertiesPanel'
import { DesignerToolbar } from '../../../forms/designer/DesignerToolbar'
import {
  formatApiError,
  hasAllowedAction,
  useDesignerSchema,
} from '../../../forms/designer/designerHelpers'
import { FormPreviewPanel } from '../../../forms/designer/FormPreviewPanel'
import { createHistory, undoHistory, redoHistory, type HistoryState } from '../../../forms/designer/historyStore'
import {
  FormFieldTypes,
  FormFieldTypeLabelsAr,
  createEmptySchema,
  type FormFieldSchema,
  type FormSchemaDocument,
} from '../../../forms/designer/schemaTypes'
import { useFormDesignerAutosave } from '../../../forms/designer/useFormDesignerAutosave'

function newField(type: number): FormFieldSchema {
  const id = crypto.randomUUID()
  return {
    id,
    key: `field_${id.slice(0, 8)}`,
    type: type as FormFieldSchema['type'],
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

function parseSchema(draftSchemaJson: string): FormSchemaDocument {
  try {
    const parsed = JSON.parse(draftSchemaJson) as FormSchemaDocument
    return parsed.pages?.length ? parsed : createEmptySchema()
  } catch {
    return createEmptySchema()
  }
}

export function FormDesignerPage() {
  const canDesign = usePermission('Forms.UpdateDraft')
  const navigate = useNavigate()
  const { formId, versionId } = useParams<{ formId: string; versionId: string }>()
  const qc = useQueryClient()
  const [history, setHistory] = useState<HistoryState | null>(null)
  const [selectedPageId, setSelectedPageId] = useState<string | null>(null)
  const [selectedFieldId, setSelectedFieldId] = useState<string | null>(null)
  const [issues, setIssues] = useState<Array<{ code: string; path: string; messageAr: string; severity: number }>>([])
  const [previewMode, setPreviewMode] = useState<'desktop' | 'tablet' | 'mobile' | null>(null)
  const [rowVersion, setRowVersion] = useState('')
  const initializedVersionIdRef = useRef<string | null>(null)
  const forceReseedRef = useRef(false)

  const versionQuery = useQuery({
    queryKey: ['form-version', formId, versionId],
    queryFn: () => api.forms.getVersion(formId!, versionId!),
    enabled: canDesign && !!formId && !!versionId,
  })

  const schema = history?.present
  const page = schema?.pages.find((p) => p.id === selectedPageId) ?? schema?.pages[0]
  const selectedField = page?.sections.flatMap((s) => s.fields).find((f) => f.id === selectedFieldId)
  const allowedActions = versionQuery.data?.allowedActions ?? []
  const canEdit = hasAllowedAction(allowedActions, 'SaveSchema')
  const canSubmit = hasAllowedAction(allowedActions, 'SubmitForReview')

  const { status, error, flush, markSavedBaseline } = useFormDesignerAutosave({
    formId,
    versionId,
    schema,
    rowVersion,
    onRowVersionChange: setRowVersion,
    enabled: canEdit,
  })

  const { applySchema, onDragEnd, moveFieldKeyboard, addField } = useDesignerSchema(history, setHistory, () => undefined)

  useEffect(() => {
    if (!versionQuery.data || !versionId) {
      return
    }

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

  const handleReload = () => {
    forceReseedRef.current = true
    void versionQuery.refetch()
  }

  const validateMutation = useMutation({
    mutationFn: () =>
      api.forms.validateVersion(formId!, versionId!, {
        schemaJson: JSON.stringify(history!.present),
        rowVersion,
      }),
    onSuccess: (result) => setIssues(result.issues),
  })

  const submitMutation = useMutation({
    mutationFn: async () => {
      const latestRowVersion = await flush()
      await api.forms.submitVersionReview(formId!, versionId!, { rowVersion: latestRowVersion, reason: 'إرسال للمراجعة' })
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['form-version', formId, versionId] })
      void navigate(`/forms/${formId}/versions/${versionId}`)
    },
  })

  const handleAddPage = () => {
    if (!schema) {
      return
    }

    const id = crypto.randomUUID()
    applySchema({
      ...schema,
      pages: [
        ...schema.pages,
        {
          id,
          key: `page_${id.slice(0, 8)}`,
          titleAr: `صفحة ${schema.pages.length + 1}`,
          order: schema.pages.length,
          sections: [{
            id: crypto.randomUUID(),
            key: `section_${crypto.randomUUID().slice(0, 8)}`,
            titleAr: 'قسم',
            order: 0,
            fields: [],
          }],
        },
      ],
    })
    setSelectedPageId(id)
  }

  const handleAddField = (type: number) => {
    if (!page?.sections[0]) {
      return
    }

    const fieldId = addField(newField(type), page, page.sections[0])
    if (fieldId) {
      setSelectedFieldId(fieldId)
    }
  }

  if (!canDesign) {
    return <div className="error" role="alert">ليست لديك صلاحية تصميم النماذج.</div>
  }

  if (versionQuery.isLoading || !history || !schema) {
    return <div className="loading">جاري تحميل المصمم…</div>
  }

  if (versionQuery.isError) {
    return <div className="error" role="alert">{formatApiError(versionQuery.error as ApiError)}</div>
  }

  if (!canEdit) {
    return (
      <div className="error" role="alert">
        لا يمكن تعديل هذا الإصدار في حالته الحالية.
        <Link to={`/forms/${formId}/versions/${versionId}`}>عودة</Link>
      </div>
    )
  }

  return (
    <div className="panel designer-shell" dir="rtl">
      <DesignerToolbar
        versionNumber={versionQuery.data?.versionNumber ?? 0}
        canUndo={history.past.length > 0}
        canRedo={history.future.length > 0}
        canSubmit={canSubmit}
        isSubmitting={submitMutation.isPending}
        status={status}
        error={error}
        formId={formId!}
        onUndo={() => setHistory((h) => (h ? undoHistory(h) : h))}
        onRedo={() => setHistory((h) => (h ? redoHistory(h) : h))}
        onValidate={() => validateMutation.mutate()}
        onTogglePreview={() => setPreviewMode(previewMode ? null : 'desktop')}
        onSubmit={() => submitMutation.mutate()}
        onReload={handleReload}
      />

      {previewMode ? (
        <FormPreviewPanel
          schema={schema}
          mode={previewMode}
          onModeChange={setPreviewMode}
          onClose={() => setPreviewMode(null)}
        />
      ) : (
        <div className="designer-grid">
          <DesignerPalette onAddField={handleAddField} />
          <DesignerCanvas
            schema={schema}
            page={page}
            selectedPageId={selectedPageId}
            selectedFieldId={selectedFieldId}
            onSelectPage={setSelectedPageId}
            onAddPage={handleAddPage}
            onSelectField={setSelectedFieldId}
            onDragEnd={onDragEnd}
            onMoveField={moveFieldKeyboard}
          />
          <DesignerPropertiesPanel
            schema={schema}
            page={page}
            selectedField={selectedField}
            issues={issues}
            onApplySchema={applySchema}
          />
        </div>
      )}
    </div>
  )
}
