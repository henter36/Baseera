import type { ReactNode } from 'react'

export function ObservationMasterDetailLayout({
  listCollapsed,
  hasSelection,
  list,
  detail,
}: Readonly<{
  listCollapsed: boolean
  hasSelection: boolean
  list: ReactNode
  detail: ReactNode
}>) {
  return (
    <div
      className={`workspace-grid ${listCollapsed ? 'is-collapsed' : ''} ${hasSelection ? 'has-selection' : ''}`}
      data-testid="observation-master-detail-layout"
    >
      {list}
      {detail}
    </div>
  )
}

export function ObservationListPane({ children }: Readonly<{ children: ReactNode }>) {
  return (
    <aside className="workspace-list-pane" aria-label="قائمة الملاحظات" data-testid="observation-list-pane">
      {children}
    </aside>
  )
}

export function ObservationDetailPane({ children }: Readonly<{ children: ReactNode }>) {
  return (
    <main className="workspace-detail-pane" aria-live="polite" data-testid="observation-detail-pane">
      {children}
    </main>
  )
}
