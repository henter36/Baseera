import { FormFieldTypeLabelsAr } from './schemaTypes'
import { diffSchemas, type FieldDiffEntry } from './versionDiff'
import type { FormSchemaDocument } from './schemaTypes'

const KIND_LABELS_AR: Record<FieldDiffEntry['kind'], string> = {
  added: 'مضاف',
  removed: 'محذوف',
  modified: 'معدّل',
  unchanged: 'بدون تغيير',
}

function labelFor(entry: FieldDiffEntry): string {
  const field = entry.after ?? entry.before
  return field ? `${field.labelAr} (${FormFieldTypeLabelsAr[field.type]})` : entry.key
}

export function VersionCompare({
  beforeSchema,
  afterSchema,
  beforeLabelAr,
  afterLabelAr,
}: Readonly<{
  beforeSchema: FormSchemaDocument
  afterSchema: FormSchemaDocument
  beforeLabelAr: string
  afterLabelAr: string
}>) {
  const entries = diffSchemas(beforeSchema, afterSchema)
  const changed = entries.filter((e) => e.kind !== 'unchanged')

  return (
    <div className="studio-diff-table">
      <p className="muted">مقارنة {beforeLabelAr} ← {afterLabelAr}</p>
      {changed.length === 0 ? (
        <div className="empty">لا فروقات بين الإصدارين.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>الحقل</th>
              <th>الحالة</th>
              <th>الخصائص المعدّلة</th>
              <th>الخيارات</th>
              <th>الشرط</th>
              <th>الصيغة</th>
              <th>الإلزام</th>
            </tr>
          </thead>
          <tbody>
            {changed.map((entry) => (
              <tr key={entry.key}>
                <td>{labelFor(entry)}</td>
                <td className={entry.kind}>{KIND_LABELS_AR[entry.kind]}</td>
                <td>{entry.changedProperties.join('، ') || '—'}</td>
                <td>{entry.optionChanges.join('، ') || '—'}</td>
                <td>{entry.conditionChanged ? 'تغيّر' : '—'}</td>
                <td>{entry.formulaChanged ? 'تغيّرت' : '—'}</td>
                <td>{entry.requiredChanged ? 'تغيّر' : '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}
