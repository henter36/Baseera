import { describe, expect, it } from 'vitest'
import { createEmptySchema } from './schemaTypes'
import { createHistory, pushHistory, redoHistory, undoHistory } from './historyStore'

describe('designer history', () => {
  it('supports undo and redo without losing ids', () => {
    const initial = createEmptySchema()
    let state = createHistory(initial)
    const next = structuredClone(initial)
    next.pages[0].titleAr = 'صفحة معدلة'
    state = pushHistory(state, next)
    expect(state.present.pages[0].titleAr).toBe('صفحة معدلة')
    state = undoHistory(state)
    expect(state.present.pages[0].titleAr).toBe(initial.pages[0].titleAr)
    expect(state.present.pages[0].id).toBe(initial.pages[0].id)
    state = redoHistory(state)
    expect(state.present.pages[0].titleAr).toBe('صفحة معدلة')
  })
})
