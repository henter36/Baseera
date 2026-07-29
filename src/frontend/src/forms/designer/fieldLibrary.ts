import { FormFieldTypes, FormFieldTypeLabelsAr, type FormFieldType } from './schemaTypes'

export type FieldLibraryCategory = {
  key: string
  labelAr: string
  types: FormFieldType[]
}

// Categories map 1:1 onto Baseera.Domain.Forms.Schema.FormFieldType — every entry here is a
// real, backend-persistable, backend-validated type. No display-only variant (e.g. a separate
// "dropdown" vs "radio" type, or a distinct "integer" type) is invented client-side; those are
// rendering/settings choices on top of SingleChoice / Number, not separate domain types.
export const FIELD_LIBRARY_CATEGORIES: FieldLibraryCategory[] = [
  {
    key: 'text',
    labelAr: 'نص',
    types: [FormFieldTypes.ShortText, FormFieldTypes.LongText, FormFieldTypes.CalculatedText],
  },
  {
    key: 'choices',
    labelAr: 'اختيارات',
    types: [FormFieldTypes.SingleChoice, FormFieldTypes.MultipleChoice, FormFieldTypes.YesNo],
  },
  {
    key: 'numbers',
    labelAr: 'أرقام',
    types: [FormFieldTypes.Number, FormFieldTypes.Percentage, FormFieldTypes.CalculatedNumber],
  },
  {
    key: 'datetime',
    labelAr: 'تاريخ ووقت',
    types: [FormFieldTypes.Date, FormFieldTypes.Time, FormFieldTypes.DateTime],
  },
  {
    key: 'attachments',
    labelAr: 'مرفقات',
    types: [FormFieldTypes.File, FormFieldTypes.Image, FormFieldTypes.Signature],
  },
  {
    key: 'structure',
    labelAr: 'هيكلة',
    types: [FormFieldTypes.RepeatingTable, FormFieldTypes.Location],
  },
  {
    key: 'organizational',
    labelAr: 'حقول مؤسسية',
    types: [FormFieldTypes.OrganizationalReference],
  },
]

export type FieldLibraryEntry = {
  type: FormFieldType
  labelAr: string
  categoryKey: string
  categoryLabelAr: string
}

export const FIELD_LIBRARY_ENTRIES: FieldLibraryEntry[] = FIELD_LIBRARY_CATEGORIES.flatMap((category) =>
  category.types.map((type) => ({
    type,
    labelAr: FormFieldTypeLabelsAr[type],
    categoryKey: category.key,
    categoryLabelAr: category.labelAr,
  })),
)

export function searchFieldLibrary(query: string): FieldLibraryEntry[] {
  const trimmed = query.trim()
  if (!trimmed) {
    return FIELD_LIBRARY_ENTRIES
  }

  const normalized = trimmed.toLowerCase()
  return FIELD_LIBRARY_ENTRIES.filter(
    (entry) =>
      entry.labelAr.includes(trimmed) ||
      entry.categoryLabelAr.includes(trimmed) ||
      entry.labelAr.toLowerCase().includes(normalized),
  )
}
