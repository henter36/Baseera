import {
  DndContext,
  PointerSensor,
  KeyboardSensor,
  closestCenter,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core'
import {
  SortableContext,
  arrayMove,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, ApiError } from '../../../api/client'
import { usePermission } from '../../../auth/AuthProvider'
import { createHistory, pushHistory, redoHistory, undoHistory, type HistoryState } from '../../../forms/designer/historyStore'
import {
  FormFieldTypeLabelsAr,
  FormFieldTypes,
  createEmptySchema,
  reindexOrders,
  type FormFieldSchema,
  type FormSchemaDocument,
  type FormSectionSchema,
} from '../../../forms/designer/schemaTypes'
import { FormPreviewPanel } from '../../../forms/designer/FormPreviewPanel'

type SaveStatus = 'idle' | 'dirty' | 'saving' | 'saved' | 'error' | 'conflict'

function SortableRow({
  id,
  children,
}: Readonly<{ id: string; children: React.ReactNode }>) {
  const { attributes, listeners, setNodeRef, transform, transition } = useSortable({ id })
  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
  }
  return (
    <div ref={setNodeRef} style={style} className="designer-row" {...attributes}>
      <button type="button" className="secondary designer-handle" aria-label="سحب لإعادة الترتيب" {...listeners}>
        ⋮⋮
      </button>
      <div className="designer-row-body">{children}</div>
    </div>
  )
}

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

