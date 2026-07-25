export function getFormString(data: FormData, key: string): string {
  const value = data.get(key)
  return typeof value === 'string' ? value : ''
}

export function getOptionalFormString(
  data: FormData,
  key: string,
): string | undefined {
  const value = getFormString(data, key).trim()
  return value || undefined
}
