import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ResourceCondition, ResourceCriticality, ResourceSourceType, ResourceStatus, ResourceType } from '../../api/client'
import { FacilityResourcesPage } from './FacilityResourcesPage'

const {
  currentPermissions,
  assets,
  categories,
  createAsset,
  exceptions,
  importConfirm,
  importPreview,
  summary,
} = vi.hoisted(() => ({
  currentPermissions: new Set<string>(),
  assets: vi.fn(),
  categories: vi.fn(),
  createAsset: vi.fn(),
  exceptions: vi.fn(),
  importConfirm: vi.fn(),
  importPreview: vi.fn(),
  summary: vi.fn(),
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
      resources: {
        summary,
        categories,
        exceptions,
        assets,
        createAsset,
        importConfirm,
        importPreview,
      },
    },
  }
})

describe('FacilityResourcesPage', () => {
  beforeEach(() => {
    currentPermissions.clear()
    for (const permission of ['Resources.ViewSummary', 'Resources.ViewAssets', 'Resources.ManageAssets', 'Resources.Import']) {
      currentPermissions.add(permission)
    }

    summary.mockReset()
    categories.mockReset()
    exceptions.mockReset()
    assets.mockReset()
    createAsset.mockReset()
    importPreview.mockReset()
    importConfirm.mockReset()

    summary.mockResolvedValue({
      facilityId: 'facility-a',
      totalRegistered: 6,
      operational: 4,
      available: 1,
      standby: 1,
      inUse: 2,
      underMaintenance: 1,
      outOfService: 1,
      awaitingParts: 1,
      unknown: 0,
      retired: 0,
      required: 8,
      gap: 4,
      surplus: 0,
      readinessRate: 0.5,
      availabilityRate: 0.125,
      dataCompletenessRate: 0.9,
      missionCriticalUnavailable: 1,
      staleRecords: 1,
      missingDataRecords: 0,
      freshnessStatus: 'partial',
      confidenceLevel: 'medium',
      isPartial: true,
      warnings: ['توجد موارد حرجة غير جاهزة.'],
      generatedAtUtc: '2026-07-25T10:00:00.000Z',
      dataEffectiveAtUtc: '2026-07-25T09:00:00.000Z',
    })
    categories.mockResolvedValue([{
      resourceType: ResourceType.Vehicle,
      resourceTypeCode: 'Vehicle',
      labelAr: 'المركبات',
      total: 2,
      operational: 1,
      available: 0,
      underMaintenance: 1,
      outOfService: 0,
      awaitingParts: 0,
      required: 3,
      gap: 2,
      readinessRate: 0.3333,
      freshnessStatus: 'current',
      confidenceLevel: 'medium',
    }])
    exceptions.mockResolvedValue([{
      type: 'CriticalResourceUnavailable',
      resourceAssetId: 'asset-a',
      resourceType: ResourceType.Vehicle,
      reference: 'VEH-A1-002',
      titleAr: 'حافلة نقل نزلاء',
      severityAr: 'حرجة',
      priorityRank: 950,
      reasonAr: 'المورد خارج الخدمة ويؤثر على الجاهزية.',
      ownerAr: null,
      dueAtUtc: null,
      actionLabelAr: 'فتح المورد',
    }])
    assets.mockResolvedValue([{
      id: 'asset-a',
      resourceType: ResourceType.Vehicle,
      assetCode: 'VEH-A1-002',
      displayName: 'حافلة نقل نزلاء',
      serialNumber: null,
      plateNumber: null,
      currentStatus: ResourceStatus.UnderMaintenance,
      condition: ResourceCondition.Fair,
      criticality: ResourceCriticality.MissionCritical,
      operationalFacilityUnitNameAr: 'عنبر الجنوب',
      custodianNameAr: null,
      lastVerifiedAtUtc: '2026-07-25T09:00:00.000Z',
      hasOpenMaintenance: true,
      dataQualityIssues: [],
    }])
    createAsset.mockResolvedValue({ id: 'asset-new' })
    importPreview.mockResolvedValue({
      totalRows: 1,
      validRows: 1,
      rejectedRows: 0,
      duplicateRows: 0,
      appliedRows: 0,
      errors: [],
    })
    importConfirm.mockResolvedValue({
      totalRows: 1,
      validRows: 1,
      rejectedRows: 0,
      duplicateRows: 0,
      appliedRows: 1,
      errors: [],
    })
  })

  it('renders resource readiness and operational exceptions from real contracts', async () => {
    renderPage()

    expect(await screen.findByRole('heading', { name: 'جاهزية الموارد والأصول الأساسية' })).toBeInTheDocument()
    expect(screen.getByText('50%')).toBeInTheDocument()
    const exceptionsList = screen.getByRole('list', { name: 'استثناءات الموارد' })
    expect(within(exceptionsList).getByText('حافلة نقل نزلاء')).toBeInTheDocument()
    expect(screen.getByRole('list', { name: 'سجلات الموارد' })).toBeInTheDocument()
  })

  it('hides the resources page when summary permission is missing', () => {
    currentPermissions.clear()
    renderPage()

    expect(screen.getByRole('alert')).toHaveTextContent('ليست لديك صلاحية عرض مساحة العمل.')
    expect(summary).not.toHaveBeenCalled()
  })

  it('sends a typed create payload without forcing legacy page navigation', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByRole('list', { name: 'سجلات الموارد' })
    const createForm = screen.getByRole('form', { name: 'إضافة مورد أساسي' })

    await user.type(within(createForm).getByLabelText('كود المورد'), 'COM-A1-100')
    await user.type(within(createForm).getByLabelText('الاسم'), 'جهاز اتصال احتياطي')
    await user.type(within(createForm).getByLabelText('معرّف المنظمة المالكة'), 'org-a')
    await user.click(within(createForm).getByRole('button', { name: 'إنشاء المورد' }))

    await waitFor(() => expect(createAsset).toHaveBeenCalledWith('facility-a', expect.objectContaining({
      resourceType: ResourceType.Vehicle,
      assetCode: 'COM-A1-100',
      displayName: 'جهاز اتصال احتياطي',
      ownershipOrganizationId: 'org-a',
      currentStatus: ResourceStatus.Available,
      condition: ResourceCondition.Good,
      criticality: ResourceCriticality.Medium,
      sourceType: ResourceSourceType.Manual,
    })))
  })

  it('previews and confirms a typed resource import batch', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByRole('list', { name: 'سجلات الموارد' })
    const importForm = screen.getByRole('form', { name: 'معاينة استيراد الموارد' })

    await user.clear(within(importForm).getByLabelText('كود المورد'))
    await user.type(within(importForm).getByLabelText('كود المورد'), 'IMP-A1-001')
    await user.click(within(importForm).getByRole('button', { name: 'معاينة الاستيراد' }))

    await waitFor(() => expect(importPreview).toHaveBeenCalledWith('facility-a', expect.objectContaining({
      sourceSystem: 'manual-csv',
      sourceReference: 'D5-demo-import',
      fileHash: 'phase-d5-demo-hash',
      rows: [expect.objectContaining({
        resourceType: ResourceType.OperationalEquipment,
        assetCode: 'IMP-A1-001',
        displayName: 'مورد مستورد للمعاينة',
        currentStatus: ResourceStatus.Available,
      })],
    })))

    expect(await screen.findByText('نتيجة المعاينة')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'تأكيد الاستيراد' }))

    await waitFor(() => expect(importConfirm).toHaveBeenCalledTimes(1))
  })
})

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/facilities/facility-a/resources']}>
        <Routes>
          <Route path="/facilities/:facilityId/resources" element={<FacilityResourcesPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}