export function FormDesignerPage() {
  const canDesign = usePermission('Forms.UpdateDraft')
  const { formId, versionId } = useParams<{ formId: string; versionId: string }>()
  const qc = useQueryClient()
  const [history, setHistory] = useState<HistoryState | null>(null)
  const [selectedPageId, setSelectedPageId] = useState<string | null>(null)
  const [selectedFieldId, setSelectedFieldId] = useState<string | null>(null)
  const [saveStatus, setSaveStatus] = useState<SaveStatus>('idle')
  const [saveError, setSaveError] = useState<string | null>(null)
  const [issues, setIssues] = useState<Array<{ code: string; path: string; messageAr: string; severity: number }>>([])
  const [previewMode, setPreviewMode] = useState<'desktop' | 'tablet' | 'mobile' | null>(null)
  const [rowVersion, setRowVersion] = useState('')
  const lastSavedJson = useRef('')
  const abortRef = useRef<AbortController | null>(null)
  const debounceRef = useRef<number | null>(null)

  const versionQuery = useQuery({
    queryKey: ['form-version', formId, versionId],
    queryFn: () => api.forms.getVersion(formId!, versionId!),
    enabled: canDesign && !!formId && !!versionId,
  })

  useEffect(() => {
    if (!versionQuery.data) return
    let schema: FormSchemaDocument
    try {
      schema = JSON.parse(versionQuery.data.draftSchemaJson) as FormSchemaDocument
      if (!schema.pages?.length) schema = createEmptySchema()
    } catch {
      schema = createEmptySchema()
    }
    setHistory(createHistory(schema))
    setSelectedPageId(schema.pages[0]?.id ?? null)
    setRowVersion(versionQuery.data.rowVersion)
    lastSavedJson.current = JSON.stringify(schema)
    setSaveStatus('idle')
  }, [versionQuery.data])

  const schema = history?.present
  const page = schema?.pages.find((p) => p.id === selectedPageId) ?? schema?.pages[0]
  const selectedField = page?.sections.flatMap((s) => s.fields).find((f) => f.id === selectedFieldId)

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  )

  const applySchema = (next: FormSchemaDocument) => {
    setHistory((h) => (h ? pushHistory(h, reindexOrders(next)) : h))
    setSaveStatus('dirty')
  }

  // Autosave
  useEffect(() => {
    if (!history || !formId || !versionId || !rowVersion) return
    const json = JSON.stringify(history.present)
    if (json === lastSavedJson.current) return
    if (debounceRef.current) window.clearTimeout(debounceRef.current)
    debounceRef.current = window.setTimeout(() => {
      abortRef.current?.abort()
      const controller = new AbortController()
      abortRef.current = controller
      setSaveStatus('saving')
      setSaveError(null)
      api.forms
        .autosaveSchema(formId, versionId, { schemaJson: json, rowVersion })
        .then((v) => {
          if (controller.signal.aborted) return
          setRowVersion(v.rowVersion)
          lastSavedJson.current = json
          setSaveStatus('saved')
          void qc.invalidateQueries({ queryKey: ['form-version', formId, versionId] })
        })
        .catch((err: ApiError) => {
          if (controller.signal.aborted) return
          if (err.status === 409) {
            setSaveStatus('conflict')
            setSaveError('تعارض تحديث. أعد تحميل أحدث نسخة.')
          } else {
            setSaveStatus('error')
            setSaveError(err.message || 'فشل الحفظ التلقائي.')
          }
        })
    }, 800)
    return () => {
      if (debounceRef.current) window.clearTimeout(debounceRef.current)
    }
  }, [history, formId, versionId, rowVersion, qc])

  const validateMutation = useMutation({
    mutationFn: () =>
      api.forms.validateVersion(formId!, versionId!, {
        schemaJson: JSON.stringify(history!.present),
        rowVersion,
      }),
    onSuccess: (result) => setIssues(result.issues),
  })

  const submitMutation = useMutation({
    mutationFn: () => api.forms.submitVersionReview(formId!, versionId!, { rowVersion, reason: 'إرسال للمراجعة' }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['form-version', formId, versionId] })
      window.location.assign(`/forms/${formId}/versions/${versionId}`)
    },
  })

  const onDragEnd = (event: DragEndEvent) => {
    if (!schema || !page) return
    const { active, over } = event
    if (!over || active.id === over.id) return
    const section = page.sections.find((s) => s.fields.some((f) => f.id === active.id))
    if (!section) return
    const oldIndex = section.fields.findIndex((f) => f.id === active.id)
    const newIndex = section.fields.findIndex((f) => f.id === over.id)
    if (oldIndex < 0 || newIndex < 0) return
    const nextSections = page.sections.map((s) =>
      s.id === section.id ? { ...s, fields: arrayMove(s.fields, oldIndex, newIndex) } : s,
    )
    applySchema({
      ...schema,
      pages: schema.pages.map((p) => (p.id === page.id ? { ...p, sections: nextSections } : p)),
    })
  }

  const moveFieldKeyboard = (fieldId: string, direction: -1 | 1) => {
    if (!schema || !page) return
    const section = page.sections.find((s) => s.fields.some((f) => f.id === fieldId))
    if (!section) return
    const index = section.fields.findIndex((f) => f.id === fieldId)
    const target = index + direction
    if (target < 0 || target >= section.fields.length) return
    const nextSections = page.sections.map((s) =>
      s.id === section.id ? { ...s, fields: arrayMove(s.fields, index, target) } : s,
    )
    applySchema({
      ...schema,
      pages: schema.pages.map((p) => (p.id === page.id ? { ...p, sections: nextSections } : p)),
    })
  }

  const addField = (type: number, section: FormSectionSchema) => {
    if (!schema || !page) return
    const field = newField(type)
    applySchema({
      ...schema,
      pages: schema.pages.map((p) =>
        p.id !== page.id
          ? p
          : {
              ...p,
              sections: p.sections.map((s) =>
                s.id === section.id ? { ...s, fields: [...s.fields, field] } : s,
              ),
            },
      ),
    })
    setSelectedFieldId(field.id)
  }

  const updateSelectedField = (patch: Partial<FormFieldSchema>) => {
    if (!schema || !page || !selectedField) return
    applySchema({
      ...schema,
      pages: schema.pages.map((p) =>
        p.id !== page.id
          ? p
          : {
              ...p,
              sections: p.sections.map((s) => ({
                ...s,
                fields: s.fields.map((f) => (f.id === selectedField.id ? { ...f, ...patch } : f)),
              })),
            },
      ),
    })
  }

  const palette = useMemo(() => Object.entries(FormFieldTypeLabelsAr).map(([k, label]) => ({ type: Number(k), label })), [])

  if (!canDesign) return <div className="error" role="alert">ليست لديك صلاحية تصميم النماذج.</div>
  if (versionQuery.isLoading || !history || !schema) return <div className="loading">جاري تحميل المصمم…</div>
  if (versionQuery.isError) {
    const err = versionQuery.error as ApiError
    return <div className="error" role="alert">{err.status === 404 ? 'الإصدار غير موجود.' : err.message}</div>
  }
  if (versionQuery.data && versionQuery.data.status !== 0 && versionQuery.data.status !== 2) {
    return (
      <div className="error" role="alert">
        لا يمكن تعديل هذا الإصدار في حالته الحالية.
        <Link to={`/forms/${formId}/versions/${versionId}`}>عودة</Link>
      </div>
    )
  }

  return (
    <div className="panel designer-shell" dir="rtl">
      <div className="page-header">
        <h1 className="page-title">مصمم النموذج — v{versionQuery.data?.versionNumber}</h1>
        <div className="toolbar">
          <button type="button" className="secondary" disabled={!history.past.length} onClick={() => setHistory((h) => (h ? undoHistory(h) : h))}>تراجع</button>
          <button type="button" className="secondary" disabled={!history.future.length} onClick={() => setHistory((h) => (h ? redoHistory(h) : h))}>إعادة</button>
          <button type="button" className="secondary" onClick={() => validateMutation.mutate()}>تحقق</button>
          <button type="button" className="secondary" onClick={() => setPreviewMode(previewMode ? null : 'desktop')}>معاينة</button>
          <button type="button" onClick={() => submitMutation.mutate()} disabled={submitMutation.isPending}>إرسال للمراجعة</button>
          <Link to={`/forms/${formId}/versions`} className="secondary">الإصدارات</Link>
        </div>
      </div>
      <div className="muted" aria-live="polite">
        الحالة: {saveStatus === 'saving' ? 'جاري الحفظ…' : saveStatus === 'saved' ? 'تم الحفظ' : saveStatus === 'dirty' ? 'تعديلات غير محفوظة' : saveStatus === 'conflict' ? 'تعارض' : saveStatus === 'error' ? 'خطأ' : 'جاهز'}
        {saveError ? ` — ${saveError}` : ''}
        {saveStatus === 'conflict' && (
          <button type="button" className="secondary" onClick={() => void versionQuery.refetch()}>إعادة التحميل</button>
        )}
      </div>

      {previewMode ? (
        <FormPreviewPanel
          schema={schema}
          mode={previewMode}
          onModeChange={setPreviewMode}
          onClose={() => setPreviewMode(null)}
        />
      ) : (
        <div className="designer-grid">
          <aside className="designer-palette" aria-label="مكونات الحقول">
            <h2 className="section-title">الحقول</h2>
            {palette.map((item) => (
              <button
                key={item.type}
                type="button"
                className="secondary"
                onClick={() => page?.sections[0] && addField(item.type, page.sections[0])}
              >
                {item.label}
              </button>
            ))}
          </aside>

          <section className="designer-canvas" aria-label="لوحة المخطط">
            <div className="designer-pages" role="tablist" aria-label="الصفحات">
              {schema.pages.map((p) => (
                <button
                  key={p.id}
                  type="button"
                  role="tab"
                  aria-selected={p.id === page?.id}
                  className={p.id === page?.id ? undefined : 'secondary'}
                  onClick={() => setSelectedPageId(p.id)}
                >
                  {p.titleAr}
                </button>
              ))}
              <button
                type="button"
                className="secondary"
                onClick={() => {
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
                }}
              >
                + صفحة
              </button>
            </div>

            {page && (
              <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={onDragEnd}>
                {page.sections.map((section) => (
                  <div key={section.id} className="panel-section">
                    <h3 className="section-title">{section.titleAr}</h3>
                    <SortableContext items={section.fields.map((f) => f.id)} strategy={verticalListSortingStrategy}>
                      {section.fields.map((field) => (
                        <SortableRow key={field.id} id={field.id}>
                          <button
                            type="button"
                            className={field.id === selectedFieldId ? undefined : 'secondary'}
                            onClick={() => setSelectedFieldId(field.id)}
                          >
                            {field.labelAr} <span className="muted">({FormFieldTypeLabelsAr[field.type]})</span>
                          </button>
                          <button type="button" className="secondary" aria-label="تحريك لأعلى" onClick={() => moveFieldKeyboard(field.id, -1)}>↑</button>
                          <button type="button" className="secondary" aria-label="تحريك لأسفل" onClick={() => moveFieldKeyboard(field.id, 1)}>↓</button>
                        </SortableRow>
                      ))}
                    </SortableContext>
                  </div>
                ))}
              </DndContext>
            )}
          </section>

          <aside className="designer-props" aria-label="خصائص الحقل">
            <h2 className="section-title">الخصائص</h2>
            {!selectedField ? (
              <div className="empty">اختر حقلًا لتعديل خصائصه.</div>
            ) : (
              <div className="form-grid">
                <label className="field">
                  التسمية العربية
                  <input value={selectedField.labelAr} onChange={(e) => updateSelectedField({ labelAr: e.target.value })} />
                </label>
                <label className="field">
                  المفتاح
                  <input value={selectedField.key} onChange={(e) => updateSelectedField({ key: e.target.value })} />
                </label>
                <label className="field">
                  <input
                    type="checkbox"
                    checked={selectedField.isRequired}
                    onChange={(e) => updateSelectedField({ isRequired: e.target.checked })}
                  />{' '}
                  إلزامي
                </label>
                <label className="field">
                  القيمة الافتراضية
                  <input
                    value={selectedField.defaultValue ?? ''}
                    onChange={(e) => updateSelectedField({ defaultValue: e.target.value })}
                  />
                </label>
              </div>
            )}
            <h2 className="section-title">التحقق</h2>
            {issues.length === 0 ? (
              <div className="muted">لا توجد مشاكل معروضة.</div>
            ) : (
              <ul>
                {issues.map((issue, i) => (
                  <li key={`${issue.code}-${i}`} className={issue.severity === 0 ? 'error' : 'muted'}>
                    {issue.messageAr} <span className="muted">({issue.path})</span>
                  </li>
                ))}
              </ul>
            )}
          </aside>
        </div>
      )}
    </div>
  )
}
