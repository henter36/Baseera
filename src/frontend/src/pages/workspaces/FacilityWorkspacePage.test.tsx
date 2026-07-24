import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { FacilityWorkspacePage } from './FacilityWorkspacePage'

const { getWorkspace, currentPermissions } = vi.hoisted(() => ({
  getWorkspace: vi.fn(),
  currentPermissions: new Set<string>(),
}))

vi.mock('../../auth/AuthProvider', () => ({
  usePermission: (code: string) => currentPermissions.has(code),
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

describe('FacilityWorkspacePage', () => {
  beforeEach(() => {
    getWorkspace.mockReset()
    getWorkspace.mockResolvedValue(shell)
    currentPermissions.clear()
    currentPermissions.add('Workspaces.View')
    currentPermissions.add('Workspaces.ViewFacility')
  })

  it('renders facility workspace widgets returned by the server', async () => {
    renderPage('/workspaces/facilities/facility-a')

    expect(await screen.findByRole('heading', { name: 'مركز قرار السجن' })).toBeInTheDocument()
    expect(screen.getByText('سجن أ1')).toBeInTheDocument()
    expect(screen.getByText('تتطلب تدخلاً')).toBeInTheDocument()
    expect(screen.getByText('قائمة الأولويات')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'فتح الملاحظة' })).toHaveAttribute('href', '/notes/workspace?noteId=note-1')
    expect(getWorkspace).toHaveBeenCalledWith('facility-operations', expect.objectContaining({ level: 1, facilityId: 'facility-a' }))
  })

  it('synchronizes date filters without losing facility context', async () => {
    renderPage('/workspaces/facilities/facility-a')
    await screen.findByRole('heading', { name: 'مركز قرار السجن' })

    fireEvent.change(screen.getByLabelText('من'), { target: { value: '2026-07-10' } })

    await waitFor(() => {
      expect(screen.getByTestId('router-location')).toHaveTextContent('fromUtc=2026-07-09T21%3A00%3A00.000Z')
    })

    fireEvent.change(screen.getByLabelText('إلى'), { target: { value: '2026-07-10' } })

    await waitFor(() => {
      expect(getWorkspace.mock.calls.at(-1)?.[1]).toMatchObject({
        facilityId: 'facility-a',
        fromUtc: '2026-07-09T21:00:00.000Z',
        toUtc: '2026-07-10T20:59:59.999Z',
      })
    })

    await waitFor(() => {
      const location = screen.getByTestId('router-location')
      expect(location).toHaveTextContent('/workspaces/facilities/facility-a')
      expect(location).toHaveTextContent('fromUtc=2026-07-09T21%3A00%3A00.000Z')
      expect(location).toHaveTextContent('toUtc=2026-07-10T20%3A59%3A59.999Z')
    })
  })

  it('does not call the workspace API when only facility-level workspace permission exists', () => {
    currentPermissions.clear()
    currentPermissions.add('Workspaces.ViewFacility')

    renderPage('/workspaces/facilities/facility-a')

    expect(screen.getByRole('alert')).toHaveTextContent('ليست لديك صلاحية عرض مساحة العمل.')
    expect(getWorkspace).not.toHaveBeenCalled()
  })

  it('does not call the workspace API when only general workspace permission exists', () => {
    currentPermissions.clear()
    currentPermissions.add('Workspaces.View')

    renderPage('/workspaces/facilities/facility-a')

    expect(screen.getByRole('alert')).toHaveTextContent('ليست لديك صلاحية عرض مساحة العمل.')
    expect(getWorkspace).not.toHaveBeenCalled()
  })

  it('keeps consumed facility id out of drill-down query parameters', async () => {
    renderPage('/workspaces/facilities/facility-a')

    const link = await screen.findByRole('link', { name: 'فتح الالتزام' })

    expect(link).toHaveAttribute(
      'href',
      '/form-compliance/facilities/facility-a?fromUtc=2026-07-01T00%3A00%3A00Z&toUtc=2026-07-24T00%3A00%3A00Z',
    )
    expect(link.getAttribute('href')?.split('?')[1]).not.toContain('facilityId=')
  })
})

function renderPage(initialEntry: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[initialEntry]}>
        <Routes>
          <Route path="/workspaces/facilities/:facilityId" element={<><FacilityWorkspacePage /><LocationProbe /></>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

function LocationProbe() {
  const location = useLocation()

  return (
    <output data-testid="router-location">
      {location.pathname}
      {location.search}
    </output>
  )
}

const widgetDefinitionBase = {
  titleEn: 'Widget',
  descriptionAr: 'وصف',
  category: 1,
  supportedLevels: [1],
  requiredPermission: 'Workspaces.ViewFacility',
  requiredDataCapability: 'Facility',
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
} as const

const shell = {
  definition: {
    key: 'facility-operations',
    titleAr: 'مركز قرار السجن',
    titleEn: 'Facility Decision Center',
    supportedLevels: [1],
    requiredPermissions: ['Workspaces.View', 'Workspaces.ViewFacility'],
    registeredWidgets: ['facility.header', 'facility.executive-summary', 'facility.priority-queue'],
    defaultLayout: { items: [], version: 1 },
    availableFilters: [],
    supportedDrillDowns: [],
    features: { supportsSavedViews: false, supportsWidgetConfiguration: false, supportsExport: false, isReferenceOnly: false },
    version: 1,
  },
  context: {
    workspaceKey: 'facility-operations',
    level: 1,
    scopeLabelAr: 'Facility',
    facilityId: 'facility-a',
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
  widgetDefinitions: [
    { ...widgetDefinitionBase, key: 'facility.header', titleAr: 'تعريف السجن', defaultSize: 4 },
    { ...widgetDefinitionBase, key: 'facility.executive-summary', titleAr: 'الملخص التشغيلي', defaultSize: 3 },
    { ...widgetDefinitionBase, key: 'facility.priority-queue', titleAr: 'قائمة الأولويات', defaultSize: 3 },
    { ...widgetDefinitionBase, key: 'facility.form-compliance', titleAr: 'الالتزام بالنماذج', defaultSize: 2 },
  ],
  widgets: [
    {
      widgetKey: 'facility.header',
      generatedAtUtc: '2026-07-24T09:00:00Z',
      dataEffectiveAtUtc: '2026-07-24T09:00:00Z',
      freshness: { status: 1, labelAr: 'محدثة' },
      confidence: { level: 1, labelAr: 'مرتفعة' },
      scopeSummary: { level: 1, labelAr: 'Facility', facilityId: 'facility-a', isSensitive: false },
      isPartial: false,
      warningMessages: [],
      payload: {
        facilityId: 'facility-a',
        facilityNameAr: 'سجن أ1',
        regionId: 'region-a',
        regionNameAr: 'منطقة أ',
        facilityType: 'سجن',
        fromUtc: '2026-07-01T00:00:00Z',
        toUtc: '2026-07-24T00:00:00Z',
        calculatedAtUtc: '2026-07-24T09:00:00Z',
      },
      drillDownTargets: [],
      allowedActions: [],
    },
    {
      widgetKey: 'facility.executive-summary',
      generatedAtUtc: '2026-07-24T09:00:00Z',
      dataEffectiveAtUtc: '2026-07-24T09:00:00Z',
      freshness: { status: 1, labelAr: 'محدثة' },
      confidence: { level: 1, labelAr: 'مرتفعة' },
      scopeSummary: { level: 1, labelAr: 'Facility', facilityId: 'facility-a', isSensitive: false },
      isPartial: false,
      warningMessages: [],
      payload: {
        statusCode: 'intervention',
        statusAr: 'تتطلب تدخلاً',
        priorityIssues: 4,
        topDriverAr: 'الملاحظات المتأخرة: 2',
        changeSummaryAr: 'سجلت 1 ملاحظة جديدة ضمن الفترة المحددة.',
        topPendingActionAr: 'متابعة الإجراءات التصحيحية المتأخرة.',
        confidenceReasons: [],
        calculatedAtUtc: '2026-07-24T09:00:00Z',
      },
      drillDownTargets: [],
      allowedActions: [],
    },
    {
      widgetKey: 'facility.priority-queue',
      generatedAtUtc: '2026-07-24T09:00:00Z',
      dataEffectiveAtUtc: '2026-07-24T09:00:00Z',
      freshness: { status: 1, labelAr: 'محدثة' },
      confidence: { level: 1, labelAr: 'مرتفعة' },
      scopeSummary: { level: 1, labelAr: 'Facility', facilityId: 'facility-a', isSensitive: false },
      isPartial: false,
      warningMessages: [],
      payload: {
        limit: 10,
        items: [{
          type: 'note',
          reference: 'OBS-1',
          titleAr: 'ملاحظة حرجة',
          severityAr: 'حرجة',
          priorityRank: 90,
          reasonAr: 'ملاحظة حرجة مفتوحة',
          dueAtUtc: '2026-07-20T00:00:00Z',
          overdueDays: 4,
          ownerAr: null,
          actionLabelAr: 'فتح الملاحظة',
          drillDownTarget: {
            routeKey: 'notes.workspace',
            labelAr: 'فتح الملاحظة',
            routeParameters: { noteId: 'note-1' },
            preservedFilters: {},
            requiredPermission: 'Notes.View',
          },
        }],
      },
      drillDownTargets: [],
      allowedActions: [],
    },
    {
      widgetKey: 'facility.form-compliance',
      generatedAtUtc: '2026-07-24T09:00:00Z',
      dataEffectiveAtUtc: '2026-07-24T09:00:00Z',
      freshness: { status: 1, labelAr: 'محدثة' },
      confidence: { level: 1, labelAr: 'مرتفعة' },
      scopeSummary: { level: 1, labelAr: 'Facility', facilityId: 'facility-a', isSensitive: false },
      isPartial: false,
      warningMessages: [],
      payload: {
        targetedForms: 1,
        completedForms: 1,
        remainingForms: 0,
        overdueForms: 0,
        completionRate: 1,
        nearestDueAtUtc: null,
        notStartedForms: 0,
        pendingReviewForms: 0,
      },
      drillDownTargets: [{
        routeKey: 'form-compliance.facility',
        labelAr: 'فتح الالتزام',
        routeParameters: { facilityId: 'facility-a' },
        preservedFilters: {
          facilityId: 'facility-a',
          fromUtc: '2026-07-01T00:00:00Z',
          toUtc: '2026-07-24T00:00:00Z',
        },
        requiredPermission: 'Forms.View',
      }],
      allowedActions: [],
    },
  ],
  widgetFailures: [],
  isPartial: false,
}
