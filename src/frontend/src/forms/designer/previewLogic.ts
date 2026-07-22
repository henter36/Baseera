import type { FormConditionGroup, FormFieldSchema, FormFormulaNode, FormSchemaDocument } from './schemaTypes'

function isEmpty(value: unknown): boolean {
  if (value == null) {
    return true
  }

  if (typeof value === 'string') {
    return value.trim() === ''
  }

  if (Array.isArray(value)) {
    return value.length === 0
  }

  return false
}

export function toDisplayString(value: unknown): string {
  if (value == null) {
    return ''
  }

  if (typeof value === 'string') {
    return value
  }

  if (typeof value === 'number' || typeof value === 'boolean') {
    return String(value)
  }

  if (typeof value === 'object') {
    try {
      return JSON.stringify(value)
    } catch {
      return ''
    }
  }

  return String(value)
}

function toComparableString(value: unknown): string {
  if (value == null) {
    return ''
  }

  return String(value)
}

function compareNumbers(current: unknown, expected: unknown, compare: (left: number, right: number) => boolean): boolean {
  const left = Number(current)
  const right = Number(expected)
  if (Number.isNaN(left) || Number.isNaN(right)) {
    return false
  }

  return compare(left, right)
}

function evaluatePredicate(
  predicate: FormConditionGroup['predicates'][number],
  values: Record<string, unknown>,
): boolean {
  const current = values[predicate.fieldKey]
  const expected = predicate.value ?? ''
  const currentText = toComparableString(current)
  const expectedText = toComparableString(expected)

  switch (predicate.operator) {
    case 0:
      return currentText === expectedText
    case 1:
      return currentText !== expectedText
    case 2:
      return compareNumbers(current, predicate.value, (left, right) => left > right)
    case 3:
      return compareNumbers(current, predicate.value, (left, right) => left >= right)
    case 4:
      return compareNumbers(current, predicate.value, (left, right) => left < right)
    case 5:
      return compareNumbers(current, predicate.value, (left, right) => left <= right)
    case 6:
      return currentText.includes(expectedText)
    case 7:
      return !currentText.includes(expectedText)
    case 8:
      return isEmpty(current)
    case 9:
      return !isEmpty(current)
    case 10:
      return current === true || current === 'true' || current === 1
    case 11:
      return current === false || current === 'false' || current === 0
    case 12:
      return (predicate.values ?? []).map(String).includes(currentText)
    case 13:
      return !(predicate.values ?? []).map(String).includes(currentText)
    case 14:
      return currentText < expectedText
    case 15:
      return currentText > expectedText
    default:
      return false
  }
}

export function evaluateCondition(
  group: FormConditionGroup | null | undefined,
  values: Record<string, unknown>,
): boolean {
  if (!group) {
    return true
  }

  const predicateOk = group.predicates.map((predicate) => evaluatePredicate(predicate, values))
  const nestedOk = group.groups.map((nested) => evaluateCondition(nested, values))
  const all = [...predicateOk, ...nestedOk]
  if (all.length === 0) {
    return true
  }

  return group.combinator === 1 ? all.some(Boolean) : all.every(Boolean)
}

const BINARY_OPERATORS: Record<number, (left: number, right: number) => unknown> = {
  0: (left, right) => left + right,
  1: (left, right) => left - right,
  2: (left, right) => left * right,
  3: (left, right) => (right === 0 ? null : left / right),
  4: (left, right) => (right === 0 ? null : left % right),
}

const FORMULA_FUNCTIONS: Record<number, (args: unknown[]) => unknown> = {
  0: (args) => {
    const nums = args.map(Number).filter((value) => !Number.isNaN(value))
    return nums.length ? Math.min(...nums) : null
  },
  1: (args) => {
    const nums = args.map(Number).filter((value) => !Number.isNaN(value))
    return nums.length ? Math.max(...nums) : null
  },
  2: (args) => args.map(Number).filter((value) => !Number.isNaN(value)).reduce((sum, value) => sum + value, 0),
  3: (args) => {
    const nums = args.map(Number).filter((value) => !Number.isNaN(value))
    return nums.length ? nums.reduce((sum, value) => sum + value, 0) / nums.length : null
  },
  4: (args) => {
    const first = Number(args[0])
    return Number.isNaN(first) ? null : Math.round(first)
  },
  5: (args) => {
    const first = Number(args[0])
    return Number.isNaN(first) ? null : Math.floor(first)
  },
  6: (args) => {
    const first = Number(args[0])
    return Number.isNaN(first) ? null : Math.ceil(first)
  },
  7: (args) => {
    const first = Number(args[0])
    return Number.isNaN(first) ? null : Math.abs(first)
  },
  8: (args) => args.find((value) => value != null && value !== '') ?? null,
  9: (args) => args.map((value) => toDisplayString(value)).join(''),
}

function evaluateBinary(node: Extract<FormFormulaNode, { kind: 'binary' }>, values: Record<string, unknown>): unknown {
  const operator = BINARY_OPERATORS[node.operator]
  if (!operator) {
    return null
  }

  const left = Number(evaluateFormula(node.left, values) ?? 0)
  const right = Number(evaluateFormula(node.right, values) ?? 0)
  return operator(left, right)
}

function evaluateFunction(node: Extract<FormFormulaNode, { kind: 'function' }>, values: Record<string, unknown>): unknown {
  const handler = FORMULA_FUNCTIONS[node.function]
  if (!handler) {
    return null
  }

  const args = node.arguments.map((argument) => evaluateFormula(argument, values))
  return handler(args)
}

export function evaluateFormula(node: FormFormulaNode | null | undefined, values: Record<string, unknown>): unknown {
  if (!node) {
    return null
  }

  switch (node.kind) {
    case 'constantNumber':
      return node.value
    case 'constantText':
      return node.value
    case 'fieldReference':
      return values[node.fieldKey]
    case 'binary':
      return evaluateBinary(node, values)
    case 'function':
      return evaluateFunction(node, values)
  }
}

export function collectVisibleFields(schema: FormSchemaDocument, values: Record<string, unknown>): FormFieldSchema[] {
  const fields: FormFieldSchema[] = []
  for (const page of schema.pages) {
    if (!evaluateCondition(page.visibilityCondition, values)) {
      continue
    }

    for (const section of page.sections) {
      if (!evaluateCondition(section.visibilityCondition, values)) {
        continue
      }

      for (const field of section.fields) {
        if (!evaluateCondition(field.visibilityCondition, values)) {
          continue
        }

        fields.push(field)
      }
    }
  }

  return fields
}
