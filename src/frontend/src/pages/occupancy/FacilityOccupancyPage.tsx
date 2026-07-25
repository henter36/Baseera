import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useParams } from 'react-router'
import {
  ApiError,
  api,
  CensusQualityStatus,
  OccupancyCapacityType,
  OccupancyMovementType,
  OccupancySourceType,
  type OccupancyCapacityRequest,
  type OccupancyMovementImportRequest,
  type OccupancySnapshotRequest,
} from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import { WorkspaceEmpty, WorkspaceError, WorkspaceLoading, WorkspaceUnauthorized } from '../../workspaces/WorkspaceShell'
import { getFormString, getOptionalFormString } from './facilityOccupancyFormData'
import { riyadhDateTimeLocalToUtc } from './occupancyDateTime'

const nowIso = () => new Date().toISOString()
const THIRTY_DAYS_MS = 30 * 24 * 60 * 60 * 1000

function currentFilters() {
  const toUtc = nowIso()
  return {
    asOfUtc: toUtc,
    fromUtc: new Date(Date.parse(toUtc) - THIRTY_DAYS_MS).toISOString(),
    toUtc,
  }
}

function mutationErrorMessage(error: unknown): string {
  if (error instanceof ApiError && error.message) {
    return error.message
  }

  return 'تعذر تنفيذ العملية. تحقق من البيانات وحاول مرة أخرى.'
}

function movementTypeFromForm(value: FormDataEntryValue | null): OccupancyMovementType {
  const parsed = Number(value)
  for (const supported of Object.values(OccupancyMovementType)) {
    if (parsed === supported) {
      return supported
    }
  }

  throw new Error('نوع الحركة غير مدعوم.')
}

