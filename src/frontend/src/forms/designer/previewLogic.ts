import type { FormConditionGroup, FormFieldSchema, FormFormulaNode, FormSchemaDocument } from './schemaTypes'

function isEmpty(value: unknown): boolean {
  if (value == null) return true
  if (typeof value === 'string') return value.trim() === ''
  if (Array.isArray(value)) return value.length === 0
  return false
}

export function evaluateCondition(
  group: FormConditionGroup | null | undefined,
  values: Record<string, unknown>,
): boolean {
  if (!group) return true
  const predicateOk = group.predicates.map((p) => {
    const current = values[p.fieldKey]
    switch (p.operator) {
      case 0: return String(current ?? '') === String(p.value ?? '')
      case 1: return String(current ?? '') !== String(p.value ?? '')
      case 2: return Number(current) > Number(p.value)
      case 3: return Number(current) >= Number(p.value)
      case 4: return Number(current) < Number(p.value)
      case 5: return Number(current) <= Number(p.value)
      case 6: return String(current ?? '').includes(String(p.value ?? ''))
      case 7: return !String(current ?? '').includes(String(p.value ?? ''))
      case 8: return isEmpty(current)
      case 9: return !isEmpty(current)
      case 10: return current === true || current === 'true' || current === 1
      case 11: return current === false || current === 'false' || current === 0
      case 12: return (p.values ?? []).map(String).includes(String(current ?? ''))
      case 13: return !(p.values ?? []).map(String).includes(String(current ?? ''))
      case 14: return String(current ?? '') < String(p.value ?? '')
      case 15: return String(current ?? '') > String(p.value ?? '')
      default: return false
    }
  })
  const nestedOk = group.groups.map((g) => evaluateCondition(g, values))
  const all = [...predicateOk, ...nestedOk]
  if (all.length === 0) return true
  return group.combinator === 1 ? all.some(Boolean) : all.every(Boolean)
}

export function evaluateFormula(node: FormFormulaNode | null | undefined, values: Record<string, unknown>): unknown {
  if (!node) return null
  switch (node.kind) {
    case 'constantNumber': return node.value
    case 'constantText': return node.value
    case 'fieldReference': return values[node.fieldKey]
    case 'binary': {
      const left = Number(evaluateFormula(node.left, values) ?? 0)
      const right = Number(evaluateFormula(node.right, values) ?? 0)
      switch (node.operator) {
        case 0: return left + right
        case 1: return left - right
        case 2: return left * right
        case 3: return right === 0 ? null : left / right
        case 4: return right === 0 ? null : left % right
        default: return null
      }
    }
    case 'function': {
      const args = node.arguments.map((a) => evaluateFormula(a, values))
      const nums = args.map((a) => Number(a)).filter((n) => !Number.isNaN(n))
      switch (node.function) {
        case 0: return nums.length ? Math.min(...nums) : null
        case 1: return nums.length ? Math.max(...nums) : null
        case 2: return nums.reduce((s, n) => s + n, 0)
        case 3: return nums.length ? nums.reduce((s, n) => s + n, 0) / nums.length : null
        case 4: return nums[0] == null ? null : Math.round(nums[0])
        case 5: return nums[0] == null ? null : Math.floor(nums[0])
        case 6: return nums[0] == null ? null : Math.ceil(nums[0])
        case 7: return nums[0] == null ? null : Math.abs(nums[0])
        case 8: return args.find((a) => a != null && a !== '') ?? null
        case 9: return args.map((a) => String(a ?? '')).join('')
        default: return null
      }
    }
  }
}

export function collectVisibleFields(schema: FormSchemaDocument, values: Record<string, unknown>): FormFieldSchema[] {
  const fields: FormFieldSchema[] = []
  for (const page of schema.pages) {
    if (!evaluateCondition(page.visibilityCondition, values)) continue
    for (const section of page.sections) {
      if (!evaluateCondition(section.visibilityCondition, values)) continue
      for (const field of section.fields) {
        if (!evaluateCondition(field.visibilityCondition, values)) continue
        fields.push(field)
      }
    }
  }
  return fields
}
