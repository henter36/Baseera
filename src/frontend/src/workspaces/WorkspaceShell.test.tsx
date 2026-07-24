import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import type { WorkspaceDefinition, WorkspaceWidgetDefinition, WorkspaceWidgetEnvelope } from '../api/client'
import { WorkspaceShell, WorkspaceWidgetContainer } from './WorkspaceShell'

describe('WorkspaceShell', () => {
  it('renders RTL header, freshness, confidence, disabled action reason, and partial warnings', () => {
    render(
      <MemoryRouter>
        <WorkspaceShell
          definition={definition}
          context={{
            workspaceKey: 'reference',
            level: 4,
            scopeLabelAr: 'Global',
            fromUtc: '2026-07-01T00:00:00Z',
            toUtc: '2026-07-24T00:00:00Z',
            locale: 'ar-SA',
            timeZone: 'Asia/Riyadh',
            includesSensitiveData: false,
          }}
          freshness={{ status: 1, labelAr: 'محدثة' }}
          confidence={{ level: 2, labelAr: 'متوسطة', reasonAr: 'جزئية' }}
          generatedAtUtc="2026-07-24T09:00:00Z"
          allowedActions={[{
            code: 'CONFIGURE_OWN_VIEW',
            labelAr: 'تخصيص العرض',
            enabled: false,
            disabledReasonAr: 'مؤجل',
            requiresConfirmation: false,
            target: null,
          }]}
          widgetFailures={[{ widgetKey: 'w2', messageAr: 'تعذر تحميل أداة', isPartialSafe: true }]}
        >
          <div>content</div>
        </WorkspaceShell>
      </MemoryRouter>,
    )

    expect(screen.getByRole('heading', { name: 'مساحة عمل مرجعية' })).toBeInTheDocument()
    expect(screen.getByText('محدثة')).toBeInTheDocument()
    expect(screen.getByText('ثقة متوسطة')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'تخصيص العرض' })).toBeDisabled()
    expect(screen.getByText(/تعذر تحميل أداة/)).toBeInTheDocument()
  })

  it('renders drill-down targets through the shared widget container', () => {
    render(
      <MemoryRouter>
        <WorkspaceWidgetContainer definition={widgetDefinition} data={widgetData}>
          <span>metric</span>
        </WorkspaceWidgetContainer>
      </MemoryRouter>,
    )

    expect(screen.getByRole('heading', { name: 'الملخص' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'فتح لوحة المتابعة' })).toBeInTheDocument()
  })
})

const definition: WorkspaceDefinition = {
  key: 'reference',
  titleAr: 'مساحة عمل مرجعية',
  titleEn: 'Reference Workspace',
  supportedLevels: [4],
  requiredPermissions: ['Workspaces.View'],
  registeredWidgets: ['w1'],
  defaultLayout: { items: [], version: 1 },
  availableFilters: [],
  supportedDrillDowns: [],
  features: { supportsSavedViews: false, supportsWidgetConfiguration: false, supportsExport: false, isReferenceOnly: true },
  version: 1,
}

const widgetDefinition: WorkspaceWidgetDefinition = {
  key: 'w1',
  titleAr: 'الملخص',
  titleEn: 'Summary',
  descriptionAr: 'وصف',
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
}

const widgetData: WorkspaceWidgetEnvelope = {
  widgetKey: 'w1',
  generatedAtUtc: '2026-07-24T09:00:00Z',
  dataEffectiveAtUtc: '2026-07-24T09:00:00Z',
  freshness: { status: 1, labelAr: 'محدثة' },
  confidence: { level: 1, labelAr: 'مرتفعة' },
  scopeSummary: { level: 4, labelAr: 'Global', isSensitive: false },
  isPartial: false,
  warningMessages: [],
  payload: {},
  drillDownTargets: [{
    routeKey: 'dashboard.operations',
    labelAr: 'فتح لوحة المتابعة',
    routeParameters: {},
    preservedFilters: {},
    requiredPermission: 'Dashboard.ViewOperational',
  }],
  allowedActions: [],
}
