import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ReferenceWorkspacePage } from './ReferenceWorkspacePage'

const { getWorkspace } = vi.hoisted(() => ({ getWorkspace: vi.fn() }))

vi.mock('../../auth/AuthProvider', () => ({
  usePermission: (code: string) => code === 'Workspaces.View',
}))

vi.mock('../../api/client', async () => {
  const actual = await vi.importActual<typeof import('../../api/client')>('../../api/client')
  return {
    ...actual,
    api: {
      ...actual.api,
      workspaces: {
        ...actual.api.workspaces,
        get: getWorkspace,
      },
    },
  }
})

describe('ReferenceWorkspacePage', () => {
  beforeEach(() => {
    getWorkspace.mockReset()
    getWorkspace.mockResolvedValue(shell)
  })

  it('renders real workspace widgets returned by the server', async () => {
    renderPage('/workspaces/reference?fromUtc=2026-07-01T00:00:00.000Z&toUtc=2026-07-24T23:59:59.999Z')

    expect(await screen.findByRole('heading', { name: 'مساحة عمل مرجعية' })).toBeInTheDocument()
    expect(screen.getByText('الملاحظات المفتوحة')).toBeInTheDocument()
    expect(screen.getByText('الإجراءات التصحيحية')).toBeInTheDocument()
    expect(screen.getByText(/تعذر تحميل أداة تجريبية/)).toBeInTheDocument()
    expect(getWorkspace).toHaveBeenCalledWith('reference', expect.objectContaining({ level: 4 }))
  })

  it('synchronizes date filters with server-side query parameters', async () => {
    renderPage('/workspaces/reference')
    await screen.findByRole('heading', { name: 'مساحة عمل مرجعية' })

    fireEvent.change(screen.getByLabelText('من'), { target: { value: '2026-07-10' } })

    await waitFor(() => {
      expect(getWorkspace.mock.calls.at(-1)?.[1]).toMatchObject({ fromUtc: '2026-07-09T21:00:00.000Z' })
    })
  })
})

function renderPage(initialEntry: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[initialEntry]}>
        <ReferenceWorkspacePage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

const widgetDefinitions = [
  {
    key: 'dashboard.operational-summary',
    titleAr: 'الملخص التشغيلي',
    titleEn: 'Operational Summary',
    descriptionAr: 'مؤشرات الملاحظات',
    category: 1,
    supportedLevels: [4],
    requiredPermission: 'Dashboard.ViewOperational',
    requiredDataCapability: 'OperationalDashboard.Summary',
    defaultSize: 4,
    minSize: 2,
    maxSize: 4,
    refreshPolicy: { minimumRefreshSeconds: 60, supportsManualRefresh: true },
    dataFreshnessPolicy: { currentForSeconds: 300, delayedAfterSeconds: 1800, staleAfterSeconds: 3600 },
    emptyErrorBehavior: { emptyMessageAr: '', errorMessageAr: '', allowPartialFailure: true },
    supportsDrillDown: true,
    isConfigurable: false,
    containsSensitiveData: false,
    isEnabled: true,
  },
  {
    key: 'dashboard.corrective-actions-summary',
    titleAr: 'الإجراءات التصحيحية',
    titleEn: 'Corrective Actions Summary',
    descriptionAr: 'مؤشرات الإجراءات',
    category: 4,
    supportedLevels: [4],
    requiredPermission: 'Dashboard.ViewCorrectiveActions',
    requiredDataCapability: 'OperationalDashboard.CorrectiveActions',
    defaultSize: 2,
    minSize: 1,
    maxSize: 4,
    refreshPolicy: { minimumRefreshSeconds: 60, supportsManualRefresh: true },
    dataFreshnessPolicy: { currentForSeconds: 300, delayedAfterSeconds: 1800, staleAfterSeconds: 3600 },
    emptyErrorBehavior: { emptyMessageAr: '', errorMessageAr: '', allowPartialFailure: true },
    supportsDrillDown: true,
    isConfigurable: false,
    containsSensitiveData: false,
    isEnabled: true,
  },
] as const

const shell = {
  definition: {
    key: 'reference',
    titleAr: 'مساحة عمل مرجعية',
    titleEn: 'Reference Workspace',
    supportedLevels: [4],
    requiredPermissions: ['Workspaces.View'],
    registeredWidgets: ['dashboard.operational-summary', 'dashboard.corrective-actions-summary'],
    defaultLayout: { items: [], version: 1 },
    availableFilters: [],
    supportedDrillDowns: [],
    features: { supportsSavedViews: false, supportsWidgetConfiguration: false, supportsExport: false, isReferenceOnly: true },
    version: 1,
  },
  context: {
    workspaceKey: 'reference',
    level: 4,
    scopeLabelAr: 'Global',
    fromUtc: '2026-07-01T00:00:00Z',
    toUtc: '2026-07-24T00:00:00Z',
    locale: 'ar-SA',
    timeZone: 'Asia/Riyadh',
    includesSensitiveData: false,
  },
  generatedAtUtc: '2026-07-24T09:00:00Z',
  freshness: { status: 1, labelAr: 'محدثة' },
  confidence: { level: 1, labelAr: 'مرتفعة' },
  allowedActions: [],
  widgetDefinitions,
  widgets: [
    {
      widgetKey: 'dashboard.operational-summary',
      generatedAtUtc: '2026-07-24T09:00:00Z',
      dataEffectiveAtUtc: '2026-07-24T09:00:00Z',
      freshness: { status: 1, labelAr: 'محدثة' },
      confidence: { level: 1, labelAr: 'مرتفعة' },
      scopeSummary: { level: 4, labelAr: 'Global', isSensitive: false },
      isPartial: false,
      warningMessages: [],
      payload: {
        openNotes: 4,
        inProgressNotes: 1,
        pendingVerificationNotes: 1,
        unassignedNotes: 2,
        requiresRouting: 1,
        overdueNotes: 1,
        dueSoonNotes: 1,
        criticalOrHighNotes: 2,
      },
      drillDownTargets: [],
      allowedActions: [],
    },
    {
      widgetKey: 'dashboard.corrective-actions-summary',
      generatedAtUtc: '2026-07-24T09:00:00Z',
      dataEffectiveAtUtc: '2026-07-24T09:00:00Z',
      freshness: { status: 1, labelAr: 'محدثة' },
      confidence: { level: 1, labelAr: 'مرتفعة' },
      scopeSummary: { level: 4, labelAr: 'Global', isSensitive: false },
      isPartial: false,
      warningMessages: [],
      payload: {
        activeActions: 3,
        overdueActions: 1,
        pendingVerificationActions: 1,
        reopenedActions: 0,
        notesWithStalledActions: 1,
      },
      drillDownTargets: [],
      allowedActions: [],
    },
  ],
  widgetFailures: [{ widgetKey: 'demo.failure', messageAr: 'تعذر تحميل أداة تجريبية', isPartialSafe: true }],
  isPartial: true,
}
