import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useParams, useSearchParams } from 'react-router'
import {
  ApiError,
  api,
  AvailabilityType,
  EmploymentStatus,
  WorkforceCoverageStatus,
  WorkforceImportKind,
  WorkforceSourceType,
  type DutyRosterPayload,
  type StaffingRequirementPayload,
  type WorkforceCoverageRowPayload,
  type WorkforceCriticalPosition,
  type WorkforceDataQualityPayload,
  type WorkforceImportPreviewRequest,
  type WorkforceImportResult,
  type WorkforceMemberListItem,
  type WorkforceMemberUpdateRequest,
  type WorkforceQualificationListItem,
  type WorkforceReconciliationItem,
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
  | 'qualifications'
  | 'requirements'
  | 'availability'
  | 'imports'
  | 'data-quality'
  | 'reconciliation'

const SECTION_NAV: ReadonlyArray<Readonly<{ key: WorkforceSection; label: string }>> = [
  { key: 'overview', label: 'المشهد العام' },
  { key: 'coverage', label: 'التغطية' },
  { key: 'shifts', label: 'الورديات' },
  { key: 'units', label: 'الوحدات' },
  { key: 'roles', label: 'الأدوار' },
  { key: 'members', label: 'الأعضاء' },
  { key: 'qualifications', label: 'المؤهلات' },
  { key: 'requirements', label: 'المتطلبات' },
  { key: 'availability', label: 'التوفر' },
  { key: 'imports', label: 'الاستيراد' },
  { key: 'data-quality', label: 'جودة البيانات' },
  { key: 'reconciliation', label: 'المصالحة' },
]

const IMPORT_KIND_OPTIONS: ReadonlyArray<Readonly<{ value: WorkforceImportKind; label: string }>> = [
  { value: WorkforceImportKind.PersonnelMaster, label: 'سجل الأفراد' },
  { value: WorkforceImportKind.Assignments, label: 'التكليفات' },
  { value: WorkforceImportKind.Qualifications, label: 'المؤهلات' },
  { value: WorkforceImportKind.Rosters, label: 'جداول الواجب' },
  { value: WorkforceImportKind.Availability, label: 'التوفر' },
  { value: WorkforceImportKind.AttendanceSummary, label: 'ملخص الحضور' },
]

function errorMessage(error: unknown) {
  if (!(error instanceof ApiError)) return 'تعذر تنفيذ العملية. تحقق من البيانات وحاول مرة أخرى.'
  if (error.status === 403) return 'ليست لديك صلاحية تنفيذ هذه العملية.'
  if (error.status === 404) return 'السجل غير موجود ضمن نطاق السجن.'
  if (error.status === 409) return 'تعارض في البيانات. حدّث الصفحة ثم أعد المحاولة.'
  if (error.status === 422) return error.message || 'البيانات غير صالحة.'
  return error.message
}

function sectionFromSearch(searchParams: URLSearchParams): WorkforceSection {
  const section = searchParams.get('section')
  return SECTION_NAV.some((item) => item.key === section) ? section as WorkforceSection : 'overview'
}

async function optionalPart<T>(name: string, loader: () => Promise<T>, fallback: T, failures: string[]) {
  try {
    return await loader()
  } catch {
    failures.push(name)
    return fallback
  }
}

export function FacilityWorkforcePage() {
  const { facilityId } = useParams()
  const [searchParams, setSearchParams] = useSearchParams()
  const queryClient = useQueryClient()
  const canView = usePermission('Workforce.ViewSummary')
  const canViewCoverage = usePermission('Workforce.ViewCoverage')
  const canViewMembers = usePermission('Workforce.ViewMembers')
  const canManageMembers = usePermission('Workforce.ManageMembers')
  const canRecordAvailability = usePermission('Workforce.RecordAvailability')
  const canImport = usePermission('Workforce.Import')
  const canReconcile = usePermission('Workforce.Reconcile')
  const canExport = usePermission('Workforce.Export')
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')
  const [exportNote, setExportNote] = useState('')
  const [importRequest, setImportRequest] = useState<WorkforceImportPreviewRequest | null>(null)
  const [importPreview, setImportPreview] = useState<WorkforceImportResult | null>(null)
  const [editingMemberId, setEditingMemberId] = useState<string | null>(null)
  const activeSection = sectionFromSearch(searchParams)

  const query = useQuery({
    queryKey: ['workforce-admin', facilityId, canViewCoverage, canViewMembers, canReconcile],
    queryFn: async () => {
      const partialFailures: string[] = []
      const summary = await api.workforce.summary(facilityId!)
      const [coverage, units, roles, members, requirements, rosters, dataQuality, criticalPositions, reconciliation] = await Promise.all([
        optionalPart('coverage', () => canViewCoverage ? api.workforce.coverage(facilityId!) : Promise.resolve([]), [] as WorkforceCoverageRowPayload[], partialFailures),
        optionalPart('units', () => canViewCoverage ? api.workforce.units(facilityId!) : Promise.resolve([]), [] as WorkforceUnitCoveragePayload[], partialFailures),
        optionalPart('roles', () => canViewMembers ? api.workforce.roles(facilityId!) : Promise.resolve([]), [] as WorkforceRoleDefinitionPayload[], partialFailures),
        optionalPart('members', () => canViewMembers ? api.workforce.members(facilityId!, { pageSize: 50 }) : Promise.resolve([]), [] as WorkforceMemberListItem[], partialFailures),
        optionalPart('requirements', () => canViewCoverage ? api.workforce.requirements(facilityId!) : Promise.resolve([]), [] as StaffingRequirementPayload[], partialFailures),
        optionalPart('rosters', () => canViewCoverage ? api.workforce.rosters(facilityId!) : Promise.resolve([]), [] as DutyRosterPayload[], partialFailures),
        optionalPart('data-quality', () => api.workforce.dataQuality(facilityId!), {
          totalMembers: summary.totalMembers,
          missingEmployeeNumber: summary.missingDataRecords,
          unknownEmploymentStatus: 0,
          missingHomeOrOperationalFacility: 0,
          staleVerification: summary.staleRecords,
          openImportIssues: 0,
          warnings: summary.warnings,
          issues: [],
        } as WorkforceDataQualityPayload, partialFailures),
        optionalPart('critical-positions', () => canViewCoverage ? api.workforce.criticalPositions(facilityId!) : Promise.resolve([]), [] as WorkforceCriticalPosition[], partialFailures),
        optionalPart('reconciliation', () => canReconcile ? api.workforce.reconciliation(facilityId!, { page: 1, pageSize: 50 }) : Promise.resolve({ items: [], totalCount: 0, page: 1, pageSize: 50 }), { items: [], totalCount: 0, page: 1, pageSize: 50 }, partialFailures),
      ])
      return { summary, coverage, units, roles, members, requirements, rosters, dataQuality, criticalPositions, reconciliation, partialFailures }
    },
    enabled: canView && Boolean(facilityId),
  })

  const qualificationsQuery = useQuery({
    queryKey: ['workforce-qualifications', facilityId],
    queryFn: async () => (await api.workforce.qualifications(facilityId!, { page: 1, pageSize: 100 })).items,
    enabled: canViewMembers && activeSection === 'qualifications' && Boolean(facilityId),
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

  const updateMemberMutation = useMutation({
    mutationFn: ({ memberId, body }: { memberId: string; body: WorkforceMemberUpdateRequest }) =>
      api.workforce.updateMember(facilityId!, memberId, body),
    onMutate: () => {
      setMessage('')
      setError('')
    },
    onSuccess: () => {
      setEditingMemberId(null)
      setMessage('تم تحديث سجل العضو.')
      queryClient.invalidateQueries({ queryKey: ['workforce-admin', facilityId] })
      queryClient.invalidateQueries({ queryKey: ['workforce-qualifications', facilityId] })
    },
    onError: (err) => setError(errorMessage(err)),
  })

  const availabilityMutation = useMutation({
    mutationFn: (body: Parameters<typeof api.workforce.createAvailability>[1]) =>
      api.workforce.createAvailability(facilityId!, body),
    onMutate: () => {
      setMessage('')
      setError('')
    },
    onSuccess: () => {
      setMessage('تم تسجيل حدث التوفر.')
      queryClient.invalidateQueries({ queryKey: ['workforce-admin', facilityId] })
    },
    onError: (err) => setError(errorMessage(err)),
  })

  const resolveMutation = useMutation({
    mutationFn: ({ itemId, resolutionAction }: { itemId: string; resolutionAction: string }) =>
      api.workforce.resolveReconciliation(facilityId!, itemId, { resolutionAction }),
    onMutate: () => {
      setMessage('')
      setError('')
    },
    onSuccess: () => {
      setMessage('تم تسجيل قرار المصالحة.')
      queryClient.invalidateQueries({ queryKey: ['workforce-admin', facilityId] })
    },
    onError: (err) => setError(errorMessage(err)),
  })

  const exportMutation = useMutation({
    mutationFn: () => api.workforce.export(facilityId!, { pageSize: 500 }),
    onMutate: () => {
      setMessage('')
      setError('')
      setExportNote('')
    },
    onSuccess: ({ blob, fileName }) => {
      const url = URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      anchor.href = url
      anchor.download = fileName || 'workforce-export.csv'
      anchor.click()
      URL.revokeObjectURL(url)
      setExportNote('تم التصدير بحقول محدودة مع إخفاء البيانات الشخصية الحساسة. لا يتضمن الملف قيودًا طبية أو حضورًا خامًا.')
      setMessage('اكتمل التصدير.')
    },
    onError: (err) => setError(errorMessage(err)),
  })

  if (!facilityId) return <WorkspaceEmpty message="معرّف السجن مطلوب." />
  if (!canView) return <WorkspaceUnauthorized />
  if (query.isLoading) return <WorkspaceLoading />
  if (query.isError) return <WorkspaceError message={errorMessage(query.error) || 'تعذر تحميل مركز القوى البشرية.'} onRetry={() => query.refetch()} />
  if (!query.data) return <WorkspaceEmpty message="لا توجد بيانات قوى بشرية." />

  const { summary, coverage, units, roles, members, requirements, rosters, dataQuality, criticalPositions, reconciliation } = query.data
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
        <div className="command-header-actions">
          {canExport && (
            <button
              type="button"
              className="command-button ghost"
              disabled={exportMutation.isPending}
              onClick={() => exportMutation.mutate()}
            >
              {exportMutation.isPending ? 'جار التصدير...' : 'تصدير محدود'}
            </button>
          )}
          <Link className="command-button ghost" to={`/workspaces/facilities/${facilityId}?section=workforce`}>العودة لمساحة السجن</Link>
        </div>
      </header>

      {message && <output className="context-action-note" aria-live="polite">{message}</output>}
      {error && <p className="context-action-note" role="alert">{error}</p>}
      {exportNote && <p className="context-action-note" data-testid="export-redaction-note">{exportNote}</p>}
      {query.data.partialFailures.length > 0 && (
        <p className="context-action-note" role="status">تعذر تحميل بعض أجزاء المركز، وتبقى البيانات المتاحة ظاهرة.</p>
      )}

      <nav className="command-section-nav" aria-label="تنقل مركز القوى البشرية">
        {SECTION_NAV.map(({ key, label }) => (
          <button key={key} type="button" aria-pressed={activeSection === key} onClick={() => setActiveSection(key)}>
            {label}
          </button>
        ))}
      </nav>

      {(activeSection === 'overview' || activeSection === 'coverage') && (
        <OverviewSection summary={summary} coverage={coverage} criticalPositions={criticalPositions} />
      )}

      {activeSection === 'shifts' && <ShiftsSection coverage={coverage} rosters={rosters} />}
      {activeSection === 'units' && <UnitsSection units={units} />}
      {activeSection === 'roles' && <RolesSection roles={roles} coverage={coverage} />}
      {activeSection === 'members' && (
        canViewMembers
          ? (
            <MembersSection
              members={members}
              canManage={canManageMembers}
              editingMemberId={editingMemberId}
              pending={updateMemberMutation.isPending}
              onEdit={setEditingMemberId}
              onCancelEdit={() => setEditingMemberId(null)}
              onSave={(memberId, body) => updateMemberMutation.mutate({ memberId, body })}
            />
          )
          : <WorkspaceUnauthorized />
      )}
      {activeSection === 'qualifications' && (
        canViewMembers
          ? <QualificationsSection items={qualificationsQuery.data ?? []} loading={qualificationsQuery.isLoading} />
          : <WorkspaceUnauthorized />
      )}
      {activeSection === 'requirements' && <RequirementsSection requirements={requirements} />}
      {activeSection === 'availability' && (
        canRecordAvailability
          ? (
            <AvailabilityForm
              members={members}
              pending={availabilityMutation.isPending}
              onSubmit={(body) => availabilityMutation.mutate(body)}
            />
          )
          : <WorkspaceUnauthorized />
      )}
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
      {activeSection === 'reconciliation' && (
        canReconcile
          ? (
            <ReconciliationSection
              items={reconciliation.items}
              totalCount={reconciliation.totalCount}
              pending={resolveMutation.isPending}
              onResolve={(itemId) => resolveMutation.mutate({ itemId, resolutionAction: 'acknowledge' })}
            />
          )
          : <WorkspaceUnauthorized />
      )}
    </main>
  )
}

function OverviewSection({
  summary,
  coverage,
  criticalPositions,
}: Readonly<{
  summary: WorkforceSummaryPayload
  coverage: WorkforceCoverageRowPayload[]
  criticalPositions: WorkforceCriticalPosition[]
}>) {
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

      {criticalPositions.length > 0 && (
        <ul className="priority-row-list" aria-label="المواقع الحرجة">
          {criticalPositions.map((item) => (
            <li key={item.id}>
              <article className="priority-row compact" data-tone={item.singlePointOfFailure || item.vacantPrimary > 0 ? 'warn' : 'ok'}>
                <span className="priority-band" />
                <span>
                  <strong>{item.roleNameAr}</strong>
                  <small>{item.statusAr} · أساسي شاغر {item.vacantPrimary} · بديل شاغر {item.vacantAlternate}</small>
                </span>
                <b>{item.singlePointOfFailure ? 'نقطة فشل' : 'مغطى'}</b>
              </article>
            </li>
          ))}
        </ul>
      )}

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

function MembersSection({
  members,
  canManage,
  editingMemberId,
  pending,
  onEdit,
  onCancelEdit,
  onSave,
}: Readonly<{
  members: WorkforceMemberListItem[]
  canManage: boolean
  editingMemberId: string | null
  pending: boolean
  onEdit: (id: string) => void
  onCancelEdit: () => void
  onSave: (memberId: string, body: WorkforceMemberUpdateRequest) => void
}>) {
  if (members.length === 0) return <WorkspaceEmpty message="لا يوجد أعضاء مسجلون." />
  return (
    <ul className="unit-load-list workforce-members" aria-label="سجلات الأعضاء">
      {members.map((member) => (
        <li key={member.id}>
          {editingMemberId === member.id ? (
            <MemberEditForm
              member={member}
              pending={pending}
              onCancel={onCancelEdit}
              onSave={(body) => onSave(member.id, body)}
            />
          ) : (
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
              {canManage && (
                <button type="button" className="command-button ghost" onClick={() => onEdit(member.id)}>تعديل</button>
              )}
            </article>
          )}
        </li>
      ))}
    </ul>
  )
}

function MemberEditForm({
  member,
  pending,
  onCancel,
  onSave,
}: Readonly<{
  member: WorkforceMemberListItem
  pending: boolean
  onCancel: () => void
  onSave: (body: WorkforceMemberUpdateRequest) => void
}>) {
  return (
    <form
      className="inline-action-form"
      aria-label={`تعديل عضو ${member.displayName}`}
      onSubmit={(event) => submitForm(event, (data) => onSave({
        displayName: getFormString(data, 'displayName').trim(),
        employmentStatus: Number(getFormString(data, 'employmentStatus')) as EmploymentStatus,
        jobTitle: getFormString(data, 'jobTitle').trim(),
        primarySpecialty: getFormString(data, 'primarySpecialty').trim(),
        currentOperationalUnitId: getOptionalFormString(data, 'currentOperationalUnitId') ?? null,
        isOperational: data.get('isOperational') === 'on',
        isSensitiveRole: data.get('isSensitiveRole') === 'on',
        rowVersion: member.rowVersion ?? null,
      }))}
    >
      <h2>تعديل {member.displayName}</h2>
      <label>الاسم المعروض<input name="displayName" required defaultValue={member.displayName} /></label>
      <label>المسمى<input name="jobTitle" required defaultValue={member.jobTitle} /></label>
      <label>التخصص<input name="primarySpecialty" required defaultValue={member.primarySpecialty} /></label>
      <label>
        الحالة
        <select name="employmentStatus" defaultValue={String(member.employmentStatus)}>
          <option value={EmploymentStatus.Active}>نشط</option>
          <option value={EmploymentStatus.SecondedIn}>إعارة واردة</option>
          <option value={EmploymentStatus.SecondedOut}>إعارة صادرة</option>
          <option value={EmploymentStatus.Suspended}>موقوف</option>
          <option value={EmploymentStatus.LongLeave}>إجازة طويلة</option>
          <option value={EmploymentStatus.Retired}>متقاعد</option>
          <option value={EmploymentStatus.Terminated}>منتهٍ</option>
          <option value={EmploymentStatus.Unknown}>غير معروف</option>
        </select>
      </label>
      <label>معرّف الوحدة<input name="currentOperationalUnitId" defaultValue={member.currentOperationalUnitId ?? ''} /></label>
      <label><input type="checkbox" name="isOperational" defaultChecked={member.isOperational} /> تشغيلي</label>
      <label><input type="checkbox" name="isSensitiveRole" defaultChecked={member.isSensitiveRole} /> دور حساس</label>
      <div className="inline-action-row">
        <button type="submit" className="command-button primary" disabled={pending}>{pending ? 'جار الحفظ...' : 'حفظ'}</button>
        <button type="button" className="command-button ghost" onClick={onCancel}>إلغاء</button>
      </div>
    </form>
  )
}

function QualificationsSection({
  items,
  loading,
}: Readonly<{
  items: WorkforceQualificationListItem[]
  loading: boolean
}>) {
  if (loading) return <WorkspaceLoading />
  if (items.length === 0) return <WorkspaceEmpty message="لا توجد مؤهلات مسجلة للأعضاء المعروضين." />
  return (
    <ul className="unit-load-list" aria-label="قائمة المؤهلات">
      {items.map((item) => (
        <li key={item.id}>
          <article data-status={item.expiresAtUtc ? 'partial' : 'complete'}>
            <span className="unit-load-title">
              <strong>{item.name}</strong>
              <small>{item.memberDisplayName}</small>
            </span>
            <span>حالة {item.status}</span>
            <span className="unit-load-values">
              <b>{item.expiresAtUtc ? item.expiresAtUtc.slice(0, 10) : '-'}</b>
              <small>انتهاء</small>
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

function AvailabilityForm({
  members,
  pending,
  onSubmit,
}: Readonly<{
  members: WorkforceMemberListItem[]
  pending: boolean
  onSubmit: (body: Parameters<typeof api.workforce.createAvailability>[1]) => void
}>) {
  if (members.length === 0) return <WorkspaceEmpty message="لا يوجد أعضاء لتسجيل التوفر." />
  return (
    <form
      className="inline-action-form"
      aria-label="تسجيل توفر عضو"
      onSubmit={(event) => submitForm(event, (data) => onSubmit({
        workforceMemberId: getFormString(data, 'workforceMemberId'),
        availabilityType: Number(getFormString(data, 'availabilityType')) as typeof AvailabilityType[keyof typeof AvailabilityType],
        startsAtUtc: new Date(getFormString(data, 'startsAtUtc')).toISOString(),
        endsAtUtc: getOptionalFormString(data, 'endsAtUtc')
          ? new Date(getFormString(data, 'endsAtUtc')).toISOString()
          : undefined,
        affectsOperationalAvailability: data.get('affectsOperationalAvailability') === 'on',
        sourceType: WorkforceSourceType.Manual,
        reasonCode: getOptionalFormString(data, 'reasonCode'),
      }))}
    >
      <h2>تسجيل توفر / غياب</h2>
      <label>
        العضو
        <select name="workforceMemberId" required defaultValue={members[0]?.id}>
          {members.map((member) => (
            <option key={member.id} value={member.id}>{member.displayName}</option>
          ))}
        </select>
      </label>
      <label>
        نوع التوفر
        <select name="availabilityType" defaultValue={AvailabilityType.AnnualLeave}>
          <option value={AvailabilityType.Available}>متاح</option>
          <option value={AvailabilityType.AnnualLeave}>إجازة سنوية</option>
          <option value={AvailabilityType.SickLeave}>إجازة مرضية</option>
          <option value={AvailabilityType.Training}>دورة</option>
          <option value={AvailabilityType.ExternalAssignment}>انتداب خارجي</option>
          <option value={AvailabilityType.RestrictedDuty}>مقيد</option>
          <option value={AvailabilityType.UnexcusedAbsence}>غياب غير مبرر</option>
        </select>
      </label>
      <label>يبدأ في<input name="startsAtUtc" type="datetime-local" required /></label>
      <label>ينتهي في<input name="endsAtUtc" type="datetime-local" /></label>
      <label>رمز السبب<input name="reasonCode" /></label>
      <label><input type="checkbox" name="affectsOperationalAvailability" defaultChecked /> يؤثر على التوفر التشغيلي</label>
      <button type="submit" className="command-button primary" disabled={pending}>{pending ? 'جار الحفظ...' : 'تسجيل'}</button>
    </form>
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

function ReconciliationSection({
  items,
  totalCount,
  pending,
  onResolve,
}: Readonly<{
  items: WorkforceReconciliationItem[]
  totalCount: number
  pending: boolean
  onResolve: (itemId: string) => void
}>) {
  if (items.length === 0) return <WorkspaceEmpty message="لا توجد بنود مصالحة مفتوحة." />
  return (
    <section className="command-section-stack">
      <p className="context-action-note">بنود مفتوحة: {totalCount}</p>
      <ul className="unit-load-list" aria-label="بنود المصالحة">
        {items.map((item) => (
          <li key={item.id}>
            <article data-status={item.severity === 'high' || item.severity === 'critical' ? 'partial' : 'attention'}>
              <span className="unit-load-title">
                <strong>{item.titleAr}</strong>
                <small>{item.issueType} · {item.severity}</small>
              </span>
              <span>{item.detailAr}</span>
              <span className="unit-load-values">
                <b>{item.suggestedActionAr}</b>
                <small>{item.responsibleHintAr}</small>
              </span>
              <button type="button" className="command-button ghost" disabled={pending} onClick={() => onResolve(item.id)}>
                معالجة
              </button>
            </article>
          </li>
        ))}
      </ul>
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
          importKind: Number(getFormString(data, 'importKind')) as WorkforceImportKind,
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
        <label>
          نوع الاستيراد
          <select name="importKind" defaultValue={WorkforceImportKind.PersonnelMaster} aria-label="نوع الاستيراد">
            {IMPORT_KIND_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>{option.label}</option>
            ))}
          </select>
        </label>
      <label>النظام المصدر<input name="sourceSystem" required /></label>
      <label>مرجع الاستيراد<input name="sourceReference" required /></label>
      <label>بصمة الملف<input name="fileHash" required /></label>
      <label>رقم الموظف<input name="employeeNumber" required /></label>
      <label>الاسم المعروض<input name="displayName" required /></label>
      <label>المعرّف الخارجي<input name="externalPersonnelId" /></label>
      <label>المسمى الوظيفي<input name="jobTitle" required /></label>
      <label>التخصص الأساسي<input name="primarySpecialty" required /></label>
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
