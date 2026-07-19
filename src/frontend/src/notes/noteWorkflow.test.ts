import { describe, expect, it } from 'vitest'
import { NoteStatus } from './noteEnums'
import { getAllowedActions } from './noteWorkflow'

describe('getAllowedActions', () => {
  it('offers submit and cancel for Draft notes', () => {
    const kinds = getAllowedActions(NoteStatus.Draft, false).map((a) => a.kind)
    expect(kinds).toEqual(['submit', 'cancel'])
  })

  it('offers assign and cancel for Open notes', () => {
    const kinds = getAllowedActions(NoteStatus.Open, false).map((a) => a.kind)
    expect(kinds).toEqual(['assign', 'cancel'])
  })

  it('offers start work and reassign for Assigned notes', () => {
    const kinds = getAllowedActions(NoteStatus.Assigned, false).map((a) => a.kind)
    expect(kinds).toEqual(['startWork', 'reassign'])
  })

  it('offers submit for verification for InProgress notes', () => {
    const kinds = getAllowedActions(NoteStatus.InProgress, false).map((a) => a.kind)
    expect(kinds).toEqual(['submitForVerification'])
  })

  it('offers verify closure and return for rework for PendingVerification notes', () => {
    const kinds = getAllowedActions(NoteStatus.PendingVerification, false).map((a) => a.kind)
    expect(kinds).toEqual(['verifyClosure', 'returnForRework'])
  })

  it('offers reopen only for Closed notes', () => {
    const kinds = getAllowedActions(NoteStatus.Closed, false).map((a) => a.kind)
    expect(kinds).toEqual(['reopen'])
  })

  it('offers only assign for Reopened notes without a current assignment', () => {
    const kinds = getAllowedActions(NoteStatus.Reopened, false).map((a) => a.kind)
    expect(kinds).toEqual(['assign'])
  })

  it('offers assign and start work for Reopened notes with a current assignment', () => {
    const kinds = getAllowedActions(NoteStatus.Reopened, true).map((a) => a.kind)
    expect(kinds).toEqual(['assign', 'startWork'])
  })

  it('offers nothing for the terminal Cancelled status', () => {
    expect(getAllowedActions(NoteStatus.Cancelled, false)).toEqual([])
  })

  it('every action declares the exact backend permission code it requires', () => {
    const allStatuses = Object.values(NoteStatus)
    for (const status of allStatuses) {
      for (const action of getAllowedActions(status, true)) {
        expect(action.permission.startsWith('Notes.')).toBe(true)
      }
    }
  })
})
