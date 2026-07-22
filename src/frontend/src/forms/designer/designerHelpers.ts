import { useCallback } from 'react'
import { arrayMove } from '@dnd-kit/sortable'
import type { DragEndEvent } from '@dnd-kit/core'
import type { ApiError } from '../../api/client'
import { pushHistory, type HistoryState } from './historyStore'
import {
  isValidFieldKey,
  reindexOrders,
  type FormConditionGroup,
  type FormFieldSchema,
  type FormFormulaNode,
  type FormPageSchema,
  type FormSchemaDocument,
  type FormSectionSchema,
} from './schemaTypes'

export type RenameFieldKeyResult =
  | { ok: true; schema: FormSchemaDocument }
  | { ok: false; error: string }

export function mapFieldInPage(
  schema: FormSchemaDocument,
  pageId: string,
  mapFields: (fields: FormFieldSchema[]) => FormFieldSchema[],
): FormSchemaDocument {
  return {
    ...schema,
    pages: schema.pages.map((page) =>
      page.id !== pageId
        ? page
        : {
            ...page,
            sections: page.sections.map((section) => ({
              ...section,
              fields: mapFields(section.fields),
            })),
          },
    ),
  }
}

export function updateFieldInSchema(
  schema: FormSchemaDocument,
  pageId: string,
  fieldId: string,
  patch: Partial<FormFieldSchema>,
): FormSchemaDocument {
  return mapFieldInPage(schema, pageId, (fields) =>
    fields.map((field) => (field.id === fieldId ? { ...field, ...patch } : field)),
  )
}

function flattenFieldTree(fields: FormFieldSchema[]): FormFieldSchema[] {
  return fields.flatMap((field) => [
    field,
    ...flattenFieldTree(field.repeatingTable?.columns ?? []),
  ])
}

function getAllFields(schema: FormSchemaDocument): FormFieldSchema[] {
  return schema.pages.flatMap((page) =>
    page.sections.flatMap((section) => flattenFieldTree(section.fields)),
  )
}

function collectFieldKeys(schema: FormSchemaDocument, excludeFieldId: string): Set<string> {
  return new Set(
    getAllFields(schema)
      .filter((field) => field.id !== excludeFieldId)
      .map((field) => field.key.toLowerCase()),
  )
}

function findFieldKey(schema: FormSchemaDocument, fieldId: string): string | null {
  return getAllFields(schema).find((field) => field.id === fieldId)?.key ?? null
}

function mapConditionGroup(
  group: FormConditionGroup | null | undefined,
  oldKey: string,
  newKey: string,
): FormConditionGroup | null | undefined {
  if (!group) {
    return group
  }

  const oldLower = oldKey.toLowerCase()
  return {
    ...group,
    predicates: group.predicates.map((predicate) => ({
      ...predicate,
      fieldKey: predicate.fieldKey.toLowerCase() === oldLower ? newKey : predicate.fieldKey,
    })),
    groups: group.groups.map((nested) => mapConditionGroup(nested, oldKey, newKey)!),
  }
}

function mapFormulaNode(
  node: FormFormulaNode | null | undefined,
  oldKey: string,
  newKey: string,
): FormFormulaNode | null | undefined {
  if (!node) {
    return node
  }

  const oldLower = oldKey.toLowerCase()
  switch (node.kind) {
    case 'fieldReference':
      return {
        ...node,
        fieldKey: node.fieldKey.toLowerCase() === oldLower ? newKey : node.fieldKey,
      }
    case 'binary':
      return {
        ...node,
        left: mapFormulaNode(node.left, oldKey, newKey)!,
        right: mapFormulaNode(node.right, oldKey, newKey)!,
      }
    case 'function':
      return {
        ...node,
        arguments: node.arguments.map((argument) => mapFormulaNode(argument, oldKey, newKey)!),
      }
    default:
      return node
  }
}

function mapFieldReferences(field: FormFieldSchema, fieldId: string, oldKey: string, newKey: string): FormFieldSchema {
  const nextField: FormFieldSchema = {
    ...field,
    key: field.id === fieldId ? newKey : field.key,
    visibilityCondition: mapConditionGroup(field.visibilityCondition, oldKey, newKey) ?? field.visibilityCondition,
    requiredCondition: mapConditionGroup(field.requiredCondition, oldKey, newKey) ?? field.requiredCondition,
    formula: mapFormulaNode(field.formula, oldKey, newKey) ?? field.formula,
  }

  if (field.repeatingTable) {
    nextField.repeatingTable = {
      ...field.repeatingTable,
      columns: field.repeatingTable.columns.map((column) => mapFieldReferences(column, fieldId, oldKey, newKey)),
    }
  }

  return nextField
}

