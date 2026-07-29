import type { FormConditionGroup, FormFieldSchema, FormFormulaNode, FormSchemaDocument } from './schemaTypes'

export type FlatField = { field: FormFieldSchema; pageId: string; sectionId: string }

export function flattenFields(schema: FormSchemaDocument): FlatField[] {
  const result: FlatField[] = []
  for (const page of schema.pages) {
    for (const section of page.sections) {
      for (const field of section.fields) {
        result.push({ field, pageId: page.id, sectionId: section.id })
      }
    }
  }
  return result
}

function conditionFieldKeys(group: FormConditionGroup | null | undefined): string[] {
  if (!group) return []
  return [
    ...group.predicates.map((p) => p.fieldKey.toLowerCase()),
    ...group.groups.flatMap((g) => conditionFieldKeys(g)),
  ]
}

function formulaFieldKeys(node: FormFormulaNode | null | undefined): string[] {
  if (!node) return []
  switch (node.kind) {
    case 'fieldReference':
      return [node.fieldKey.toLowerCase()]
    case 'binary':
      return [...formulaFieldKeys(node.left), ...formulaFieldKeys(node.right)]
    case 'function':
      return node.arguments.flatMap((arg) => formulaFieldKeys(arg))
    default:
      return []
  }
}

/** All field keys a single field's own condition/formula settings refer to. */
export function fieldOwnDependencies(field: FormFieldSchema): string[] {
  return [
    ...conditionFieldKeys(field.visibilityCondition),
    ...conditionFieldKeys(field.requiredCondition),
    ...formulaFieldKeys(field.formula),
  ]
}

export type DependentReference = {
  field: FormFieldSchema
  via: 'visibilityCondition' | 'requiredCondition' | 'formula'
}

/** Fields elsewhere in the schema whose condition/formula depends on the given field's key. */
export function findDependents(schema: FormSchemaDocument, fieldKey: string): DependentReference[] {
  const lower = fieldKey.toLowerCase()
  const dependents: DependentReference[] = []
  for (const { field } of flattenFields(schema)) {
    if (conditionFieldKeys(field.visibilityCondition).includes(lower)) {
      dependents.push({ field, via: 'visibilityCondition' })
    }
    if (conditionFieldKeys(field.requiredCondition).includes(lower)) {
      dependents.push({ field, via: 'requiredCondition' })
    }
    if (formulaFieldKeys(field.formula).includes(lower)) {
      dependents.push({ field, via: 'formula' })
    }
  }

  return dependents
}

/**
 * Builds a directed graph of "field A's formula/condition depends on field B" and detects cycles.
 * Mirrors the intent of the server-side FormDependencyGraph so the builder UI can warn before a
 * server round trip; the server's validateVersion call remains the final source of truth.
 */
export function detectDependencyCycle(schema: FormSchemaDocument): string[] | null {
  const fields = flattenFields(schema).map((f) => f.field)
  const byKey = new Map(fields.map((f) => [f.key.toLowerCase(), f]))
  const visiting = new Set<string>()
  const visited = new Set<string>()
  const stack: string[] = []

  function visit(key: string): string[] | null {
    const lower = key.toLowerCase()
    if (visiting.has(lower)) {
      const cycleStart = stack.indexOf(lower)
      return stack.slice(cycleStart === -1 ? 0 : cycleStart).concat(lower)
    }
    if (visited.has(lower)) {
      return null
    }

    const field = byKey.get(lower)
    if (!field) {
      return null
    }

    visiting.add(lower)
    stack.push(lower)
    for (const dep of fieldOwnDependencies(field)) {
      const cycle = visit(dep)
      if (cycle) {
        return cycle
      }
    }
    stack.pop()
    visiting.delete(lower)
    visited.add(lower)
    return null
  }

  for (const field of fields) {
    const cycle = visit(field.key)
    if (cycle) {
      return cycle
    }
  }

  return null
}

export function wouldCreateSelfReference(fieldKey: string, referencedKey: string): boolean {
  return fieldKey.trim().toLowerCase() === referencedKey.trim().toLowerCase()
}
