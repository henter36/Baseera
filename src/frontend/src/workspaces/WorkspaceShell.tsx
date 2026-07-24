import { Link } from 'react-router-dom'
import type {
  DataFreshness,
  WorkspaceAllowedAction,
  WorkspaceConfidence,
  WorkspaceContext,
  WorkspaceDefinition,
  WorkspaceDrillDownTarget,
  WorkspaceWidgetDefinition,
  WorkspaceWidgetEnvelope,
} from '../api/client'

const DATE_FORMAT = new Intl.DateTimeFormat('ar-SA', {
  timeZone: 'Asia/Riyadh',
  year: 'numeric',
  month: 'short',
  day: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
})

export function WorkspaceShell({
  definition,
  context,
  freshness,
  confidence,
  generatedAtUtc,
  allowedActions,
  widgetFailures,
  children,
}: Readonly<{
  definition: WorkspaceDefinition
  context: WorkspaceContext
  freshness: DataFreshness
  confidence: WorkspaceConfidence
  generatedAtUtc: string
  allowedActions: WorkspaceAllowedAction[]
  widgetFailures?: Array<{ widgetKey: string; messageAr: string; isPartialSafe: boolean }>
  children: React.ReactNode
}>) {
  return (
    <section className="workspace-shell" dir="rtl">
      <WorkspaceHeader
        definition={definition}
        context={context}
        freshness={freshness}
        confidence={confidence}
        generatedAtUtc={generatedAtUtc}
        allowedActions={allowedActions}
      />
      {widgetFailures && widgetFailures.length > 0 && (
        <div className="workspace-warning" role="status">
          بعض الأدوات لم تكتمل: {widgetFailures.map((failure) => failure.messageAr).join('، ')}
        </div>
      )}
      {children}
    </section>
  )
}

export function WorkspaceHeader({
  definition,
  context,
  freshness,
  confidence,
  generatedAtUtc,
  allowedActions,
}: Readonly<{
  definition: WorkspaceDefinition
  context: WorkspaceContext
  freshness: DataFreshness
  confidence: WorkspaceConfidence
  generatedAtUtc: string
  allowedActions: WorkspaceAllowedAction[]
}>) {
  return (
    <header className="workspace-shared-header">
      <div>
        <p className="muted">{workspaceLevelLabel(context.level)} · {context.scopeLabelAr}</p>
        <h1 className="page-title">{definition.titleAr}</h1>
        <div className="workspace-header-meta">
          <span>آخر توليد {formatDate(generatedAtUtc)}</span>
          <span className="badge" data-tone={freshnessTone(freshness.status)}>{freshness.labelAr}</span>
          <span className="badge" data-tone={confidenceTone(confidence.level)}>ثقة {confidence.labelAr}</span>
          {definition.features.isReferenceOnly && <span className="badge" data-tone="muted">مرجعية</span>}
        </div>
      </div>
      <WorkspaceActionBar allowedActions={allowedActions} />
    </header>
  )
}

export function WorkspaceActionBar({ allowedActions }: Readonly<{ allowedActions: WorkspaceAllowedAction[] }>) {
  if (allowedActions.length === 0) {
    return null
  }

  return (
    <div className="workspace-shared-actions" aria-label="إجراءات مساحة العمل">
      {allowedActions.map((action) => (
        <button
          key={action.code}
          type="button"
          className="secondary"
          disabled={!action.enabled}
          title={action.disabledReasonAr ?? undefined}
        >
          {action.labelAr}
        </button>
      ))}
    </div>
  )
}

export function WorkspaceWidgetContainer({
  definition,
  data,
  children,
}: Readonly<{
  definition: WorkspaceWidgetDefinition
  data?: WorkspaceWidgetEnvelope
  children: React.ReactNode
}>) {
  return (
    <article className={`workspace-widget size-${definition.defaultSize}`} aria-labelledby={`${definition.key}-title`}>
      <header>
        <div>
          <h2 id={`${definition.key}-title`}>{definition.titleAr}</h2>
          {definition.descriptionAr && <p className="muted">{definition.descriptionAr}</p>}
        </div>
        <div className="workspace-widget-badges">
          {definition.containsSensitiveData && <span className="badge" data-tone="warn">حساس</span>}
          {data && <span className="badge" data-tone={freshnessTone(data.freshness.status)}>{data.freshness.labelAr}</span>}
        </div>
      </header>
      {data?.warningMessages.map((warning) => <div key={warning} className="workspace-warning">{warning}</div>)}
      {children}
      {data && data.drillDownTargets.length > 0 && (
        <footer>
          {data.drillDownTargets.map((target) => <DrillDownLink key={target.routeKey} target={target} />)}
        </footer>
      )}
    </article>
  )
}

