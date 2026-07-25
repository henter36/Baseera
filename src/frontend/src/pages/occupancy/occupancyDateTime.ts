const RIYADH_UTC_OFFSET_MINUTES = 3 * 60

export function riyadhDateTimeLocalToUtc(value: string): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/.exec(value)
  if (!match) {
    throw new Error('صيغة التاريخ والوقت غير صحيحة.')
  }

  const [, year, month, day, hour, minute] = match
  return new Date(Date.UTC(
    Number(year),
    Number(month) - 1,
    Number(day),
    Number(hour),
    Number(minute) - RIYADH_UTC_OFFSET_MINUTES,
    0,
    0,
  )).toISOString()
}