export function FacilityOccupancyPage() {
  const { facilityId } = useParams()
  const canView = usePermission('Occupancy.ViewSummary')
  const canViewUnits = usePermission('Occupancy.ViewUnitBreakdown')
  const canViewMovements = usePermission('Occupancy.ViewMovements')
  const canManageCapacity = usePermission('Occupancy.ManageCapacity')
  const canRecordSnapshot = usePermission('Occupancy.RecordSnapshot')
  const canImport = usePermission('Occupancy.Import')
  const queryClient = useQueryClient()
  const [message, setMessage] = useState('')
  const [errorMessage, setErrorMessage] = useState('')

  const summary = useQuery({
    queryKey: ['occupancy-admin', facilityId, canViewUnits, canViewMovements],
    queryFn: () => {
      const filters = currentFilters()
      return Promise.all([
        api.occupancy.summary(facilityId!, { asOfUtc: filters.asOfUtc }),
        canViewUnits ? api.occupancy.units(facilityId!, { asOfUtc: filters.asOfUtc }) : Promise.resolve({ units: [] }),
        canViewMovements
          ? api.occupancy.movementsSummary(facilityId!, { fromUtc: filters.fromUtc, toUtc: filters.toUtc })
          : Promise.resolve({
            admissions: 0,
            releases: 0,
            transferIn: 0,
            transferOut: 0,
            internalTransfers: 0,
            temporaryLeave: 0,
            returns: 0,
            death: 0,
            hospitalTransfers: 0,
            courtTransfers: 0,
            corrections: 0,
            otherMovements: 0,
            netMovement: 0,
            dailyTrend: [],
          }),
      ])
    },
    enabled: canView && Boolean(facilityId),
  })

  const capacityMutation = useMutation({
    mutationFn: (body: OccupancyCapacityRequest) => api.occupancy.recordCapacity(facilityId!, body),
    onMutate: () => {
      setMessage('')
      setErrorMessage('')
    },
    onSuccess: () => {
      setErrorMessage('')
      setMessage('تم تسجيل الطاقة المعتمدة كسجل تاريخي جديد.')
      queryClient.invalidateQueries({ queryKey: ['occupancy-admin', facilityId] })
      queryClient.invalidateQueries({ queryKey: ['workspace'] })
    },
    onError: (error) => setErrorMessage(mutationErrorMessage(error)),
  })
  const snapshotMutation = useMutation({
    mutationFn: (body: OccupancySnapshotRequest) => api.occupancy.recordSnapshot(facilityId!, body),
    onMutate: () => {
      setMessage('')
      setErrorMessage('')
    },
    onSuccess: () => {
      setErrorMessage('')
      setMessage('تم تسجيل Snapshot الإشغال دون حفظ هوية نزيل.')
      queryClient.invalidateQueries({ queryKey: ['occupancy-admin', facilityId] })
      queryClient.invalidateQueries({ queryKey: ['workspace'] })
    },
    onError: (error) => setErrorMessage(mutationErrorMessage(error)),
  })
  const importMutation = useMutation({
    mutationFn: (body: OccupancyMovementImportRequest) => api.occupancy.importMovements(facilityId!, body),
    onMutate: () => {
      setMessage('')
      setErrorMessage('')
    },
    onSuccess: (result) => {
      setErrorMessage('')
      setMessage(`تم قبول ${result.acceptedRows} حركة، وتجاهل ${result.duplicateRows} مكررة.`)
      queryClient.invalidateQueries({ queryKey: ['occupancy-admin', facilityId] })
      queryClient.invalidateQueries({ queryKey: ['workspace'] })
    },
    onError: (error) => setErrorMessage(mutationErrorMessage(error)),
  })

  if (!facilityId) return <WorkspaceEmpty message="معرّف السجن مطلوب." />
  if (!canView) return <WorkspaceUnauthorized />
  if (summary.isLoading) return <WorkspaceLoading />
  if (summary.isError) return <WorkspaceError message="تعذر تحميل إدارة الإشغال." onRetry={() => summary.refetch()} />
  if (!summary.data) return <WorkspaceEmpty message="لا توجد بيانات إشغال." />

  const [overview, units, movements] = summary.data

  return (
    <main className="facility-command-center occupancy-admin" dir="rtl">
      <header className="command-header">
        <div>
          <span className="command-eyebrow">إدارة الإشغال</span>
          <h1>الطاقة الاستيعابية وحركة النزلاء</h1>
          <p>تسجيل منضبط للطاقة وSnapshot والحركات دون عرض بيانات تعريفية للنزلاء.</p>
        </div>
        <Link className="command-button ghost" to={`/workspaces/facilities/${facilityId}?section=occupancy`}>العودة لمساحة السجن</Link>
      </header>

      {message && <output className="context-action-note" aria-live="polite">{message}</output>}
      {errorMessage && <p className="context-action-note" role="alert">{errorMessage}</p>}

      <section className="occupancy-command-strip" data-status={overview.statusCode}>
        <div>
          <span className="command-eyebrow">الحالة الحالية</span>
          <h2>{overview.statusAr}</h2>
          <p>{overview.sourceAr} · {overview.warnings.join(' ') || 'لا توجد تحذيرات جودة بيانات.'}</p>
        </div>
        <strong>{overview.occupancyRate == null ? '-' : `${Math.round(overview.occupancyRate * 100)}%`}</strong>
      </section>

      <section className="command-section-stack">
        <div className="readiness-rail">
          <Metric label="الطاقة" value={overview.approvedCapacity ?? '-'} />
          <Metric label="العدد" value={overview.currentCount ?? '-'} />
          <Metric label="الشواغر" value={overview.availablePlaces ?? '-'} />
          <Metric label="التجاوز" value={overview.overCapacityCount ?? 0} />
          <Metric label="دخول" value={movements.admissions} />
          <Metric label="إفراج" value={movements.releases} />
          <Metric label="صافي الحركة" value={movements.netMovement} />
        </div>
        <ul className="unit-load-list occupancy-units" aria-label="وحدات الإشغال">
          {units.units.map((unit) => (
            <li key={unit.unitId}>
              <article data-status={unit.statusCode}>
                <span className="unit-load-title"><strong>{unit.unitNameAr}</strong><small>{unit.unitCode} · {unit.statusAr}</small></span>
                <span
                  className="unit-capacity-bar"
                  aria-label={`نسبة إشغال وحدة ${unit.unitNameAr}: ${Math.round((unit.occupancyRate ?? 0) * 100)}%`}
                >
                  <i style={{ inlineSize: `${Math.min(120, Math.round((unit.occupancyRate ?? 0) * 100))}%` }} />
                </span>
                <span className="unit-load-values"><b>{unit.currentCount ?? '-'}</b><small>عدد</small><b>{unit.approvedCapacity ?? '-'}</b><small>طاقة</small></span>
              </article>
            </li>
          ))}
        </ul>
      </section>

      <section className="occupancy-admin-forms">
        {canManageCapacity && <CapacityForm onSubmit={(body) => capacityMutation.mutate(body)} pending={capacityMutation.isPending} />}
        {canRecordSnapshot && <SnapshotForm onSubmit={(body) => snapshotMutation.mutate(body)} pending={snapshotMutation.isPending} />}
        {canImport && <MovementImportForm onSubmit={(body) => importMutation.mutate(body)} pending={importMutation.isPending} />}
      </section>
    </main>
  )
}

