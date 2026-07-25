import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useParams, useSearchParams } from 'react-router'
import {
  ApiError,
  api,
  EmploymentStatus,
  WorkforceCoverageStatus,
  WorkforceSourceType,
  type DutyRosterPayload,
  type StaffingRequirementPayload,
  type WorkforceCoverageRowPayload,
  type WorkforceDataQualityPayload,
  type WorkforceImportPreviewRequest,
  type WorkforceImportResult,
  type WorkforceMemberListItem,
  type WorkforceRoleDefinitionPayload,
  type WorkforceSummaryPayload,
  type WorkforceUnitCoveragePayload,
} from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import { WorkspaceEmpty, WorkspaceError, WorkspaceLoading, WorkspaceUnauthorized } from '../../workspaces/WorkspaceShell'

type WorkforceSection =
  | 'overview'
  | 'coverage'
  | 'shifts'
  | 'units'
  | 'roles'
  | 'members'
  | 'requirements'
  | 'imports'
  | 'data-quality'

const SECTION_NAV: ReadonlyArray<Readonly<{ key: WorkforceSection; label: string }>> = [
  { key: 'overview', label: 'المشهد العام' },
  { key: 'coverage', label: 'التغطية' },
  { key: 'shifts', label: 'الورديات' },
  { key: 'units', label: 'الوحدات' },
  { key: 'roles', label: 'الأدوار' },
  { key: 'members', label: 'الأعضاء' },
  { key: 'requirements', label: 'المتطلبات' },
  { key: 'imports', label: 'الاستيراد' },
  { key: 'data-quality', label: 'جودة البيانات' },
]

function errorMessage(error: unknown) {
  return error instanceof ApiError ? error.message : 'تعذر تنفيذ العملية. تحقق من البيانات وحاول مرة أخرى.'
}

function sectionFromSearch(searchParams: URLSearchParams): WorkforceSection {
  const section = searchParams.get('section')
  return SECTION_NAV.some((item) => item.key === section) ? section as WorkforceSection : 'overview'
}

