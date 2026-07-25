import { describe, expect, it } from 'vitest'
import { riyadhDateTimeLocalToUtc } from './occupancyDateTime'

describe('riyadhDateTimeLocalToUtc', () => {
  it('converts Riyadh datetime-local values to explicit UTC instants', () => {
    expect(riyadhDateTimeLocalToUtc('2026-07-24T14:30')).toBe('2026-07-24T11:30:00.000Z')
  })
})
