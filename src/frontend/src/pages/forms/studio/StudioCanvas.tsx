import { useState } from 'react'
import {
  DndContext,
  PointerSensor,
  KeyboardSensor,
  closestCenter,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core'
import { SortableContext, sortableKeyboardCoordinates, useSortable, verticalListSortingStrategy } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import type { DependentReference } from '../../../forms/designer/fieldDependencies'
import { FormFieldTypeLabelsAr, type FormFieldSchema, type FormPageSchema, type FormSchemaDocument } from '../../../forms/designer/schemaTypes'

function InlineEditableText({
  value,
  onCommit,
  ariaLabel,
  tag = 'span',
}: Readonly<{ value: string; onCommit: (next: string) => void; ariaLabel: string; tag?: 'span' | 'h2' | 'h3' }>) {
  const [draft, setDraft] = useState(value)
  const [editing, setEditing] = useState(false)

  if (!editing) {
    const Tag = tag
    return (
      <Tag>
        <button
          type="button"
          className="inline-edit-trigger"
          aria-label={`تعديل ${ariaLabel}`}
          onClick={() => { setDraft(value); setEditing(true) }}
        >
          {value || '(بلا عنوان)'}
        </button>
      </Tag>
    )
  }

  const commit = () => {
    setEditing(false)
    const trimmed = draft.trim()
    if (trimmed && trimmed !== value) {
      onCommit(trimmed)
    }
  }

  return (
    <input
      autoFocus
      aria-label={ariaLabel}
      value={draft}
      onChange={(e) => setDraft(e.target.value)}
      onBlur={commit}
      onKeyDown={(e) => {
        if (e.key === 'Enter') { e.currentTarget.blur() }
        if (e.key === 'Escape') { setDraft(value); setEditing(false) }
      }}
    />
  )
}

function SortableRow({ id, children }: Readonly<{ id: string; children: React.ReactNode }>) {
  const { attributes, listeners, setNodeRef, setActivatorNodeRef, transform, transition } = useSortable({ id })
  const style = { transform: CSS.Transform.toString(transform), transition }
  return (
    <div ref={setNodeRef} style={style} className="designer-row">
      <button type="button" ref={setActivatorNodeRef} className="designer-handle" aria-label="سحب لإعادة الترتيب" {...attributes} {...listeners}>
        ⋮⋮
      </button>
      <div className="designer-row-body">{children}</div>
    </div>
  )
}

type FieldRowProps = {
  field: FormFieldSchema
  isSelected: boolean
  hasError: boolean
  hasWarning: boolean
  fieldDependents: (fieldKey: string) => DependentReference[]
  onSelect: () => void
  onMoveUp: () => void
  onMoveDown: () => void
  onDuplicate: () => void
  onDelete: () => void
  onRenameLabel: (label: string) => void
}

function FieldRow({ field, isSelected, hasError, hasWarning, fieldDependents, onSelect, onMoveUp, onMoveDown, onDuplicate, onDelete, onRenameLabel }: Readonly<FieldRowProps>) {
  const [confirmingDelete, setConfirmingDelete] = useState(false)
  const [editingLabel, setEditingLabel] = useState(false)
  const [labelDraft, setLabelDraft] = useState(field.labelAr)
  // Computed lazily (only once the user actually opens the delete-confirmation), not on every
  // render, since finding dependents walks the whole schema — see StudioCanvas's fieldDependents.
  const dependents = confirmingDelete ? fieldDependents(field.key) : []

  const commitLabel = () => {
    setEditingLabel(false)
    const trimmed = labelDraft.trim()
    if (trimmed && trimmed !== field.labelAr) {
      onRenameLabel(trimmed)
    }
  }

  return (
    <SortableRow id={field.id}>
      <div className={isSelected ? 'studio-field-row studio-field-row-selected' : 'studio-field-row'}>
        {editingLabel ? (
          <input
            autoFocus
            aria-label={`عنوان الحقل ${field.labelAr}`}
            value={labelDraft}
            onChange={(e) => setLabelDraft(e.target.value)}
            onBlur={commitLabel}
            onKeyDown={(e) => {
              if (e.key === 'Enter') e.currentTarget.blur()
              if (e.key === 'Escape') { setLabelDraft(field.labelAr); setEditingLabel(false) }
            }}
          />
        ) : (
          <button type="button" className={isSelected ? undefined : 'secondary'} aria-pressed={isSelected} onClick={onSelect}>
            {field.labelAr}
          </button>
        )}
        {!editingLabel && (
          <button type="button" className="secondary" aria-label="تعديل عنوان الحقل" onClick={() => { setLabelDraft(field.labelAr); setEditingLabel(true) }}>✎</button>
        )}
        <span className="muted">({FormFieldTypeLabelsAr[field.type]})</span>
        <span className="studio-inspector-badges">
          {field.isRequired && <span className="badge" data-tone="warn">إلزامي</span>}
          {field.visibilityCondition && <span className="badge">شرط</span>}
          {field.formula && <span className="badge">صيغة</span>}
          {hasError && <span className="badge" data-tone="danger">خطأ</span>}
          {hasWarning && <span className="badge" data-tone="warn">تحذير</span>}
        </span>
      </div>
      <button type="button" className="secondary" aria-label="تحريك لأعلى" onClick={onMoveUp}>↑</button>
      <button type="button" className="secondary" aria-label="تحريك لأسفل" onClick={onMoveDown}>↓</button>
      <button type="button" className="secondary" aria-label="نسخ الحقل" onClick={onDuplicate}>نسخ</button>
      {!confirmingDelete && (
        <button type="button" className="secondary" aria-label="حذف الحقل" onClick={() => setConfirmingDelete(true)}>حذف</button>
      )}
      {confirmingDelete && (
        <span className="warn" role="alert">
          {dependents.length > 0 ? (
            <>
              يُستخدم هذا الحقل في {dependents.length} شرط/صيغة أخرى ({dependents.map((d) => d.field.labelAr).join('، ')}).
              حذفه سيجعل هذه العناصر تشير إلى حقل محذوف.
            </>
          ) : (
            'تأكيد حذف هذا الحقل؟'
          )}
          <button type="button" onClick={() => { setConfirmingDelete(false); onDelete() }}>تأكيد الحذف</button>
          <button type="button" className="secondary" onClick={() => setConfirmingDelete(false)}>إلغاء</button>
        </span>
      )}
    </SortableRow>
  )
}

export type StudioCanvasProps = {
  schema: FormSchemaDocument
  page: FormPageSchema | undefined
  selectedPageId: string | null
  selectedFieldId: string | null
  fieldIssueTone: (fieldId: string) => { hasError: boolean; hasWarning: boolean }
  fieldDependents: (fieldKey: string) => DependentReference[]
  onSelectPage: (pageId: string) => void
  onSelectField: (fieldId: string) => void
  onAddPage: () => void
  onDuplicatePage: (pageId: string) => void
  onDeletePage: (pageId: string) => void
  canDeletePage: boolean
  onRenamePageTitle: (pageId: string, title: string) => void
  onAddSection: (pageId: string) => void
  onDuplicateSection: (pageId: string, sectionId: string) => void
  onDeleteSection: (pageId: string, sectionId: string) => void
  onRenameSectionTitle: (sectionId: string, title: string) => void
  onDragEnd: (event: DragEndEvent, page: FormPageSchema | undefined) => void
  onMoveField: (fieldId: string, direction: -1 | 1, page: FormPageSchema | undefined) => void
  onDuplicateField: (pageId: string, sectionId: string, fieldId: string) => void
  onDeleteField: (pageId: string, sectionId: string, fieldId: string) => void
  onRenameFieldLabel: (pageId: string, fieldId: string, label: string) => void
}

export function StudioCanvas({
  schema,
  page,
  selectedPageId,
  selectedFieldId,
  fieldIssueTone,
  fieldDependents,
  onSelectPage,
  onSelectField,
  onAddPage,
  onDuplicatePage,
  onDeletePage,
  canDeletePage,
  onRenamePageTitle,
  onAddSection,
  onDuplicateSection,
  onDeleteSection,
  onRenameSectionTitle,
  onDragEnd,
  onMoveField,
  onDuplicateField,
  onDeleteField,
  onRenameFieldLabel,
}: Readonly<StudioCanvasProps>) {
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  )

  return (
    <section className="studio-canvas" aria-label="Canvas النموذج">
      <div className="designer-pages" role="tablist" aria-label="الصفحات">
        {schema.pages.map((p) => (
          <button
            key={p.id}
            type="button"
            role="tab"
            aria-selected={p.id === selectedPageId}
            className={p.id === selectedPageId ? undefined : 'secondary'}
            onClick={() => onSelectPage(p.id)}
          >
            {p.titleAr} <span className="studio-outline-count">({p.sections.reduce((n, s) => n + s.fields.length, 0)})</span>
          </button>
        ))}
        <button type="button" className="secondary" onClick={onAddPage}>+ صفحة</button>
      </div>

      {page && (
        <>
          <div className="page-header">
            <InlineEditableText tag="h2" value={page.titleAr} onCommit={(next) => onRenamePageTitle(page.id, next)} ariaLabel="عنوان الصفحة" />
            <div className="toolbar">
              <button type="button" className="secondary" onClick={() => onAddSection(page.id)}>+ قسم</button>
              <button type="button" className="secondary" onClick={() => onDuplicatePage(page.id)}>نسخ الصفحة</button>
              <button
                type="button"
                className="secondary"
                disabled={!canDeletePage}
                title={canDeletePage ? undefined : 'لا يمكن حذف الصفحة الوحيدة في النموذج.'}
                onClick={() => onDeletePage(page.id)}
              >
                حذف الصفحة
              </button>
            </div>
          </div>

          <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={(event) => onDragEnd(event, page)}>
            {page.sections.map((section) => (
              <div key={section.id} className="panel-section">
                <div className="page-header">
                  <InlineEditableText tag="h3" value={section.titleAr} onCommit={(next) => onRenameSectionTitle(section.id, next)} ariaLabel="عنوان القسم" />
                  <div className="toolbar">
                    <button type="button" className="secondary" onClick={() => onDuplicateSection(page.id, section.id)}>نسخ القسم</button>
                    <button
                      type="button"
                      className="secondary"
                      disabled={section.fields.length > 0}
                      title={section.fields.length > 0 ? 'لا يمكن حذف قسم يحتوي على حقول.' : undefined}
                      onClick={() => onDeleteSection(page.id, section.id)}
                    >
                      حذف القسم
                    </button>
                  </div>
                </div>
                {section.fields.length === 0 ? (
                  <div className="empty">لا توجد حقول في هذا القسم بعد. أضف حقلًا من مكتبة الحقول.</div>
                ) : (
                  <SortableContext items={section.fields.map((f) => f.id)} strategy={verticalListSortingStrategy}>
                    {section.fields.map((field) => {
                      const tone = fieldIssueTone(field.id)
                      return (
                        <FieldRow
                          key={field.id}
                          field={field}
                          isSelected={field.id === selectedFieldId}
                          hasError={tone.hasError}
                          hasWarning={tone.hasWarning}
                          fieldDependents={fieldDependents}
                          onSelect={() => onSelectField(field.id)}
                          onMoveUp={() => onMoveField(field.id, -1, page)}
                          onMoveDown={() => onMoveField(field.id, 1, page)}
                          onDuplicate={() => onDuplicateField(page.id, section.id, field.id)}
                          onDelete={() => onDeleteField(page.id, section.id, field.id)}
                          onRenameLabel={(label) => onRenameFieldLabel(page.id, field.id, label)}
                        />
                      )
                    })}
                  </SortableContext>
                )}
              </div>
            ))}
          </DndContext>
        </>
      )}
    </section>
  )
}
