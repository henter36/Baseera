import { reindexOrders, type FormFieldSchema, type FormPageSchema, type FormSchemaDocument, type FormSectionSchema } from './schemaTypes'

function cloneWithNewIds(field: FormFieldSchema): FormFieldSchema {
  const id = crypto.randomUUID()
  return {
    ...field,
    id,
    key: `${field.key}_copy_${id.slice(0, 4)}`,
    choice: field.choice ? { ...field.choice, options: field.choice.options.map((o) => ({ ...o })) } : field.choice,
    repeatingTable: field.repeatingTable
      ? { ...field.repeatingTable, columns: field.repeatingTable.columns.map(cloneWithNewIds) }
      : field.repeatingTable,
  }
}

export function renamePageTitle(schema: FormSchemaDocument, pageId: string, titleAr: string): FormSchemaDocument {
  return { ...schema, pages: schema.pages.map((p) => (p.id === pageId ? { ...p, titleAr } : p)) }
}

export function renameSectionTitle(schema: FormSchemaDocument, sectionId: string, titleAr: string): FormSchemaDocument {
  return {
    ...schema,
    pages: schema.pages.map((page) => ({
      ...page,
      sections: page.sections.map((s) => (s.id === sectionId ? { ...s, titleAr } : s)),
    })),
  }
}

export function addSection(schema: FormSchemaDocument, pageId: string): FormSchemaDocument {
  const id = crypto.randomUUID()
  return {
    ...schema,
    pages: schema.pages.map((page) =>
      page.id !== pageId
        ? page
        : {
            ...page,
            sections: [
              ...page.sections,
              { id, key: `section_${id.slice(0, 8)}`, titleAr: `قسم ${page.sections.length + 1}`, order: page.sections.length, fields: [] },
            ],
          },
    ),
  }
}

export function duplicateSection(schema: FormSchemaDocument, pageId: string, sectionId: string): FormSchemaDocument {
  const id = crypto.randomUUID()
  return reindexOrders({
    ...schema,
    pages: schema.pages.map((page) => {
      if (page.id !== pageId) return page
      const source = page.sections.find((s) => s.id === sectionId)
      if (!source) return page
      const copy: FormSectionSchema = {
        ...source,
        id,
        key: `${source.key}_copy_${id.slice(0, 4)}`,
        titleAr: `${source.titleAr} (نسخة)`,
        fields: source.fields.map(cloneWithNewIds),
      }
      return { ...page, sections: [...page.sections, copy] }
    }),
  })
}

export function deleteSection(schema: FormSchemaDocument, pageId: string, sectionId: string): FormSchemaDocument {
  return reindexOrders({
    ...schema,
    pages: schema.pages.map((page) =>
      page.id !== pageId ? page : { ...page, sections: page.sections.filter((s) => s.id !== sectionId) },
    ),
  })
}

export function duplicateField(schema: FormSchemaDocument, pageId: string, sectionId: string, fieldId: string): FormSchemaDocument {
  return reindexOrders({
    ...schema,
    pages: schema.pages.map((page) => {
      if (page.id !== pageId) return page
      return {
        ...page,
        sections: page.sections.map((section) => {
          if (section.id !== sectionId) return section
          const index = section.fields.findIndex((f) => f.id === fieldId)
          if (index === -1) return section
          const copy = cloneWithNewIds(section.fields[index])
          const fields = [...section.fields]
          fields.splice(index + 1, 0, copy)
          return { ...section, fields }
        }),
      }
    }),
  })
}

export function deleteField(schema: FormSchemaDocument, pageId: string, sectionId: string, fieldId: string): FormSchemaDocument {
  return reindexOrders({
    ...schema,
    pages: schema.pages.map((page) => {
      if (page.id !== pageId) return page
      return {
        ...page,
        sections: page.sections.map((section) =>
          section.id !== sectionId ? section : { ...section, fields: section.fields.filter((f) => f.id !== fieldId) },
        ),
      }
    }),
  })
}

export function canDeletePage(schema: FormSchemaDocument): boolean {
  return schema.pages.length > 1
}

export function deletePage(schema: FormSchemaDocument, pageId: string): FormSchemaDocument {
  if (!canDeletePage(schema)) {
    return schema
  }
  return reindexOrders({ ...schema, pages: schema.pages.filter((p) => p.id !== pageId) })
}

export function duplicatePage(schema: FormSchemaDocument, pageId: string): FormSchemaDocument {
  const id = crypto.randomUUID()
  const source = schema.pages.find((p) => p.id === pageId)
  if (!source) return schema
  const copy: FormPageSchema = {
    ...source,
    id,
    key: `${source.key}_copy_${id.slice(0, 4)}`,
    titleAr: `${source.titleAr} (نسخة)`,
    sections: source.sections.map((section) => {
      const sectionId = crypto.randomUUID()
      return { ...section, id: sectionId, key: `${section.key}_copy_${sectionId.slice(0, 4)}`, fields: section.fields.map(cloneWithNewIds) }
    }),
  }
  return reindexOrders({ ...schema, pages: [...schema.pages, copy] })
}

export function moveFieldAcrossSections(
  schema: FormSchemaDocument,
  pageId: string,
  fromSectionId: string,
  toSectionId: string,
  fieldId: string,
): FormSchemaDocument {
  if (fromSectionId === toSectionId) {
    return schema
  }

  return reindexOrders({
    ...schema,
    pages: schema.pages.map((page) => {
      if (page.id !== pageId) return page
      const fromSection = page.sections.find((s) => s.id === fromSectionId)
      const field = fromSection?.fields.find((f) => f.id === fieldId)
      if (!field) return page
      return {
        ...page,
        sections: page.sections.map((section) => {
          if (section.id === fromSectionId) {
            return { ...section, fields: section.fields.filter((f) => f.id !== fieldId) }
          }
          if (section.id === toSectionId) {
            return { ...section, fields: [...section.fields, field] }
          }
          return section
        }),
      }
    }),
  })
}