export function FacilityWorkforcePage() {
  const { facilityId } = useParams()
  const [searchParams, setSearchParams] = useSearchParams()
  const queryClient = useQueryClient()
  const canView = usePermission('Workforce.ViewSummary')
  const canViewCoverage = usePermission('Workforce.ViewCoverage')
  const canViewMembers = usePermission('Workforce.ViewMembers')
  const canImport = usePermission('Workforce.Import')
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')
  const [importRequest, setImportRequest] = useState<WorkforceImportPreviewRequest | null>(null)
  const [importPreview, setImportPreview] = useState<WorkforceImportResult | null>(null)
  const activeSection = sectionFromSearch(searchParams)

  const query = useQuery({
    queryKey: ['workforce-admin', facilityId, canViewCoverage, canViewMembers],
    queryFn: async () => {
      const [summary, coverage, units, roles, members, requirements, rosters, dataQuality] = await Promise.all([
        api.workforce.summary(facilityId!),
        canViewCoverage ? api.workforce.coverage(facilityId!) : Promise.resolve([] as WorkforceCoverageRowPayload[]),
        canViewCoverage ? api.workforce.units(facilityId!) : Promise.resolve([] as WorkforceUnitCoveragePayload[]),
        canViewMembers ? api.workforce.roles(facilityId!) : Promise.resolve([] as WorkforceRoleDefinitionPayload[]),
        canViewMembers ? api.workforce.members(facilityId!, { pageSize: 50 }) : Promise.resolve([] as WorkforceMemberListItem[]),
        canViewCoverage ? api.workforce.requirements(facilityId!) : Promise.resolve([] as StaffingRequirementPayload[]),
        canViewCoverage ? api.workforce.rosters(facilityId!) : Promise.resolve([] as DutyRosterPayload[]),
        api.workforce.dataQuality(facilityId!),
      ])
      return { summary, coverage, units, roles, members, requirements, rosters, dataQuality }
    },
    enabled: canView && Boolean(facilityId),
  })

  const importPreviewMutation = useMutation({
    mutationFn: (body: WorkforceImportPreviewRequest) => api.workforce.importPreview(facilityId!, body),
    onMutate: (body) => {
      setMessage('')
      setError('')
      setImportRequest(body)
      setImportPreview(null)
    },
    onSuccess: (result) => {
      setError('')
      setImportPreview(result)
      setMessage('تم إنشاء معاينة الاستيراد. راجع النتائج قبل التأكيد.')
    },
    onError: (err) => setError(errorMessage(err)),
  })

  const importConfirmMutation = useMutation({
    mutationFn: (body: WorkforceImportPreviewRequest) => api.workforce.importConfirm(facilityId!, body),
    onMutate: () => {
      setMessage('')
      setError('')
    },
    onSuccess: (result) => {
      setError('')
      setImportPreview(result)
      setMessage(`تم تأكيد الاستيراد وتطبيق ${result.appliedRows} صف.`)
      queryClient.invalidateQueries({ queryKey: ['workforce-admin', facilityId] })
      queryClient.invalidateQueries({ queryKey: ['workspace'] })
    },
    onError: (err) => setError(errorMessage(err)),
  })

  if (!facilityId) return <WorkspaceEmpty message="معرّف السجن مطلوب." />
  if (!canView) return <WorkspaceUnauthorized />
  if (query.isLoading) return <WorkspaceLoading />
  if (query.isError) return <WorkspaceError message="تعذر تحميل مركز القوى البشرية." onRetry={() => query.refetch()} />
  if (!query.data) return <WorkspaceEmpty message="لا توجد بيانات قوى بشرية." />

  const { summary, coverage, units, roles, members, requirements, rosters, dataQuality } = query.data
  const setActiveSection = (section: WorkforceSection) => {
    const params = new URLSearchParams(searchParams)
    if (section === 'overview') params.delete('section')
    else params.set('section', section)
    setSearchParams(params, { replace: false })
  }

  return (
    <main className="facility-command-center workforce-admin" dir="rtl">
      <header className="command-header">
        <div>
          <span className="command-eyebrow">مركز القوى البشرية</span>
          <h1>القوى البشرية والتغطية التشغيلية</h1>
          <p>قراءة التغطية والورديات والأعضاء والمتطلبات دون خلطها مع موارد المعدات.</p>
        </div>
        <Link className="command-button ghost" to={`/workspaces/facilities/${facilityId}?section=workforce`}>العودة لمساحة السجن</Link>
      </header>

      {message && <output className="context-action-note" aria-live="polite">{message}</output>}
      {error && <p className="context-action-note" role="alert">{error}</p>}

      <nav className="command-section-nav" aria-label="تنقل مركز القوى البشرية">
        {SECTION_NAV.map(({ key, label }) => (
          <button key={key} type="button" aria-pressed={activeSection === key} onClick={() => setActiveSection(key)}>
            {label}
          </button>
        ))}
      </nav>

      {(activeSection === 'overview' || activeSection === 'coverage') && (
        <OverviewSection summary={summary} coverage={coverage} />
      )}

      {activeSection === 'shifts' && <ShiftsSection coverage={coverage} rosters={rosters} />}
      {activeSection === 'units' && <UnitsSection units={units} />}
      {activeSection === 'roles' && <RolesSection roles={roles} coverage={coverage} />}
      {activeSection === 'members' && (
        canViewMembers
          ? <MembersSection members={members} />
          : <WorkspaceUnauthorized />
      )}
      {activeSection === 'requirements' && <RequirementsSection requirements={requirements} />}
      {activeSection === 'imports' && (
        canImport
          ? (
            <WorkforceImportForm
              pending={importPreviewMutation.isPending || importConfirmMutation.isPending}
              preview={importPreview}
              canConfirm={Boolean(importRequest && importPreview && importPreview.validRows > 0)}
              onPreview={(body) => importPreviewMutation.mutate(body)}
              onConfirm={() => {
                if (importRequest) importConfirmMutation.mutate(importRequest)
              }}
            />
          )
          : <WorkspaceUnauthorized />
      )}
      {activeSection === 'data-quality' && <DataQualitySection dataQuality={dataQuality} />}
    </main>
  )
}

