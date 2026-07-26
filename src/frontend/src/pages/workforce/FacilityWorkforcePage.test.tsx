import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, EmploymentStatus, WorkforceCoverageStatus, WorkforceImportKind } from '../../api/client'
import { FacilityWorkforcePage } from './FacilityWorkforcePage'

const {
  currentPermissions,
  coverage,
  criticalPositions,
  createAvailability,
  dataQuality,
  exportWorkforce,
  importConfirm,
  importPreview,
  member,
  members,
  qualifications,
  reconciliation,
  requirements,
  resolveReconciliation,
  roles,
  rosters,
  summary,
  units,
  updateMember,
} = vi.hoisted(() => ({
  currentPermissions: new Set<string>(),
  coverage: vi.fn(),
  criticalPositions: vi.fn(),
  createAvailability: vi.fn(),
  dataQuality: vi.fn(),
  exportWorkforce: vi.fn(),
  importConfirm: vi.fn(),
  importPreview: vi.fn(),
  member: vi.fn(),
  members: vi.fn(),
  qualifications: vi.fn(),
  reconciliation: vi.fn(),
  requirements: vi.fn(),
  resolveReconciliation: vi.fn(),
  roles: vi.fn(),
  rosters: vi.fn(),
  summary: vi.fn(),
  units: vi.fn(),
  updateMember: vi.fn(),
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
        qualifications,
        member,
        updateMember,
        requirements,
        rosters,
        createAvailability,
        importConfirm,
        importPreview,
        dataQuality,
        criticalPositions,
        reconciliation,
        resolveReconciliation,
        export: exportWorkforce,
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
      'Workforce.ManageMembers',
      'Workforce.RecordAvailability',
      'Workforce.Import',
      'Workforce.Reconcile',
      'Workforce.Export',
    ]) {
      currentPermissions.add(permission)
    }

    for (const mock of [
      summary, coverage, units, roles, members, member, requirements, rosters,
      importPreview, importConfirm, dataQuality, createAvailability, updateMember,
      criticalPositions, reconciliation, resolveReconciliation, exportWorkforce,
    ]) {
      mock.mockReset()
    }

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
      displayName: 'ناصر الدوسري',
      employmentStatus: EmploymentStatus.Active,
      jobTitle: 'ضابط أمن',
      primarySpecialty: 'أمن',
      currentOperationalUnitId: 'unit-1',
      currentOperationalUnitNameAr: 'عنبر أ',
      isOperational: true,
      isSensitiveRole: true,
      lastVerifiedAtUtc: '2026-07-25T09:00:00.000Z',
      rowVersion: 'AQID',
      dataQualityIssues: [],
    }])
    member.mockResolvedValue({
      member: {
        id: 'member-1',
        employeeNumber: 'EMP-001',
        displayName: 'ناصر الدوسري',
        employmentStatus: EmploymentStatus.Active,
        jobTitle: 'ضابط أمن',
        primarySpecialty: 'أمن',
        isOperational: true,
        isSensitiveRole: true,
        dataQualityIssues: [],
      },
      assignments: [],
      qualifications: [{
        id: 'qual-1',
        qualificationType: 0,
        name: 'رخصة برج',
        expiresAtUtc: '2026-08-01T00:00:00.000Z',
        status: 0,
      }],
      availability: [],
    })
    qualifications.mockResolvedValue({
      items: [{
        id: 'qual-1',
        memberId: 'member-1',
        memberDisplayName: 'ناصر الدوسري',
        qualificationType: 0,
        roleDefinitionId: 'role-tower',
        roleCode: 'TOWER',
        name: 'رخصة برج',
        expiresAtUtc: '2026-08-01T00:00:00.000Z',
        status: 0,
      }],
      totalCount: 1,
      page: 1,
      pageSize: 100,
    })
    requirements.mockResolvedValue([{
      id: 'req-1',
      roleDefinitionId: 'role-tower',
      roleCode: 'TOWER',
      requiredHeadcount: 3,
      minimumSafeHeadcount: 2,
      effectiveFromUtc: '2026-07-01T00:00:00.000Z',
      sourceReference: 'ORD-1',
    }])
    rosters.mockResolvedValue([{
      id: 'roster-1',
      shiftDefinitionId: 'shift-day',
      dutyDate: '2026-07-25',
      status: 'Draft',
      assignmentCount: 2,
    }])
    dataQuality.mockResolvedValue({
      totalMembers: 12,
      missingEmployeeNumber: 0,
      unknownEmploymentStatus: 1,
      missingHomeOrOperationalFacility: 0,
      staleVerification: 1,
      openImportIssues: 0,
      warnings: ['سجلات تحقق قديمة'],
    })
    criticalPositions.mockResolvedValue([{
      id: 'crit-1',
      roleDefinitionId: 'role-tower',
      roleCode: 'TOWER',
      roleNameAr: 'ضابط برج',
      requiredPrimaryCount: 1,
      requiredAlternateCount: 1,
      primaryFilled: 0,
      alternateFilled: 0,
      vacantPrimary: 1,
      vacantAlternate: 1,
      actingCount: 0,
      singlePointOfFailure: true,
      criticality: 3,
      statusAr: 'شاغر',
    }])
    reconciliation.mockResolvedValue({
      items: [{
        id: 'recon-1',
        issueType: 'SourceConflict',
        severity: 'high',
        titleAr: 'تعارض مصدر',
        detailAr: 'تعارض رقم خارجي',
        entityType: 'WorkforceMember',
        suggestedActionAr: 'اعتماد المصدر الأحدث',
        responsibleHintAr: 'مسؤول القوى البشرية',
        detectedAtUtc: '2026-07-25T08:00:00.000Z',
      }],
      totalCount: 1,
      page: 1,
      pageSize: 50,
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
    updateMember.mockResolvedValue(undefined)
    createAvailability.mockResolvedValue({ id: 'avail-1' })
    resolveReconciliation.mockResolvedValue(undefined)
    exportWorkforce.mockResolvedValue({
      blob: new Blob(['employeeNumber,displayName\nEMP-001,ناصر الدوسري'], { type: 'text/csv' }),
      fileName: 'workforce-export.csv',
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
    expect(within(screen.getByRole('list', { name: 'سجلات الأعضاء' })).getByText('ناصر الدوسري')).toBeInTheDocument()
  })

  it('syncs section selection into the URL and supports back/forward searchParams', async () => {
    const user = userEvent.setup()
    renderPage()

    await screen.findByRole('heading', { name: 'القوى البشرية والتغطية التشغيلية' })
    await user.click(screen.getByRole('button', { name: 'الوحدات' }))
    expect(await screen.findByTestId('router-location')).toHaveTextContent('section=units')
    expect(screen.getByLabelText('تغطية الوحدات')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'المشهد العام' }))
    await waitFor(() => {
      expect(screen.getByTestId('router-location')).not.toHaveTextContent('section=')
    })
  })

  it('hides the workforce page when summary permission is missing', () => {
    currentPermissions.clear()
    renderPage()

    expect(screen.getByRole('alert')).toHaveTextContent('ليست لديك صلاحية عرض مساحة العمل.')
    expect(summary).not.toHaveBeenCalled()
  })

  it('hides import and export controls without permissions', async () => {
    currentPermissions.delete('Workforce.Import')
    currentPermissions.delete('Workforce.Export')
    const user = userEvent.setup()
    renderPage()

    expect(await screen.findByRole('heading', { name: 'القوى البشرية والتغطية التشغيلية' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'تصدير محدود' })).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'الاستيراد' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('ليست لديك صلاحية عرض مساحة العمل.')
  })

  it('previews and confirms a typed workforce import batch with importKind', async () => {
    const user = userEvent.setup()
    renderPage('/facilities/facility-a/workforce?section=imports')

    const importForm = await screen.findByRole('form', { name: 'معاينة استيراد القوى البشرية' })
    await user.selectOptions(within(importForm).getByLabelText('نوع الاستيراد'), String(WorkforceImportKind.Qualifications))
    await user.type(within(importForm).getByLabelText('النظام المصدر'), 'manual-csv')
    await user.type(within(importForm).getByLabelText('مرجع الاستيراد'), 'D5-1-test-import')
    await user.type(within(importForm).getByLabelText('بصمة الملف'), 'phase-d5-1-test-hash')
    await user.type(within(importForm).getByLabelText('رقم الموظف'), 'IMP-A1-001')
    await user.type(within(importForm).getByLabelText('الاسم المعروض'), 'ناصر الدوسري')
    await user.type(within(importForm).getByLabelText('المسمى الوظيفي'), 'ضابط أمن')
    await user.type(within(importForm).getByLabelText('التخصص الأساسي'), 'أمن')
    await user.click(within(importForm).getByRole('button', { name: 'معاينة الاستيراد' }))

    await waitFor(() => expect(importPreview).toHaveBeenCalledWith('facility-a', expect.objectContaining({
      importKind: WorkforceImportKind.Qualifications,
      sourceSystem: 'manual-csv',
      sourceReference: 'D5-1-test-import',
      fileHash: 'phase-d5-1-test-hash',
      rows: [expect.objectContaining({
        employeeNumber: 'IMP-A1-001',
        displayName: 'ناصر الدوسري',
        jobTitle: 'ضابط أمن',
        primarySpecialty: 'أمن',
        employmentStatus: EmploymentStatus.Active,
      })],
    })))

    expect(await screen.findByText('نتيجة المعاينة')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'تأكيد الاستيراد' }))

    await waitFor(() => expect(importConfirm).toHaveBeenCalledTimes(1))
  })

  it('updates a member from the members section', async () => {
    const user = userEvent.setup()
    renderPage('/facilities/facility-a/workforce?section=members')

    await screen.findByText('ناصر الدوسري')
    await user.click(screen.getByRole('button', { name: 'تعديل' }))
    const form = await screen.findByRole('form', { name: 'تعديل عضو ناصر الدوسري' })
    await user.clear(within(form).getByLabelText('المسمى'))
    await user.type(within(form).getByLabelText('المسمى'), 'قائد وردية')
    await user.click(within(form).getByRole('button', { name: 'حفظ' }))

    await waitFor(() => expect(updateMember).toHaveBeenCalledWith('facility-a', 'member-1', expect.objectContaining({
      displayName: 'ناصر الدوسري',
      jobTitle: 'قائد وردية',
      primarySpecialty: 'أمن',
      rowVersion: 'AQID',
      isOperational: true,
      isSensitiveRole: true,
    })))
  })

  it('loads qualifications and reconciliation from the API', async () => {
    const user = userEvent.setup()
    renderPage()

    await screen.findByRole('heading', { name: 'القوى البشرية والتغطية التشغيلية' })
    await user.click(screen.getByRole('button', { name: 'المؤهلات' }))
    expect(await screen.findByRole('list', { name: 'قائمة المؤهلات' })).toBeInTheDocument()
    expect(screen.getByText('رخصة برج')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'المصالحة' }))
    expect(await screen.findByRole('list', { name: 'بنود المصالحة' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'معالجة' }))
    await waitFor(() => expect(resolveReconciliation).toHaveBeenCalledWith('facility-a', 'recon-1', {
      resolutionAction: 'acknowledge',
    }))
  })

  it('surfaces 403/404/409/422 API failures for member update', async () => {
    const user = userEvent.setup()
    const cases: Array<[ApiError, string]> = [
      [new ApiError(403, 'forbidden'), 'ليست لديك صلاحية تنفيذ هذه العملية.'],
      [new ApiError(404, 'missing'), 'السجل غير موجود ضمن نطاق السجن.'],
      [new ApiError(409, 'conflict'), 'تعارض في البيانات. حدّث الصفحة ثم أعد المحاولة.'],
      [new ApiError(422, 'invalid payload'), 'invalid payload'],
    ]

    renderPage('/facilities/facility-a/workforce?section=members')
    await screen.findByText('ناصر الدوسري')

    for (const [error, expected] of cases) {
      updateMember.mockRejectedValueOnce(error)
      await user.click(screen.getByRole('button', { name: 'تعديل' }))
      const form = await screen.findByRole('form', { name: 'تعديل عضو ناصر الدوسري' })
      await user.click(within(form).getByRole('button', { name: 'حفظ' }))
      expect(await screen.findByRole('alert')).toHaveTextContent(expected)
      await user.click(within(form).getByRole('button', { name: 'إلغاء' }))
    }
  }, 15_000)

  it('exports with redaction note when Workforce.Export is granted', async () => {
    const user = userEvent.setup()
    const createObjectURL = vi.fn(() => 'blob:workforce')
    const revokeObjectURL = vi.fn()
    const clickSpy = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined)
    Object.defineProperty(URL, 'createObjectURL', { value: createObjectURL, configurable: true })
    Object.defineProperty(URL, 'revokeObjectURL', { value: revokeObjectURL, configurable: true })

    try {
      renderPage()
      await screen.findByRole('button', { name: 'تصدير محدود' })
      await user.click(screen.getByRole('button', { name: 'تصدير محدود' }))

      await waitFor(() => expect(exportWorkforce).toHaveBeenCalledWith('facility-a', { pageSize: 500 }))
      expect(await screen.findByTestId('export-redaction-note')).toHaveTextContent('إخفاء البيانات الشخصية الحساسة')
    } finally {
      clickSpy.mockRestore()
    }
  })

  it('renders empty and partial states', async () => {
    members.mockResolvedValue([])
    requirements.mockResolvedValue([])
    units.mockResolvedValue([])
    reconciliation.mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 50 })
    const user = userEvent.setup()
    renderPage()

    await screen.findByText('64%')
    expect(screen.getByText('توجد فجوات تغطية في أدوار حرجة.')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'الأعضاء' }))
    expect(await screen.findByText('لا يوجد أعضاء مسجلون.')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'المتطلبات' }))
    expect(await screen.findByText('لا توجد متطلبات تسكين مسجلة.')).toBeInTheDocument()
  })
})

function LocationProbe() {
  const location = useLocation()
  return <div data-testid="router-location">{location.pathname}{location.search}</div>
}

function renderPage(path = '/facilities/facility-a/workforce') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>
        <LocationProbe />
        <Routes>
          <Route path="/facilities/:facilityId/workforce" element={<FacilityWorkforcePage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}
