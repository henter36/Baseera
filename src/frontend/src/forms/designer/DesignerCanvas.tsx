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
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { FormFieldTypeLabelsAr, type FormFieldSchema, type FormPageSchema, type FormSchemaDocument } from './schemaTypes'

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
      <button type="button" className="designer-handle" aria-label="سحب لإعادة الترتيب" {...listeners}>
        ⋮⋮
      </button>
      <div className="designer-row-body">{children}</div>
    </div>
  )
}

type DesignerCanvasProps = {
  schema: FormSchemaDocument
  page: FormPageSchema | undefined
  selectedPageId: string | null
  selectedFieldId: string | null
  onSelectPage: (pageId: string) => void
  onAddPage: () => void
  onSelectField: (fieldId: string) => void
  onDragEnd: (event: DragEndEvent, page: FormPageSchema | undefined) => void
  onMoveField: (fieldId: string, direction: -1 | 1, page: FormPageSchema | undefined) => void
}

export function DesignerCanvas({
  schema,
  page,
  selectedPageId,
  selectedFieldId,
  onSelectPage,
  onAddPage,
  onSelectField,
  onDragEnd,
  onMoveField,
}: Readonly<DesignerCanvasProps>) {
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  )

  return (
    <section className="designer-canvas" aria-label="لوحة المخطط">
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
            {p.titleAr}
          </button>
        ))}
        <button type="button" className="secondary" onClick={onAddPage}>+ صفحة</button>
      </div>

      {page && (
        <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={(event) => onDragEnd(event, page)}>
          {page.sections.map((section) => (
            <div key={section.id} className="panel-section">
              <h3 className="section-title">{section.titleAr}</h3>
              <SortableContext items={section.fields.map((f) => f.id)} strategy={verticalListSortingStrategy}>
                {section.fields.map((field) => (
                  <FieldRow
                    key={field.id}
                    field={field}
                    isSelected={field.id === selectedFieldId}
                    onSelect={() => onSelectField(field.id)}
                    onMoveUp={() => onMoveField(field.id, -1, page)}
                    onMoveDown={() => onMoveField(field.id, 1, page)}
                  />
                ))}
              </SortableContext>
            </div>
          ))}
        </DndContext>
      )}
    </section>
  )
}

function FieldRow({
  field,
  isSelected,
  onSelect,
  onMoveUp,
  onMoveDown,
}: Readonly<{
  field: FormFieldSchema
  isSelected: boolean
  onSelect: () => void
  onMoveUp: () => void
  onMoveDown: () => void
}>) {
  return (
    <SortableRow id={field.id}>
      <button type="button" className={isSelected ? undefined : 'secondary'} onClick={onSelect}>
        {field.labelAr} <span className="muted">({FormFieldTypeLabelsAr[field.type]})</span>
      </button>
      <button type="button" className="secondary" aria-label="تحريك لأعلى" onClick={onMoveUp}>↑</button>
      <button type="button" className="secondary" aria-label="تحريك لأسفل" onClick={onMoveDown}>↓</button>
    </SortableRow>
  )
}
