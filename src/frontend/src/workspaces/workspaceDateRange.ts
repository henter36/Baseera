const DATE_PART_FORMATTERS = new Map<string, Intl.DateTimeFormat>()
const OFFSET_FORMATTERS = new Map<string, Intl.DateTimeFormat>()

type LocalDateTimeParts = Readonly<{
  year: number
  month: number
  day: number
  hour: number
  minute: number
  second: number
  millisecond: number
}>

export function instantToDateInput(instantUtc: string, timeZone: string): string {
  const date = new Date(instantUtc)
  const parts = datePartFormatter(timeZone).formatToParts(date)
  const year = part(parts, 'year')
  const month = part(parts, 'month')
  const day = part(parts, 'day')
  return `${year}-${month}-${day}`
}

export function startOfLocalDateUtc(date: string, timeZone: string): string {
  const local = parseDateInput(date)
  return zonedLocalTimeToUtc(
    {
      year: local.year,
      month: local.month,
      day: local.day,
      hour: 0,
      minute: 0,
      second: 0,
      millisecond: 0,
    },
    timeZone,
  ).toISOString()
}

export function endOfLocalDateUtc(date: string, timeZone: string): string {
  const local = parseDateInput(date)
  const nextLocalDay = new Date(Date.UTC(local.year, local.month - 1, local.day + 1))
  const end = zonedLocalTimeToUtc(
    {
      year: nextLocalDay.getUTCFullYear(),
      month: nextLocalDay.getUTCMonth() + 1,
      day: nextLocalDay.getUTCDate(),
      hour: 0,
      minute: 0,
      second: 0,
      millisecond: 0,
    },
    timeZone,
  )
  end.setUTCMilliseconds(end.getUTCMilliseconds() - 1)
  return end.toISOString()
}

function zonedLocalTimeToUtc(local: LocalDateTimeParts, timeZone: string) {
  const localAsUtc = localPartsAsUtcMilliseconds(local)
  let utc = localAsUtc
  for (let attempt = 0; attempt < 4; attempt += 1) {
    const offset = timeZoneOffsetMilliseconds(new Date(utc), timeZone)
    const nextUtc = localAsUtc - offset
    if (nextUtc === utc) {
      break
    }

    utc = nextUtc
  }

  return new Date(utc)
}

function localPartsAsUtcMilliseconds(local: LocalDateTimeParts) {
  return Date.UTC(
    local.year,
    local.month - 1,
    local.day,
    local.hour,
    local.minute,
    local.second,
    local.millisecond,
  )
}

function timeZoneOffsetMilliseconds(date: Date, timeZone: string) {
  const name = offsetFormatter(timeZone)
    .formatToParts(date)
    .find((item) => item.type === 'timeZoneName')?.value ?? 'GMT'
  if (name === 'GMT' || name === 'UTC') {
    return 0
  }

  const match = /^GMT(?<sign>[+-])(?<hour>\d{1,2})(?::?(?<minute>\d{2}))?$/.exec(name)
  if (!match?.groups) {
    throw new Error(`Unsupported time zone offset format: ${name}`)
  }

  const sign = match.groups.sign === '-' ? -1 : 1
  const hours = Number.parseInt(match.groups.hour, 10)
  const minutes = Number.parseInt(match.groups.minute ?? '0', 10)
  return sign * ((hours * 60) + minutes) * 60 * 1000
}

function parseDateInput(value: string) {
  const match = /^(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})$/.exec(value)
  if (!match?.groups) {
    throw new Error('Invalid date input value.')
  }

  return {
    year: Number.parseInt(match.groups.year, 10),
    month: Number.parseInt(match.groups.month, 10),
    day: Number.parseInt(match.groups.day, 10),
  }
}

function datePartFormatter(timeZone: string) {
  const existing = DATE_PART_FORMATTERS.get(timeZone)
  if (existing) {
    return existing
  }

  const formatter = new Intl.DateTimeFormat('en-CA', {
    timeZone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  })
  DATE_PART_FORMATTERS.set(timeZone, formatter)
  return formatter
}

function offsetFormatter(timeZone: string) {
  const existing = OFFSET_FORMATTERS.get(timeZone)
  if (existing) {
    return existing
  }

  const formatter = new Intl.DateTimeFormat('en-US', {
    timeZone,
    timeZoneName: 'shortOffset',
  })
  OFFSET_FORMATTERS.set(timeZone, formatter)
  return formatter
}

function part(parts: Intl.DateTimeFormatPart[], type: Intl.DateTimeFormatPartTypes) {
  return parts.find((item) => item.type === type)?.value ?? ''
}
