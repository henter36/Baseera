/** Shared list-page presentation helpers for notes/forms list screens. */
export function formatListDate(value?: string | null): string {
  if (!value) return '—'
  return new Date(value).toLocaleString('ar-SA')
}

export function listSortIndicator(columnKey: string, sortBy: string, sortDesc: boolean): string {
  if (sortBy !== columnKey) return ''
  return sortDesc ? '↓' : '↑'
}

export function nextListSortState(
  currentSortBy: string,
  currentSortDesc: boolean,
  columnKey: string,
): { sortBy: string; sortDesc: boolean } {
  if (currentSortBy === columnKey) {
    return { sortBy: currentSortBy, sortDesc: !currentSortDesc }
  }
  return { sortBy: columnKey, sortDesc: true }
}

export function listQueryErrorMessage(
  error: unknown,
  forbiddenMessage: string,
  fallbackMessage: string,
): string | null {
  if (!error) return null
  const err = error as { status?: number; message?: string }
  if (err.status === 403) return forbiddenMessage
  return err.message || fallbackMessage
}
