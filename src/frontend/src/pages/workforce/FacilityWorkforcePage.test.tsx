import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { EmploymentStatus, WorkforceCoverageStatus } from '../../api/client'
import { FacilityWorkforcePage } from './FacilityWorkforcePage'

const {
  currentPermissions,
  coverage,
  createMember,
  dataQuality,
  importConfirm,
  importPreview,
  members,
  requirements,
  roles,
  rosters,
  summary,
  units,
} = vi.hoisted(() => ({
  currentPermissions: new Set<string>(),
  coverage: vi.fn(),
  createMember: vi.fn(),
  dataQuality: vi.fn(),
  importConfirm: vi.fn(),
  importPreview: vi.fn(),
  members: vi.fn(),
  requirements: vi.fn(),
  roles: vi.fn(),
  rosters: vi.fn(),
  summary: vi.fn(),
  units: vi.fn(),
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
      workforce: {
        ...actual.api.workforce,
        summary,
        coverage,
        units,
        roles,
        members,
        createMember,
        requirements,
        rosters,
        importConfirm,
        importPreview,
        dataQuality,
      },
    },
  }
})

describe('FacilityWorkforcePage', () => {
  beforeEach(() => {
    currentPermissions.clear()
    for (const permission of [
      'Workforce.ViewSummary',
      'Workforce.ViewCoverage',
      'Workforce.ViewMembers',
      'Workforce.Import',
    ]) {
      currentPermissions.add(permission)
    }

    summary.mockReset()
    coverage.mockReset()
    units.mockReset()
    roles.mockReset()
    members.mockReset()
    requirements.mockReset()
    rosters.mockReset()
    importPreview.mockReset()
    importConfirm.mockReset()
    dataQuality.mockReset()
    createMember.mockReset()

    summary.mockResolvedValue({
      facilityId: 'facility-a',
      totalMembers: 12,
      operationallyEligible: 10,
      required: 14,
      minimumSafe: 11,
      scheduled: 9,
      present: 8,
      operationallyAvailable: 9,
      onLeave: 2,
      inTraining: 1,
      restricted: 0,
      gap: 5,
      safeGap: 2,
      coverageRate: 0.6429,
      qualificationCoverage: 0.8,
      coverageStatus: WorkforceCoverageStatus.Attention,
      criticalPositionsAtRisk: 1,
      staleRecords: 1,
      missingDataRecords: 0,
      freshnessStatus: 'partial',
      confidenceLevel: 'medium',
      isPartial: true,
      warnings: ['توجد فجوات تغطية في أدوار حرجة.'],
      fatigueIndicators: [],
      generatedAtUtc: '2026-07-25T10:00:00.000Z',
      dataEffectiveAtUtc: '2026-07-25T09:00:00.000Z',
    })
    coverage.mockResolvedValue([{
      roleDefinitionId: 'role-tower',
      roleCode: 'TOWER',
      roleNameAr: 'ضابط برج',
      facilityUnitId: 'unit-1',
      unitNameAr: 'عنبر أ',
      shiftDefinitionId: 'shift-day',
      shiftCode: 'DAY',
      required: 3,
      minimumSafe: 2,
      scheduled: 2,
      present: 1,
      operationallyAvailable: 2,
      gap: 1,
      safeGap: 0,
      coverageRate: 0.6667,
      coverageStatus: WorkforceCoverageStatus.Attention,
    }])
    units.mockResolvedValue([{
      facilityUnitId: 'unit-1',
      unitNameAr: 'عنبر أ',
      required: 6,
      operationallyAvailable: 4,
      gap: 2,
      coverageRate: 0.6667,
      coverageStatus: WorkforceCoverageStatus.Attention,
    }])
    roles.mockResolvedValue([{
      id: 'role-tower',
      code: 'TOWER',
      nameAr: 'ضابط برج',
      nameEn: 'Tower Officer',
      category: 1,
      criticality: 3,
      requiresCertification: true,
      isShiftBased: true,
      isSensitive: false,
    }])
    members.mockResolvedValue([{
      id: 'member-1',
      employeeNumber: 'EMP-001',
      displayName: 'أحمد العتيبي',
      employmentStatus: EmploymentStatus.Active,
      jobTitle: 'ضابط أمن',
      primarySpecialty: 'أمن',
      currentOperationalUnitId: 'unit-1',
      currentOperationalUnitNameAr: 'عنبر أ',
      isOperational: true,
      lastVerifiedAtUtc: '2026-07-25T09:00:00.000Z',
      dataQualityIssues: [],
    }])
    requirements.mockResolvedValue([])
    rosters.mockResolvedValue([])
    dataQuality.mockResolvedValue({
      totalMembers: 12,
      missingEmployeeNumber: 0,
      unknownEmploymentStatus: 1,
      missingHomeOrOperationalFacility: 0,
      staleVerification: 1,
      openImportIssues: 0,
      warnings: ['سجلات تحقق قديمة'],
    })
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

  it('renders coverage overview and section navigation', async () => {
    renderPage()

    expect(await screen.findByRole('heading', { name: 'القوى البشرية والتغطية التشغيلية' })).toBeInTheDocument()
    expect(screen.getByText('64%')).toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: 'تنقل مركز القوى البشرية' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'العودة لمساحة السجن' })).toHaveAttribute(
      'href',
      '/workspaces/facilities/facility-a?section=workforce',
    )

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'الأعضاء' }))
    expect(await screen.findByRole('list', { name: 'سجلات الأعضاء' })).toBeInTheDocument()
    expect(within(screen.getByRole('list', { name: 'سجلات الأعضاء' })).getByText('أحمد العتيبي')).toBeInTheDocument()
  })

  it('hides the workforce page when summary permission is missing', () => {
    currentPermissions.clear()
    renderPage()

    expect(screen.getByRole('alert')).toHaveTextContent('ليست لديك صلاحية عرض مساحة العمل.')
    expect(summary).not.toHaveBeenCalled()
  })

  it('previews and confirms a typed workforce import batch', async () => {
    const user = userEvent.setup()
    renderPage('/facilities/facility-a/workforce?section=imports')

    const importForm = await screen.findByRole('form', { name: 'معاينة استيراد القوى البشرية' })
    await user.clear(within(importForm).getByLabelText('رقم الموظف'))
    await user.type(within(importForm).getByLabelText('رقم الموظف'), 'IMP-A1-001')
    await user.click(within(importForm).getByRole('button', { name: 'معاينة الاستيراد' }))

    await waitFor(() => expect(importPreview).toHaveBeenCalledWith('facility-a', expect.objectContaining({
      sourceSystem: 'manual-csv',
      sourceReference: 'D5-1-demo-import',
      fileHash: 'phase-d5-1-demo-hash',
      rows: [expect.objectContaining({
        employeeNumber: 'IMP-A1-001',
        displayName: 'عضو مستورد للمعاينة',
        jobTitle: 'ضابط أمن',
        primarySpecialty: 'أمن',
        employmentStatus: EmploymentStatus.Active,
      })],
    })))

    expect(await screen.findByText('نتيجة المعاينة')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'تأكيد الاستيراد' }))

    await waitFor(() => expect(importConfirm).toHaveBeenCalledTimes(1))
  })
})

function renderPage(path = '/facilities/facility-a/workforce') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/facilities/:facilityId/workforce" element={<FacilityWorkforcePage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}
