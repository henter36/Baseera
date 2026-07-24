import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useParams } from 'react-router'
import { api } from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import { WorkspaceEmpty, WorkspaceError, WorkspaceLoading, WorkspaceUnauthorized } from '../../workspaces/WorkspaceShell'

const nowIso = () => new Date().toISOString()

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
  const filters = useMemo(() => ({ asOfUtc: nowIso(), fromUtc: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(), toUtc: nowIso() }), [])

  const summary = useQuery({
    queryKey: ['occupancy-admin', facilityId, filters],
    queryFn: () => Promise.all([
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
          netMovement: 0,
          dailyTrend: [],
          rejectedMovements: 0,
        }),
    ]),
    enabled: canView && Boolean(facilityId),
  })

  const capacityMutation = useMutation({
    mutationFn: (body: Record<string, unknown>) => api.occupancy.recordCapacity(facilityId!, body),
    onSuccess: () => {
      setMessage('تم تسجيل الطاقة المعتمدة كسجل تاريخي جديد.')
      queryClient.invalidateQueries({ queryKey: ['occupancy-admin', facilityId] })
      queryClient.invalidateQueries({ queryKey: ['workspace'] })
    },
  })
  const snapshotMutation = useMutation({
    mutationFn: (body: Record<string, unknown>) => api.occupancy.recordSnapshot(facilityId!, body),
    onSuccess: () => {
      setMessage('تم تسجيل Snapshot الإشغال دون حفظ هوية نزيل.')
      queryClient.invalidateQueries({ queryKey: ['occupancy-admin', facilityId] })
      queryClient.invalidateQueries({ queryKey: ['workspace'] })
    },
  })
  const importMutation = useMutation({
    mutationFn: (body: Record<string, unknown>) => api.occupancy.importMovements(facilityId!, body),
    onSuccess: (result) => {
      setMessage(`تم قبول ${result.acceptedRows} حركة، وتجاهل ${result.duplicateRows} مكررة.`)
      queryClient.invalidateQueries({ queryKey: ['occupancy-admin', facilityId] })
      queryClient.invalidateQueries({ queryKey: ['workspace'] })
    },
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
              <button type="button" data-status={unit.statusCode}>
                <span className="unit-load-title"><strong>{unit.unitNameAr}</strong><small>{unit.unitCode} · {unit.statusAr}</small></span>
                <span className="unit-capacity-bar"><i style={{ inlineSize: `${Math.min(120, Math.round((unit.occupancyRate ?? 0) * 100))}%` }} /></span>
                <span className="unit-load-values"><b>{unit.currentCount ?? '-'}</b><small>عدد</small><b>{unit.approvedCapacity ?? '-'}</b><small>طاقة</small></span>
              </button>
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

function CapacityForm({ onSubmit, pending }: Readonly<{ onSubmit: (body: Record<string, unknown>) => void; pending: boolean }>) {
  return (
    <form className="inline-action-form" onSubmit={(event) => submitForm(event, (data) => onSubmit({
      approvedCapacity: Number(data.get('approvedCapacity')),
      effectiveFromUtc: data.get('effectiveFromUtc'),
      sourceReference: data.get('sourceReference'),
      capacityType: 0,
      sourceType: 0,
    }))}>
      <h2>تسجيل طاقة معتمدة</h2>
      <label>العدد<input name="approvedCapacity" type="number" min="1" required /></label>
      <label>يسري من<input name="effectiveFromUtc" type="datetime-local" required /></label>
      <label>مرجع المصدر<input name="sourceReference" required /></label>
      <button type="submit" className="command-button" disabled={pending}>حفظ الطاقة</button>
    </form>
  )
}

function SnapshotForm({ onSubmit, pending }: Readonly<{ onSubmit: (body: Record<string, unknown>) => void; pending: boolean }>) {
  return (
    <form className="inline-action-form" onSubmit={(event) => submitForm(event, (data) => onSubmit({
      capturedAtUtc: data.get('capturedAtUtc'),
      inmateCount: Number(data.get('inmateCount')),
      sourceReference: data.get('sourceReference'),
      sourceType: 0,
      isAuthoritative: true,
      qualityStatus: 0,
    }))}>
      <h2>تسجيل Snapshot</h2>
      <label>وقت الالتقاط<input name="capturedAtUtc" type="datetime-local" required /></label>
      <label>العدد<input name="inmateCount" type="number" min="0" required /></label>
      <label>مرجع المصدر<input name="sourceReference" required /></label>
      <button type="submit" className="command-button" disabled={pending}>حفظ Snapshot</button>
    </form>
  )
}

function MovementImportForm({ onSubmit, pending }: Readonly<{ onSubmit: (body: Record<string, unknown>) => void; pending: boolean }>) {
  return (
    <form className="inline-action-form" onSubmit={(event) => submitForm(event, (data) => onSubmit({
      sourceSystem: data.get('sourceSystem'),
      importReference: data.get('importReference'),
      rows: [{
        inmateReferenceHash: data.get('inmateReferenceHash'),
        movementType: Number(data.get('movementType')),
        toFacilityId: data.get('toFacilityId') || undefined,
        fromFacilityId: data.get('fromFacilityId') || undefined,
        occurredAtUtc: data.get('occurredAtUtc'),
        externalEventId: data.get('externalEventId'),
      }],
    }))}>
      <h2>استيراد حركة مضبوطة</h2>
      <label>نظام المصدر<input name="sourceSystem" required /></label>
      <label>مرجع الاستيراد<input name="importReference" required /></label>
      <label>Hash النزيل<input name="inmateReferenceHash" required /></label>
      <label>نوع الحركة<select name="movementType" defaultValue="0"><option value="0">دخول</option><option value="1">إفراج</option><option value="2">نقل إلى السجن</option><option value="3">نقل خارج السجن</option></select></label>
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
