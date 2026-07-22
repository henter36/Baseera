import { FormFieldTypeLabelsAr } from './schemaTypes'

type DesignerPaletteProps = {
  onAddField: (type: number) => void
}

export function DesignerPalette({ onAddField }: Readonly<DesignerPaletteProps>) {
  const palette = Object.entries(FormFieldTypeLabelsAr).map(([k, label]) => ({ type: Number(k), label }))

  return (
    <aside className="designer-palette" aria-label="مكونات الحقول">
      <h2 className="section-title">الحقول</h2>
      {palette.map((item) => (
        <button key={item.type} type="button" className="secondary" onClick={() => onAddField(item.type)}>
          {item.label}
        </button>
      ))}
    </aside>
  )
}
