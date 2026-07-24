import { describe, expect, it } from 'vitest'
import { endOfLocalDateUtc, instantToDateInput, startOfLocalDateUtc } from './workspaceDateRange'

describe('workspaceDateRange', () => {
  it('converts Asia/Riyadh local date start to the correct UTC instant', () => {
    expect(startOfLocalDateUtc('2026-07-10', 'Asia/Riyadh')).toBe('2026-07-09T21:00:00.000Z')
  })

  it('computes end of local day as the next local start minus one millisecond', () => {
    expect(endOfLocalDateUtc('2026-07-10', 'Asia/Riyadh')).toBe('2026-07-10T20:59:59.999Z')
  })

  it('handles daylight saving changes for local day bounds', () => {
    expect(startOfLocalDateUtc('2026-03-08', 'America/New_York')).toBe('2026-03-08T05:00:00.000Z')
    expect(endOfLocalDateUtc('2026-03-08', 'America/New_York')).toBe('2026-03-09T03:59:59.999Z')
  })

  it('renders UTC instants into date inputs through the workspace timezone', () => {
    expect(instantToDateInput('2026-07-09T21:30:00.000Z', 'Asia/Riyadh')).toBe('2026-07-10')
    expect(instantToDateInput('2026-07-10T01:00:00.000Z', 'America/New_York')).toBe('2026-07-09')
  })
})