export function renameFieldKeyInSchema(
  schema: FormSchemaDocument,
  fieldId: string,
  newKey: string,
): RenameFieldKeyResult {
  const trimmed = newKey.trim()
  if (!trimmed) {
    return { ok: false, error: 'المفتاح مطلوب.' }
  }

  if (!isValidFieldKey(trimmed)) {
    return { ok: false, error: 'صيغة المفتاح غير صالحة.' }
  }

  const oldKey = findFieldKey(schema, fieldId)
  if (!oldKey) {
    return { ok: false, error: 'الحقل غير موجود.' }
  }

  if (oldKey.toLowerCase() === trimmed.toLowerCase()) {
    return { ok: true, schema }
  }

  const existingKeys = collectFieldKeys(schema, fieldId)
  if (existingKeys.has(trimmed.toLowerCase())) {
    return { ok: false, error: 'المفتاح مكرر.' }
  }

  return {
    ok: true,
    schema: {
      ...schema,
      pages: schema.pages.map((page) => ({
        ...page,
        visibilityCondition: mapConditionGroup(page.visibilityCondition, oldKey, trimmed) ?? page.visibilityCondition,
        sections: page.sections.map((section) => ({
          ...section,
          visibilityCondition: mapConditionGroup(section.visibilityCondition, oldKey, trimmed) ?? section.visibilityCondition,
          fields: section.fields.map((field) => mapFieldReferences(field, fieldId, oldKey, trimmed)),
        })),
      })),
    },
  }
}

export function useDesignerSchema(
  history: HistoryState | null,
  setHistory: React.Dispatch<React.SetStateAction<HistoryState | null>>,
  onDirty: () => void,
) {
  const applySchema = useCallback(
    (next: FormSchemaDocument) => {
      setHistory((h) => (h ? pushHistory(h, reindexOrders(next)) : h))
      onDirty()
    },
    [onDirty, setHistory],
  )

  const onDragEnd = useCallback(
    (event: DragEndEvent, page: FormPageSchema | undefined) => {
      if (!history?.present || !page) {
        return
      }

      const { active, over } = event
      if (!over || active.id === over.id) {
        return
      }

      const section = page.sections.find((s) => s.fields.some((f) => f.id === active.id))
      if (!section) {
        return
      }

      const oldIndex = section.fields.findIndex((f) => f.id === active.id)
      const newIndex = section.fields.findIndex((f) => f.id === over.id)
      if (oldIndex < 0 || newIndex < 0) {
        return
      }

      const nextSections = page.sections.map((s) =>
        s.id === section.id ? { ...s, fields: arrayMove(s.fields, oldIndex, newIndex) } : s,
      )
      applySchema({
        ...history.present,
        pages: history.present.pages.map((p) => (p.id === page.id ? { ...p, sections: nextSections } : p)),
      })
    },
    [applySchema, history?.present],
  )

  const moveFieldKeyboard = useCallback(
    (fieldId: string, direction: -1 | 1, page: FormPageSchema | undefined) => {
      if (!history?.present || !page) {
        return
      }

      const section = page.sections.find((s) => s.fields.some((f) => f.id === fieldId))
      if (!section) {
        return
      }

      const index = section.fields.findIndex((f) => f.id === fieldId)
      const target = index + direction
      if (target < 0 || target >= section.fields.length) {
        return
      }

      const nextSections = page.sections.map((s) =>
        s.id === section.id ? { ...s, fields: arrayMove(s.fields, index, target) } : s,
      )
      applySchema({
        ...history.present,
        pages: history.present.pages.map((p) => (p.id === page.id ? { ...p, sections: nextSections } : p)),
      })
    },
    [applySchema, history?.present],
  )

  const addField = useCallback(
    (field: FormFieldSchema, page: FormPageSchema, section: FormSectionSchema) => {
      if (!history?.present) {
        return
      }

      applySchema({
        ...history.present,
        pages: history.present.pages.map((p) =>
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
      return field.id
    },
    [applySchema, history?.present],
  )

  return { applySchema, onDragEnd, moveFieldKeyboard, addField }
}

export function saveStatusLabel(status: string): string {
  switch (status) {
    case 'saving':
      return 'جاري الحفظ…'
    case 'saved':
      return 'تم الحفظ'
    case 'dirty':
      return 'تعديلات غير محفوظة'
    case 'conflict':
      return 'تعارض'
    case 'error':
      return 'خطأ'
    default:
      return 'جاهز'
  }
}

export function hasAllowedAction(actions: string[] | undefined, action: string): boolean {
  return actions?.includes(action) ?? false
}

export function formatApiError(err: ApiError): string {
  if (err.status === 404) {
    return 'العنصر غير موجود أو خارج نطاقك.'
  }

  if (err.status === 403) {
    return 'ليست لديك صلاحية تنفيذ هذه العملية.'
  }

  if (err.status === 409) {
    return 'تعارض أو انتقال غير صالح.'
  }

  if (typeof err.message === 'string' && err.message.length > 0) {
    return err.message
  }

  return 'حدث خطأ غير متوقع.'
}
