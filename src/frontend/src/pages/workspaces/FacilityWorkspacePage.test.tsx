import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { FacilityWorkspacePage } from './FacilityWorkspacePage'

const { getWorkspace, getNoteWorkspaceDetail, getCorrectiveAction, getCorrectiveActionHistory, currentPermissions } = vi.hoisted(() => ({
  getWorkspace: vi.fn(),
  getNoteWorkspaceDetail: vi.fn(),
  getCorrectiveAction: vi.fn(),
  getCorrectiveActionHistory: vi.fn(),
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
      notes: {
        ...actual.api.notes,
        workspaceDetail: getNoteWorkspaceDetail,
      },
      correctiveActions: {
        ...actual.api.correctiveActions,
        get: getCorrectiveAction,
        history: getCorrectiveActionHistory,
      },
    },
  }
})

describe('FacilityWorkspacePage', () => {
  beforeEach(() => {
    getWorkspace.mockReset()
    getWorkspace.mockResolvedValue(shell)
    getNoteWorkspaceDetail.mockReset()
    getNoteWorkspaceDetail.mockResolvedValue(noteWorkspaceDetail)
    getCorrectiveAction.mockReset()
    getCorrectiveAction.mockResolvedValue(correctiveActionDetail)
    getCorrectiveActionHistory.mockReset()
    getCorrectiveActionHistory.mockResolvedValue(correctiveActionHistory)
    currentPermissions.clear()
    currentPermissions.add('Workspaces.View')
    currentPermissions.add('Workspaces.ViewFacility')
  })

  it('renders command header, situation overview, and intervention queue', async () => {
    renderPage('/workspaces/facilities/facility-a')

    expect(await screen.findByRole('heading', { name: 'سجن أ1' })).toBeInTheDocument()
    expect(screen.getByText('مركز قيادة السجن')).toBeInTheDocument()
    expect(screen.getAllByText('تتطلب تدخلاً').length).toBeGreaterThan(0)
    expect(screen.getByText('قائمة الأولويات')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'فتح الملاحظة' })).not.toBeInTheDocument()
    expect(getWorkspace).toHaveBeenCalledWith('facility-operations', expect.objectContaining({ level: 1, facilityId: 'facility-a' }))
  })

  it('synchronizes date filters without losing facility context', async () => {
    renderPage('/workspaces/facilities/facility-a')
    await screen.findByRole('heading', { name: 'سجن أ1' })

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

  it('opens note details in the context panel and syncs URL state', async () => {
    renderPage('/workspaces/facilities/facility-a')

    fireEvent.click(await screen.findByRole('button', { name: /ملاحظة حرجة/ }))

    await waitFor(() => {
      expect(screen.getByTestId('router-location')).toHaveTextContent('panel=note')
      expect(screen.getByTestId('router-location')).toHaveTextContent('entityId=note-1')
    })
    expect(await screen.findByRole('heading', { name: 'ملاحظة حرجة' })).toBeInTheDocument()
    expect(screen.getByText('وصف تفصيلي للملاحظة الحرجة')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'فتح الصفحة الكاملة' })).toHaveAttribute('href', '/notes/workspace?noteId=note-1')
    expect(getNoteWorkspaceDetail).toHaveBeenCalledWith('note-1')
  })

  it('keeps unsupported note actions disabled inside the context panel', async () => {
    getNoteWorkspaceDetail.mockResolvedValueOnce({
      ...noteWorkspaceDetail,
      allowedActions: ['START_WORK', 'ASSIGN'],
    })

    renderPage('/workspaces/facilities/facility-a')

    fireEvent.click(await screen.findByRole('button', { name: /ملاحظة حرجة/ }))

    const assign = await screen.findByRole('button', { name: 'إسناد' })
    expect(assign).toBeDisabled()
    expect(assign).toHaveAttribute('title', 'يتطلب هذا الإجراء نموذجًا متقدمًا في الصفحة الكاملة.')
    expect(screen.getByRole('button', { name: 'بدء المعالجة' })).toBeEnabled()
  })

  it('opens corrective action details in the context panel without page navigation', async () => {
    renderPage('/workspaces/facilities/facility-a')

    fireEvent.click(await screen.findByRole('button', { name: /إجراء متأخر/ }))

    await waitFor(() => {
      expect(screen.getByTestId('router-location')).toHaveTextContent('panel=corrective-action')
      expect(screen.getByTestId('router-location')).toHaveTextContent('entityId=action-1')
    })
    expect(await screen.findByText('وصف تفصيلي للإجراء التصحيحي')).toBeInTheDocument()
    expect(screen.getByText('تأخر التنفيذ')).toBeInTheDocument()
    expect(getCorrectiveAction).toHaveBeenCalledWith('action-1')
    expect(getCorrectiveActionHistory).toHaveBeenCalledWith('action-1')
  })

  it('opens escalation and form previews inside the workspace', async () => {
    renderPage('/workspaces/facilities/facility-a')

    fireEvent.click(await screen.findByRole('button', { name: /تصعيد حرج/ }))
    expect(await screen.findByRole('heading', { name: 'تصعيد حرج' })).toBeInTheDocument()
    expect(screen.getByText('لا يحتوي عنصر الأولوية الحالي على معرف occurrence محدد؛ تعرض اللوحة ملخصًا آمنًا، والصفحة الكاملة متاحة عند الحاجة.')).toBeInTheDocument()

    fireEvent.click(screen.getByLabelText('إغلاق لوحة التفاصيل'))
    fireEvent.click(await screen.findByRole('button', { name: /نموذج متأخر/ }))
    expect(await screen.findByRole('heading', { name: 'نموذج متأخر' })).toBeInTheDocument()
    expect(screen.getByText('الانتقال إلى صفحة التعبئة أو المراجعة يبقى إجراءً صريحًا فقط عندما يحتاج المستخدم إدخال النموذج.')).toBeInTheDocument()
  })

  it('opens the action center without leaving the facility workspace', async () => {
    renderPage('/workspaces/facilities/facility-a')

    fireEvent.click(await screen.findByRole('button', { name: 'مركز الإجراءات' }))

    expect(screen.getByRole('heading', { name: 'مركز الإجراءات' })).toBeInTheDocument()
    expect(screen.getByText('مسندة أو تحتاج إجراء')).toBeInTheDocument()
    expect(screen.getByTestId('router-location')).toHaveTextContent('/workspaces/facilities/facility-a')
  })

  it('supports direct panel links and browser back to the operational scene', async () => {
    renderPage('/workspaces/facilities/facility-a?panel=note&entityId=note-1')

    expect(await screen.findByRole('heading', { name: 'ملاحظة حرجة' })).toBeInTheDocument()

    fireEvent.click(screen.getByLabelText('إغلاق لوحة التفاصيل'))

    await waitFor(() => {
      expect(screen.queryByRole('heading', { name: 'ملاحظة حرجة' })).not.toBeInTheDocument()
      expect(screen.getByTestId('router-location')).not.toHaveTextContent('panel=note')
    })
  })

  it('announces partial data without dropping the command center', async () => {
    getWorkspace.mockResolvedValueOnce({
      ...shell,
      isPartial: true,
      widgetFailures: [{ widgetKey: 'facility.alerts-escalations', messageAr: 'تعذر تحميل التصعيدات.', isPartialSafe: true }],
    })

    renderPage('/workspaces/facilities/facility-a')

    expect(await screen.findByText(/بيانات جزئية/)).toHaveTextContent('تعذر تحميل التصعيدات.')
    expect(screen.getByText('قائمة الأولويات')).toBeInTheDocument()
  })

  it('closes the context panel without losing filters', async () => {
    renderPage('/workspaces/facilities/facility-a?fromUtc=2026-07-09T21%3A00%3A00.000Z')

    fireEvent.click(await screen.findByRole('button', { name: /ملاحظة حرجة/ }))
    await screen.findByRole('heading', { name: 'ملاحظة حرجة' })

    fireEvent.click(screen.getByLabelText('إغلاق لوحة التفاصيل'))

    await waitFor(() => {
      const location = screen.getByTestId('router-location')
      expect(location).toHaveTextContent('fromUtc=2026-07-09T21%3A00%3A00.000Z')
      expect(location).not.toHaveTextContent('panel=note')
      expect(location).not.toHaveTextContent('entityId=note-1')
    })
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
        items: [
          {
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
          },
          {
            type: 'corrective-action',
            reference: 'CA-1',
            titleAr: 'إجراء متأخر',
            severityAr: 'عالية',
            priorityRank: 82,
            reasonAr: 'تأخر التنفيذ',
            dueAtUtc: '2026-07-18T00:00:00Z',
            overdueDays: 6,
            ownerAr: 'فريق الصيانة',
            actionLabelAr: 'فتح الإجراء',
            drillDownTarget: {
              routeKey: 'corrective-actions.list',
              labelAr: 'فتح الإجراء',
              routeParameters: { id: 'action-1' },
              preservedFilters: {},
              requiredPermission: 'CorrectiveActions.View',
            },
          },
          {
            type: 'escalation',
            reference: 'ESC-1',
            titleAr: 'تصعيد حرج',
            severityAr: 'حرجة',
            priorityRank: 88,
            reasonAr: 'تصعيد لم يعالج',
            dueAtUtc: '2026-07-19T00:00:00Z',
            overdueDays: 5,
            ownerAr: 'مدير المناوبة',
            actionLabelAr: 'استعراض التصعيد',
            drillDownTarget: {
              routeKey: 'escalations.occurrences',
              labelAr: 'فتح التصعيد',
              routeParameters: { id: 'esc-1' },
              preservedFilters: {},
              requiredPermission: 'Escalations.View',
            },
          },
          {
            type: 'form',
            reference: 'FORM-1',
            titleAr: 'نموذج متأخر',
            severityAr: 'متوسطة',
            priorityRank: 73,
            reasonAr: 'نموذج دوري متأخر',
            dueAtUtc: '2026-07-21T00:00:00Z',
            overdueDays: 3,
            ownerAr: 'فريق الالتزام',
            actionLabelAr: 'فتح الالتزام',
            drillDownTarget: {
              routeKey: 'form-compliance.facility',
              labelAr: 'فتح الالتزام',
              routeParameters: { facilityId: 'facility-a' },
              preservedFilters: {},
              requiredPermission: 'Forms.View',
            },
          },
        ],
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

const noteWorkspaceDetail = {
  note: {
    id: 'note-1',
    referenceNumber: 'OBS-1',
    title: 'ملاحظة حرجة',
    description: 'وصف تفصيلي للملاحظة الحرجة',
    status: 2,
    statusAr: 'مفتوحة',
    severity: 4,
    severityAr: 'حرجة',
    noteTypeId: 'type-1',
    noteTypeCode: 'OPS',
    noteTypeNameAr: 'تشغيلية',
    noteTypeIsActive: true,
    sourceType: 1,
    sourceAr: 'يدوي',
    classification: 1,
    scopeType: 2,
    regionId: 'region-a',
    facilityId: 'facility-a',
    reportedByUserId: 'user-1',
    reportedByDisplayName: 'مستخدم',
    reportedAtUtc: '2026-07-20T00:00:00Z',
    dueAtUtc: '2026-07-20T00:00:00Z',
    isOverdue: true,
    createdAtUtc: '2026-07-20T00:00:00Z',
    rowVersion: 'rv-note',
    isSensitiveRedacted: false,
  },
  allowedActions: ['START_WORK'],
  summary: {
    openCorrectiveActions: 1,
    attachmentCount: 0,
    waitingResource: false,
    waitingVerification: false,
    waitingClosureApproval: false,
    hasEscalation: false,
    progressPercent: 25,
    lastUpdatedAtUtc: '2026-07-24T09:00:00Z',
  },
  assignments: [],
  correctiveActions: {
    items: [{
      id: 'action-1',
      operationalNoteId: 'note-1',
      referenceNumber: 'CA-1',
      title: 'إجراء تصحيحي',
      actionType: 1,
      actionTypeAr: 'تصحيحي',
      priority: 2,
      priorityAr: 'متوسطة',
      status: 2,
      statusAr: 'قيد التنفيذ',
      classification: 1,
      dueAtUtc: '2026-07-22T00:00:00Z',
      isOverdue: true,
      isDueSoon: false,
      overdueDays: 2,
      currentAssigneeDisplay: 'فريق الصيانة',
      createdAtUtc: '2026-07-20T00:00:00Z',
      rowVersion: 'rv-action',
      isSensitiveRedacted: false,
    }],
    page: 1,
    pageSize: 10,
    totalCount: 1,
  },
  attachments: [],
  resources: [],
  decisions: [],
  links: [],
  timeline: [{
    id: 'tl-1',
    type: 'created',
    titleAr: 'إنشاء الملاحظة',
    occurredAtUtc: '2026-07-20T00:00:00Z',
    tone: 'info',
  }],
}

const correctiveActionDetail = {
  id: 'action-1',
  operationalNoteId: 'note-1',
  referenceNumber: 'CA-1',
  title: 'إجراء متأخر',
  actionType: 1,
  actionTypeAr: 'تصحيحي',
  priority: 3,
  priorityAr: 'عالية',
  status: 3,
  statusAr: 'قيد التنفيذ',
  classification: 1,
  ownerDepartmentId: null,
  dueAtUtc: '2026-07-18T00:00:00Z',
  isOverdue: true,
  isDueSoon: false,
  overdueDays: 6,
  currentAssigneeDisplay: 'فريق الصيانة',
  createdAtUtc: '2026-07-16T00:00:00Z',
  rowVersion: 'rv-ca',
  isSensitiveRedacted: false,
  description: 'وصف تفصيلي للإجراء التصحيحي',
  createdByUserId: 'user-1',
  createdByDisplayName: 'مستخدم',
  submittedAtUtc: '2026-07-16T01:00:00Z',
  workStartedAtUtc: '2026-07-16T02:00:00Z',
  submittedForVerificationAtUtc: null,
  completedAtUtc: null,
  completedByUserId: null,
  completionSummary: null,
  reopenedAtUtc: null,
  reopenReason: null,
  cancelledAtUtc: null,
  cancelReason: null,
  currentAssignment: {
    id: 'assignment-1',
    correctiveActionId: 'action-1',
    assignedToUserId: null,
    assignedToUserDisplayName: null,
    assignedToDepartmentId: 'dept-1',
    assignedToDepartmentName: 'فريق الصيانة',
    assignedByUserId: 'user-1',
    assignedByDisplayName: 'مستخدم',
    assignedAtUtc: '2026-07-16T02:00:00Z',
    dueAtUtc: '2026-07-18T00:00:00Z',
    reason: 'تأخر التنفيذ',
    acceptedAtUtc: null,
    completedAtUtc: null,
    endedAtUtc: null,
    endReason: null,
    isCurrent: true,
  },
}

const correctiveActionHistory = [{
  id: 'history-1',
  fromStatus: 2,
  toStatus: 3,
  toStatusAr: 'قيد التنفيذ',
  changedByUserId: 'user-1',
  changedByDisplayName: 'مستخدم',
  changedAtUtc: '2026-07-16T02:00:00Z',
  reason: 'بدء المعالجة',
  assignmentId: 'assignment-1',
  metadataJson: null,
}]
