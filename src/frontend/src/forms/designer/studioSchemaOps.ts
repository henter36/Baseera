import {
  reindexOrders,
  type FormConditionGroup,
  type FormFieldSchema,
  type FormFormulaNode,
  type FormPageSchema,
  type FormSchemaDocument,
  type FormSectionSchema,
} from './schemaTypes'

function remapConditionGroup<T extends FormConditionGroup | null | undefined>(group: T, keyMap: Map<string, string>): T {
  if (!group) return group
  return {
    ...group,
    predicates: group.predicates.map((p) => ({ ...p, fieldKey: keyMap.get(p.fieldKey) ?? p.fieldKey })),
    groups: group.groups.map((g) => remapConditionGroup(g, keyMap)),
  } as T
}

function remapFormulaNode<T extends FormFormulaNode | null | undefined>(node: T, keyMap: Map<string, string>): T {
  if (!node) return node
  switch (node.kind) {
    case 'fieldReference':
      return { ...node, fieldKey: keyMap.get(node.fieldKey) ?? node.fieldKey } as T
    case 'binary':
      return { ...node, left: remapFormulaNode(node.left, keyMap), right: remapFormulaNode(node.right, keyMap) } as T
    case 'function':
      return { ...node, arguments: node.arguments.map((a) => remapFormulaNode(a, keyMap)) } as T
    default:
      return node
  }
}

/** Precomputes old-key -> new-key for every field being duplicated together (including nested
 * repeating-table columns), so visibilityCondition/requiredCondition/formula references between
 * fields in the same duplicated group can be rewritten to point at their copies rather than the
 * untouched originals. */
function collectFieldKeyMap(fields: FormFieldSchema[], map: Map<string, string> = new Map()): Map<string, string> {
  for (const field of fields) {
    const newId = crypto.randomUUID()
    map.set(field.key, `${field.key}_copy_${newId.slice(0, 4)}`)
    if (field.repeatingTable) {
      collectFieldKeyMap(field.repeatingTable.columns, map)
    }
  }
  return map
}

function cloneWithNewIds(field: FormFieldSchema, keyMap: Map<string, string>): FormFieldSchema {
  const id = crypto.randomUUID()
  return {
    ...field,
    id,
    key: keyMap.get(field.key) ?? `${field.key}_copy_${id.slice(0, 4)}`,
    choice: field.choice ? { ...field.choice, options: field.choice.options.map((o) => ({ ...o })) } : field.choice,
    repeatingTable: field.repeatingTable
      ? { ...field.repeatingTable, columns: field.repeatingTable.columns.map((c) => cloneWithNewIds(c, keyMap)) }
      : field.repeatingTable,
    visibilityCondition: remapConditionGroup(field.visibilityCondition, keyMap),
    requiredCondition: remapConditionGroup(field.requiredCondition, keyMap),
    formula: remapFormulaNode(field.formula, keyMap),
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
      const keyMap = collectFieldKeyMap(source.fields)
      const copy: FormSectionSchema = {
        ...source,
        id,
        key: `${source.key}_copy_${id.slice(0, 4)}`,
        titleAr: `${source.titleAr} (نسخة)`,
        visibilityCondition: remapConditionGroup(source.visibilityCondition, keyMap),
        fields: source.fields.map((f) => cloneWithNewIds(f, keyMap)),
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
          const source = section.fields[index]
          const keyMap = collectFieldKeyMap(source.repeatingTable ? source.repeatingTable.columns : [])
          const copy = cloneWithNewIds(source, keyMap)
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
  const keyMap = source.sections.reduce((map, section) => collectFieldKeyMap(section.fields, map), new Map<string, string>())
  const copy: FormPageSchema = {
    ...source,
    id,
    key: `${source.key}_copy_${id.slice(0, 4)}`,
    titleAr: `${source.titleAr} (نسخة)`,
    visibilityCondition: remapConditionGroup(source.visibilityCondition, keyMap),
    sections: source.sections.map((section) => {
      const sectionId = crypto.randomUUID()
      return {
        ...section,
        id: sectionId,
        key: `${section.key}_copy_${sectionId.slice(0, 4)}`,
        visibilityCondition: remapConditionGroup(section.visibilityCondition, keyMap),
        fields: section.fields.map((f) => cloneWithNewIds(f, keyMap)),
      }
    }),
  }
  return reindexOrders({ ...schema, pages: [...schema.pages, copy] })
}

export function appendNewPage(schema: FormSchemaDocument): { schema: FormSchemaDocument; pageId: string } {
  const pageId = crypto.randomUUID()
  const sectionId = crypto.randomUUID()
  return {
    pageId,
    schema: {
      ...schema,
      pages: [
        ...schema.pages,
        {
          id: pageId,
          key: `page_${pageId.slice(0, 8)}`,
          titleAr: `صفحة ${schema.pages.length + 1}`,
          order: schema.pages.length,
          sections: [{ id: sectionId, key: `section_${sectionId.slice(0, 8)}`, titleAr: 'قسم', order: 0, fields: [] }],
        },
      ],
    },
  }
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