function OverviewSection({
  summary,
  coverage,
}: Readonly<{ summary: WorkforceSummaryPayload; coverage: WorkforceCoverageRowPayload[] }>) {
  const roleGaps = coverage.filter((row) => row.gap > 0)
  return (
    <section className="command-section-stack">
      <div className="occupancy-command-strip" data-status={stripStatus(summary.coverageStatus, summary.gap)}>
        <div>
          <span className="command-eyebrow">تغطية تشغيلية</span>
          <h2>{summary.coverageRate == null ? 'احتياج غير محدد' : `${Math.round(summary.coverageRate * 100)}%`}</h2>
          <p>{summary.warnings.join(' ') || 'لا توجد تحذيرات تغطية حالية.'}</p>
        </div>
        <strong>{summary.operationallyAvailable}/{summary.required || '-'}</strong>
      </div>

      <div className="workforce-rail readiness-rail" aria-label="سكة التغطية">
        <Metric label="المطلوب" value={summary.required} />
        <Metric label="المتاح" value={summary.operationallyAvailable} />
        <Metric label="الحاضر" value={summary.present} />
        <Metric label="الفجوة" value={summary.gap} />
        <Metric label="الحد الآمن" value={summary.minimumSafe} />
      </div>

      <div className="duty-status-band" data-status={stripStatus(summary.coverageStatus, summary.gap)}>
        <span>أعضاء {summary.totalMembers}</span>
        <span>مؤهلون {summary.operationallyEligible}</span>
        <span>مجدولون {summary.scheduled}</span>
        <span>إجازة {summary.onLeave}</span>
        <span>مواقع حرجة {summary.criticalPositionsAtRisk}</span>
      </div>

      {roleGaps.length > 0 && (
        <ul className="priority-row-list" aria-label="فجوات التغطية">
          {roleGaps.map((row) => (
            <li key={`${row.roleDefinitionId}-${row.shiftDefinitionId ?? 'any'}-${row.facilityUnitId ?? 'facility'}`}>
              <article className="priority-row compact" data-tone={row.gap > 0 ? 'warn' : 'ok'}>
                <span className="priority-band" />
                <span><strong>{row.roleNameAr}</strong><small>{row.roleCode} · فجوة {row.gap}</small></span>
                <b>{row.coverageRate == null ? '-' : `${Math.round(row.coverageRate * 100)}%`}</b>
              </article>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

function ShiftsSection({
  coverage,
  rosters,
}: Readonly<{ coverage: WorkforceCoverageRowPayload[]; rosters: DutyRosterPayload[] }>) {
  const shiftRows = coverage.filter((row) => row.shiftCode)
  return (
    <section className="command-section-stack">
      {shiftRows.length === 0 && rosters.length === 0 ? (
        <WorkspaceEmpty message="لا توجد تغطية ورديات أو جداول مسجلة." />
      ) : (
        <>
          {shiftRows.length > 0 && (
            <ul className="shift-coverage-list" aria-label="تغطية الورديات">
              {shiftRows.map((row) => (
                <li key={`shift-${row.roleDefinitionId}-${row.shiftDefinitionId}`}>
                  <div className="shift-coverage-row" data-status={row.gap > 0 ? 'partial' : 'complete'}>
                    <span><strong>{row.shiftCode}</strong><small>{row.roleNameAr}</small></span>
                    <span>مطلوب {row.required}</span>
                    <span>حاضر {row.present}</span>
                    <span>فجوة {row.gap}</span>
                  </div>
                </li>
              ))}
            </ul>
          )}
          {rosters.length > 0 && (
            <ul className="unit-load-list" aria-label="جداول الواجب">
              {rosters.map((roster) => (
                <li key={roster.id}>
                  <article data-status={roster.status === 'Published' ? 'complete' : 'partial'}>
                    <span className="unit-load-title">
                      <strong>{roster.dutyDate}</strong>
                      <small>{roster.shiftDefinitionId}</small>
                    </span>
                    <span>{roster.status}</span>
                    <span className="unit-load-values"><b>{roster.assignmentCount}</b><small>تعيينات</small></span>
                  </article>
                </li>
              ))}
            </ul>
          )}
        </>
      )}
    </section>
  )
}

function UnitsSection({ units }: Readonly<{ units: WorkforceUnitCoveragePayload[] }>) {
  if (units.length === 0) return <WorkspaceEmpty message="لا توجد تغطية وحدات." />
  return (
    <div className="workforce-rail" aria-label="تغطية الوحدات">
      {units.map((unit) => (
        <article key={unit.facilityUnitId ?? unit.unitNameAr} data-status={unit.gap > 0 ? 'partial' : 'complete'}>
          <span>{unit.unitNameAr}</span>
          <strong>{unit.coverageRate == null ? '-' : `${Math.round(unit.coverageRate * 100)}%`}</strong>
          <small>مطلوب {unit.required} · متاح {unit.operationallyAvailable} · فجوة {unit.gap}</small>
        </article>
      ))}
    </div>
  )
}

function RolesSection({
  roles,
  coverage,
}: Readonly<{ roles: WorkforceRoleDefinitionPayload[]; coverage: WorkforceCoverageRowPayload[] }>) {
  if (roles.length === 0) return <WorkspaceEmpty message="لا توجد أدوار معرفة." />
  return (
    <ul className="unit-load-list" aria-label="تعريفات الأدوار">
      {roles.map((role) => {
        const gap = coverage.find((row) => row.roleDefinitionId === role.id)?.gap ?? 0
        return (
          <li key={role.id}>
            <article data-status={gap > 0 ? 'partial' : 'complete'}>
              <span className="unit-load-title"><strong>{role.nameAr}</strong><small>{role.code}</small></span>
              <span>{role.isShiftBased ? 'ورديات' : 'ثابت'}</span>
              <span className="unit-load-values"><b>{gap}</b><small>فجوة</small></span>
            </article>
          </li>
        )
      })}
    </ul>
  )
}

function MembersSection({ members }: Readonly<{ members: WorkforceMemberListItem[] }>) {
  if (members.length === 0) return <WorkspaceEmpty message="لا يوجد أعضاء مسجلون." />
  return (
    <ul className="unit-load-list workforce-members" aria-label="سجلات الأعضاء">
      {members.map((member) => (
        <li key={member.id}>
          <article data-status={member.isOperational ? 'complete' : 'partial'}>
            <span className="unit-load-title">
              <strong>{member.displayName}</strong>
              <small>{member.employeeNumber} · {member.jobTitle}</small>
            </span>
            <span>{employmentLabel(member.employmentStatus)}</span>
            <span className="unit-load-values">
              <b>{member.currentOperationalUnitNameAr ?? '-'}</b>
              <small>الوحدة</small>
              <b>{member.isOperational ? 'نعم' : 'لا'}</b>
              <small>تشغيلي</small>
            </span>
          </article>
        </li>
      ))}
    </ul>
  )
}

function RequirementsSection({ requirements }: Readonly<{ requirements: StaffingRequirementPayload[] }>) {
  if (requirements.length === 0) return <WorkspaceEmpty message="لا توجد متطلبات تسكين مسجلة." />
  return (
    <ul className="unit-load-list" aria-label="متطلبات التسكين">
      {requirements.map((item) => (
        <li key={item.id}>
          <article data-status="complete">
            <span className="unit-load-title">
              <strong>{item.roleCode ?? item.roleDefinitionId}</strong>
              <small>{item.sourceReference}</small>
            </span>
            <span>مطلوب {item.requiredHeadcount}</span>
            <span className="unit-load-values"><b>{item.minimumSafeHeadcount}</b><small>حد آمن</small></span>
          </article>
        </li>
      ))}
    </ul>
  )
}

function DataQualitySection({ dataQuality }: Readonly<{ dataQuality: WorkforceDataQualityPayload }>) {
  return (
    <section className="command-section-stack">
      <div className="workforce-rail readiness-rail" aria-label="جودة بيانات القوى البشرية">
        <Metric label="الأعضاء" value={dataQuality.totalMembers} />
        <Metric label="رقم مفقود" value={dataQuality.missingEmployeeNumber} />
        <Metric label="حالة مجهولة" value={dataQuality.unknownEmploymentStatus} />
        <Metric label="منشأة ناقصة" value={dataQuality.missingHomeOrOperationalFacility} />
        <Metric label="تحقق قديم" value={dataQuality.staleVerification} />
        <Metric label="استيراد مفتوح" value={dataQuality.openImportIssues} />
      </div>
      {dataQuality.warnings.length > 0 && (
        <ul className="priority-row-list" aria-label="تحذيرات الجودة">
          {dataQuality.warnings.map((warning) => (
            <li key={warning}>
              <article className="priority-row compact" data-tone="warn">
                <span className="priority-band" />
                <span><strong>تحذير</strong><small>{warning}</small></span>
              </article>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

function WorkforceImportForm({
  pending,
  preview,
  canConfirm,
  onPreview,
  onConfirm,
}: Readonly<{
  pending: boolean
  preview: WorkforceImportResult | null
  canConfirm: boolean
  onPreview: (body: WorkforceImportPreviewRequest) => void
  onConfirm: () => void
}>) {
  return (
    <section className="command-section-stack" id="import">
      <form
        className="inline-action-form"
        aria-label="معاينة استيراد القوى البشرية"
        onSubmit={(event) => submitForm(event, (data) => onPreview({
          sourceSystem: getFormString(data, 'sourceSystem').trim(),
          sourceReference: getFormString(data, 'sourceReference').trim(),
          fileHash: getFormString(data, 'fileHash').trim(),
          rows: [{
            employeeNumber: getFormString(data, 'employeeNumber').trim(),
            displayName: getFormString(data, 'displayName').trim(),
            externalPersonnelId: getOptionalFormString(data, 'externalPersonnelId'),
            employmentStatus: EmploymentStatus.Active,
            jobTitle: getFormString(data, 'jobTitle').trim(),
            primarySpecialty: getFormString(data, 'primarySpecialty').trim(),
            currentOperationalUnitId: getOptionalFormString(data, 'currentOperationalUnitId'),
            isOperational: true,
          }],
        }))}
      >
        <h2>معاينة استيراد القوى البشرية</h2>
        <label>النظام المصدر<input name="sourceSystem" required defaultValue="manual-csv" /></label>
        <label>مرجع الاستيراد<input name="sourceReference" required defaultValue="D5-1-demo-import" /></label>
        <label>بصمة الملف<input name="fileHash" required defaultValue="phase-d5-1-demo-hash" /></label>
        <label>رقم الموظف<input name="employeeNumber" required defaultValue={`EMP-${Date.now().toString().slice(-6)}`} /></label>
        <label>الاسم المعروض<input name="displayName" required defaultValue="عضو مستورد للمعاينة" /></label>
        <label>المعرّف الخارجي<input name="externalPersonnelId" /></label>
        <label>المسمى الوظيفي<input name="jobTitle" required defaultValue="ضابط أمن" /></label>
        <label>التخصص الأساسي<input name="primarySpecialty" required defaultValue="أمن" /></label>
        <label>معرّف الوحدة التشغيلية<input name="currentOperationalUnitId" /></label>
        <input type="hidden" name="sourceType" value={WorkforceSourceType.Import} />
        <button type="submit" className="command-button" disabled={pending}>{pending ? 'جار المعاينة...' : 'معاينة الاستيراد'}</button>
      </form>

      {preview && (
        <div className="occupancy-command-strip" data-status={preview.rejectedRows > 0 ? 'partial' : 'complete'}>
          <div>
            <span className="command-eyebrow">نتيجة المعاينة</span>
            <h2>{preview.validRows}/{preview.totalRows}</h2>
            <p>مرفوض {preview.rejectedRows} · مكرر {preview.duplicateRows} · مطبق {preview.appliedRows}</p>
            {preview.errors.length > 0 && <p role="alert">{preview.errors.join(' ')}</p>}
          </div>
          <button type="button" className="command-button primary" disabled={pending || !canConfirm} onClick={onConfirm}>تأكيد الاستيراد</button>
        </div>
      )}
    </section>
  )
}

function Metric({ label, value }: Readonly<{ label: string; value: number | string }>) {
  return <div className="command-metric" data-tone="info"><span>{label}</span><strong>{value}</strong></div>
}

function submitForm(event: FormEvent<HTMLFormElement>, handler: (data: FormData) => void) {
  event.preventDefault()
  handler(new FormData(event.currentTarget))
}

function getFormString(data: FormData, key: string): string {
  const value = data.get(key)
  return typeof value === 'string' ? value : ''
}

function getOptionalFormString(data: FormData, key: string): string | undefined {
  const value = getFormString(data, key).trim()
  return value || undefined
}

function stripStatus(status: number, gap: number) {
  if (status === WorkforceCoverageStatus.Unsafe || status === WorkforceCoverageStatus.Critical || gap > 0) return 'partial'
  if (status === WorkforceCoverageStatus.Ready) return 'complete'
  if (status === WorkforceCoverageStatus.Unknown) return 'missing'
  return 'attention'
}

function employmentLabel(status: number) {
  if (status === EmploymentStatus.Active) return 'نشط'
  if (status === EmploymentStatus.SecondedIn) return 'إعارة واردة'
  if (status === EmploymentStatus.SecondedOut) return 'إعارة صادرة'
  if (status === EmploymentStatus.Suspended) return 'موقوف'
  if (status === EmploymentStatus.LongLeave) return 'إجازة طويلة'
  if (status === EmploymentStatus.Retired) return 'متقاعد'
  if (status === EmploymentStatus.Terminated) return 'منتهٍ'
  return 'غير معروف'
}
