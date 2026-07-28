import { Link } from 'react-router'

export function ObservationWorkspaceHeader({
  source,
  listCollapsed,
  canCreate,
  onToggleList,
}: Readonly<{
  source: string
  listCollapsed: boolean
  canCreate: boolean
  onToggleList: () => void
}>) {
  return (
    <header className="workspace-topbar">
      <div>
        <h1 className="page-title">مساحة عمل الملاحظات</h1>
        <p className="muted">قائمة، تفاصيل، إجراءات، تكليفات، أدلة وسجل زمني من صفحة واحدة.</p>
        {source.startsWith('facility:') && (
          <Link className="muted" to={`/workspaces/facilities/${source.slice('facility:'.length)}`}>
            ← العودة إلى مساحة عمل السجن
          </Link>
        )}
      </div>
      <div className="workspace-topbar-actions">
        <button type="button" className="secondary" onClick={onToggleList}>
          {listCollapsed ? 'إظهار القائمة' : 'طي القائمة'}
        </button>
        {canCreate && (
          <Link to="/notes/new" className="button-link primary">
            ملاحظة جديدة
          </Link>
        )}
      </div>
    </header>
  )
}