export function WorkspaceFilterBar({
  fromUtc,
  toUtc,
  onChange,
  onReset,
}: Readonly<{
  fromUtc: string
  toUtc: string
  onChange: (next: { fromUtc: string; toUtc: string }) => void
  onReset: () => void
}>) {
  return (
    <form className="workspace-filter-bar" role="search" onSubmit={(event) => event.preventDefault()}>
      <label>
        <span>من</span>
        <input type="date" value={toDateInput(fromUtc)} onChange={(event) => onChange({ fromUtc: fromDateInput(event.target.value), toUtc })} />
      </label>
      <label>
        <span>إلى</span>
        <input type="date" value={toDateInput(toUtc)} onChange={(event) => onChange({ fromUtc, toUtc: endOfDateInput(event.target.value) })} />
      </label>
      <button type="button" className="secondary" onClick={onReset}>إعادة ضبط</button>
    </form>
  )
}

export function MasterDetailWorkspaceLayout({
  list,
  detail,
  hasSelection,
  onBack,
}: Readonly<{
  list: React.ReactNode
  detail: React.ReactNode
  hasSelection: boolean
  onBack: () => void
}>) {
  return (
    <div className={`master-detail-layout ${hasSelection ? 'has-selection' : ''}`}>
      <aside>{list}</aside>
      <main>
        {hasSelection && <button type="button" className="secondary mobile-back" onClick={onBack}>رجوع إلى القائمة</button>}
        {detail}
      </main>
    </div>
  )
}

export function WorkspaceLoading() {
  return <div className="workspace-loading" aria-busy="true">جاري تحميل مساحة العمل…</div>
}

export function WorkspaceEmpty({ message }: Readonly<{ message: string }>) {
  return <div className="empty">{message}</div>
}

export function WorkspaceError({ message, onRetry }: Readonly<{ message: string; onRetry?: () => void }>) {
  return (
    <div className="error" role="alert">
      <span>{message}</span>
      {onRetry && <button type="button" className="secondary" onClick={onRetry}>إعادة المحاولة</button>}
    </div>
  )
}

export function WorkspaceUnauthorized() {
  return <div className="error" role="alert">ليست لديك صلاحية عرض مساحة العمل.</div>
}

function DrillDownLink({ target }: Readonly<{ target: WorkspaceDrillDownTarget }>) {
  const route = routeForTarget(target)
  if (!route) {
    return <span className="muted">{target.labelAr}</span>
  }

  return <Link to={route}><button type="button" className="secondary">{target.labelAr}</button></Link>
}

function routeForTarget(target: WorkspaceDrillDownTarget) {
  if (target.routeKey === 'dashboard.operations') {
    return '/dashboard'
  }

  if (target.routeKey === 'corrective-actions.list') {
    return '/corrective-actions'
  }

  return ''
}

function workspaceLevelLabel(level: number) {
  if (level === 1) return 'منشأة'
  if (level === 2) return 'منطقة'
  if (level === 3) return 'المركز'
  return 'نطاق تخصصي'
}

function freshnessTone(status: number) {
  if (status === 1) return 'ok'
  if (status === 2 || status === 5) return 'warn'
  if (status === 3) return 'danger'
  return 'muted'
}

function confidenceTone(level: number) {
  if (level === 1) return 'ok'
  if (level === 2) return 'warn'
  if (level === 3) return 'danger'
  return 'muted'
}

function formatDate(value: string) {
  return DATE_FORMAT.format(new Date(value))
}

function toDateInput(value: string) {
  return value.slice(0, 10)
}

function fromDateInput(value: string) {
  return `${value}T00:00:00.000Z`
}

function endOfDateInput(value: string) {
  return `${value}T23:59:59.999Z`
}
