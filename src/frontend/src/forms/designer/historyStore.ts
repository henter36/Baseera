import type { FormSchemaDocument } from './schemaTypes'
import { cloneSchema } from './schemaTypes'

const MAX_HISTORY = 50

export type HistoryState = {
  past: FormSchemaDocument[]
  present: FormSchemaDocument
  future: FormSchemaDocument[]
}

export function createHistory(initial: FormSchemaDocument): HistoryState {
  return { past: [], present: cloneSchema(initial), future: [] }
}

export function pushHistory(state: HistoryState, next: FormSchemaDocument): HistoryState {
  const past = [...state.past, cloneSchema(state.present)]
  if (past.length > MAX_HISTORY) past.shift()
  return { past, present: cloneSchema(next), future: [] }
}

export function undoHistory(state: HistoryState): HistoryState {
  if (state.past.length === 0) return state
  const past = [...state.past]
  const previous = past.pop()!
  return {
    past,
    present: previous,
    future: [cloneSchema(state.present), ...state.future],
  }
}

export function redoHistory(state: HistoryState): HistoryState {
  if (state.future.length === 0) return state
  const [next, ...rest] = state.future
  return {
    past: [...state.past, cloneSchema(state.present)],
    present: next,
    future: rest,
  }
}
