import type { ReactNode } from 'react'

// Shared across the different workspace panes (list/detail, and later Region/HQ workspaces per
// docs/ux-rescue/phase1a-observation-architecture.md): a single place for the loading/empty/error
// states every workspace pane needs, so each page doesn't reinvent its own markup and ARIA wiring.

export function WorkspaceSkeletonRows({ count = 5, className = 'observation-card-skeleton' }: Readonly<{ count?: number; className?: string }>) {
  return (
    <>
      {Array.from({ length: count }).map((_, index) => (
        // eslint-disable-next-line react/no-array-index-key -- skeleton placeholders, stable count
        <div key={index} className={className} aria-hidden="true" />
      ))}
    </>
  )
}

export function WorkspaceEmptyState({ title, hint }: Readonly<{ title: string; hint?: ReactNode }>) {
  return (
    <div className="empty">
      <strong>{title}</strong>
      {hint && <p className="muted">{hint}</p>}
    </div>
  )
}

export function WorkspaceErrorState({
  message,
  onRetry,
}: Readonly<{ message: string; onRetry?: () => void }>) {
  return (
    <div className="error" role="alert">
      <span>{message}</span>
      {onRetry && (
        <button type="button" className="secondary" onClick={onRetry}>
          إعادة المحاولة
        </button>
      )}
    </div>
  )
}