function Metric({ label, value }: Readonly<{ label: string; value: string | number }>) {
  return <div className="command-metric" data-tone="info"><span>{label}</span><strong>{value}</strong></div>
}

function CapacityForm({ onSubmit, pending }: Readonly<{ onSubmit: (body: OccupancyCapacityRequest) => void; pending: boolean }>) {
  return (
    <form className="inline-action-form" onSubmit={(event) => submitForm(event, (data) => onSubmit({
      approvedCapacity: Number(data.get('approvedCapacity')),
      effectiveFromUtc: riyadhDateTimeLocalToUtc(getFormString(data, 'effectiveFromUtc')),
      sourceReference: getFormString(data, 'sourceReference').trim(),
      capacityType: OccupancyCapacityType.ApprovedOperational,
      sourceType: OccupancySourceType.Manual,
    }))}>
      <h2>تسجيل طاقة معتمدة</h2>
      <label>العدد<input name="approvedCapacity" type="number" min="1" required /></label>
      <label>يسري من<input name="effectiveFromUtc" type="datetime-local" required /></label>
      <label>مرجع المصدر<input name="sourceReference" required /></label>
      <button type="submit" className="command-button" disabled={pending}>حفظ الطاقة</button>
    </form>
  )
}

function SnapshotForm({ onSubmit, pending }: Readonly<{ onSubmit: (body: OccupancySnapshotRequest) => void; pending: boolean }>) {
  return (
    <form className="inline-action-form" onSubmit={(event) => submitForm(event, (data) => onSubmit({
      capturedAtUtc: riyadhDateTimeLocalToUtc(getFormString(data, 'capturedAtUtc')),
      inmateCount: Number(data.get('inmateCount')),
      sourceReference: getFormString(data, 'sourceReference').trim(),
      sourceType: OccupancySourceType.Manual,
      isAuthoritative: true,
      qualityStatus: CensusQualityStatus.Complete,
    }))}>
      <h2>تسجيل Snapshot</h2>
      <label>وقت الالتقاط<input name="capturedAtUtc" type="datetime-local" required /></label>
      <label>العدد<input name="inmateCount" type="number" min="0" required /></label>
      <label>مرجع المصدر<input name="sourceReference" required /></label>
      <button type="submit" className="command-button" disabled={pending}>حفظ Snapshot</button>
    </form>
  )
}

function MovementImportForm({ onSubmit, pending }: Readonly<{ onSubmit: (body: OccupancyMovementImportRequest) => void; pending: boolean }>) {
  return (
    <form className="inline-action-form" onSubmit={(event) => submitForm(event, (data) => onSubmit({
      sourceSystem: getFormString(data, 'sourceSystem').trim(),
      importReference: getFormString(data, 'importReference').trim(),
      rows: [{
        inmateReferenceHash: getFormString(data, 'inmateReferenceHash').trim(),
        movementType: movementTypeFromForm(data.get('movementType')),
        toFacilityId: getOptionalFormString(data, 'toFacilityId'),
        fromFacilityId: getOptionalFormString(data, 'fromFacilityId'),
        occurredAtUtc: riyadhDateTimeLocalToUtc(getFormString(data, 'occurredAtUtc')),
        externalEventId: getFormString(data, 'externalEventId').trim(),
      }],
    }))}>
      <h2>استيراد حركة مضبوطة</h2>
      <label>نظام المصدر<input name="sourceSystem" required /></label>
      <label>مرجع الاستيراد<input name="importReference" required /></label>
      <label>Hash النزيل<input name="inmateReferenceHash" required /></label>
      <label>نوع الحركة<select name="movementType" defaultValue={String(OccupancyMovementType.Admission)}><option value={String(OccupancyMovementType.Admission)}>دخول</option><option value={String(OccupancyMovementType.Release)}>إفراج</option><option value={String(OccupancyMovementType.TransferIn)}>نقل إلى السجن</option><option value={String(OccupancyMovementType.TransferOut)}>نقل خارج السجن</option></select></label>
      <label>من سجن<input name="fromFacilityId" /></label>
      <label>إلى سجن<input name="toFacilityId" /></label>
      <label>وقت الحركة<input name="occurredAtUtc" type="datetime-local" required /></label>
      <label>معرف الحدث الخارجي<input name="externalEventId" required /></label>
      <button type="submit" className="command-button" disabled={pending}>استيراد الحركة</button>
    </form>
  )
}

function submitForm(event: FormEvent<HTMLFormElement>, submit: (data: FormData) => void) {
  event.preventDefault()
  submit(new FormData(event.currentTarget))
}
