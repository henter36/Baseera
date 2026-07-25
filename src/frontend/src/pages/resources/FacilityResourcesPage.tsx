import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useParams, useSearchParams } from 'react-router'
import {
  ApiError,
  api,
  ResourceCondition,
  ResourceCriticality,
  ResourceSourceType,
  ResourceStatus,
  ResourceType,
  type ResourceAssetCreateRequest,
  type ResourceImportPreviewRequest,
  type ResourceImportResult,
} from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import { WorkspaceEmpty, WorkspaceError, WorkspaceLoading, WorkspaceUnauthorized } from '../../workspaces/WorkspaceShell'

function errorMessage(error: unknown) {
  return error instanceof ApiError ? error.message : 'تعذر تنفيذ العملية. تحقق من البيانات وحاول مرة أخرى.'
}

export function FacilityResourcesPage() {
  const { facilityId } = useParams()
  const [searchParams, setSearchParams] = useSearchParams()
  const queryClient = useQueryClient()
  const canView = usePermission('Resources.ViewSummary')
  const canViewAssets = usePermission('Resources.ViewAssets')
  const canManageAssets = usePermission('Resources.ManageAssets')
  const canImport = usePermission('Resources.Import')
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')
  const [importRequest, setImportRequest] = useState<ResourceImportPreviewRequest | null>(null)
  const [importPreview, setImportPreview] = useState<ResourceImportResult | null>(null)
  const resourceType = searchParams.get('type')

  const query = useQuery({
    queryKey: ['resources-admin', facilityId, resourceType],
    queryFn: async () => {
      const filters = resourceType ? { resourceType } : {}
      const [summary, categories, exceptions, assets] = await Promise.all([
        api.resources.summary(facilityId!),
        canViewAssets ? api.resources.categories(facilityId!) : Promise.resolve([]),
        canViewAssets ? api.resources.exceptions(facilityId!, 20) : Promise.resolve([]),
        canViewAssets ? api.resources.assets(facilityId!, filters) : Promise.resolve([]),
      ])
      return { summary, categories, exceptions, assets }
    },
    enabled: canView && Boolean(facilityId),
  })

  const createMutation = useMutation({
    mutationFn: (body: ResourceAssetCreateRequest) => api.resources.createAsset(facilityId!, body),
    onMutate: () => {
      setMessage('')
      setError('')
    },
    onSuccess: () => {
      setError('')
      setMessage('تم إنشاء المورد وربطه بسجل حالة وموضع افتتاحي.')
      queryClient.invalidateQueries({ queryKey: ['resources-admin', facilityId] })
      queryClient.invalidateQueries({ queryKey: ['workspace'] })
    },
    onError: (err) => setError(errorMessage(err)),
  })

  const importPreviewMutation = useMutation({
    mutationFn: (body: ResourceImportPreviewRequest) => api.resources.importPreview(facilityId!, body),
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
    mutationFn: (body: ResourceImportPreviewRequest) => api.resources.importConfirm(facilityId!, body),
    onMutate: () => {
      setMessage('')
      setError('')
    },
    onSuccess: (result) => {
      setError('')
      setImportPreview(result)
      setMessage(`تم تأكيد الاستيراد وتطبيق ${result.appliedRows} صف.`)
      queryClient.invalidateQueries({ queryKey: ['resources-admin', facilityId] })
      queryClient.invalidateQueries({ queryKey: ['workspace'] })
    },
    onError: (err) => setError(errorMessage(err)),
  })

  if (!facilityId) return <WorkspaceEmpty message="معرّف السجن مطلوب." />
  if (!canView) return <WorkspaceUnauthorized />
  if (query.isLoading) return <WorkspaceLoading />
  if (query.isError) return <WorkspaceError message="تعذر تحميل مركز الموارد." onRetry={() => query.refetch()} />
  if (!query.data) return <WorkspaceEmpty message="لا توجد بيانات موارد." />

  const { summary, categories, exceptions, assets } = query.data

  return (
    <main className="facility-command-center resources-admin" dir="rtl">
      <header className="command-header">
        <div>
          <span className="command-eyebrow">مركز الموارد</span>
          <h1>جاهزية الموارد والأصول الأساسية</h1>
          <p>إدارة المركبات وأجهزة الاتصال والمعدات والأصول الثابتة دون القوى البشرية أو الأسلحة.</p>
        </div>
        <Link className="command-button ghost" to={`/workspaces/facilities/${facilityId}?section=resources`}>العودة لمساحة السجن</Link>
      </header>

      {message && <output className="context-action-note" aria-live="polite">{message}</output>}
      {error && <p className="context-action-note" role="alert">{error}</p>}

      <section className="occupancy-command-strip" data-status={summary.gap > 0 ? 'partial' : 'complete'}>
        <div>
          <span className="command-eyebrow">جاهزية تشغيلية</span>
          <h2>{summary.readinessRate == null ? 'احتياج غير محدد' : `${Math.round(summary.readinessRate * 100)}%`}</h2>
          <p>{summary.warnings.join(' ') || 'لا توجد تحذيرات موارد حالية.'}</p>
        </div>
        <strong>{summary.operational}/{summary.required || '-'}</strong>
      </section>

      <section className="command-section-stack">
        <div className="readiness-rail">
          <Metric label="إجمالي" value={summary.totalRegistered} />
          <Metric label="متاح" value={summary.available} />
          <Metric label="قيد الاستخدام" value={summary.inUse} />
          <Metric label="صيانة" value={summary.underMaintenance} />
          <Metric label="خارج الخدمة" value={summary.outOfService} />
          <Metric label="الفجوة" value={summary.gap} />
        </div>

        <div className="resource-rail">
          {categories.map((category) => (
            <button
              key={category.resourceTypeCode}
              type="button"
              data-status={categoryRailStatus(category.gap, category.total)}
              onClick={() => {
                const params = new URLSearchParams(searchParams)
                params.set('type', String(category.resourceType))
                setSearchParams(params)
              }}
            >
              <span>{category.labelAr}</span>
              <strong>{category.readinessRate == null ? '-' : `${Math.round(category.readinessRate * 100)}%`}</strong>
              <small>{category.operational} تشغيلي · فجوة {category.gap}</small>
            </button>
          ))}
        </div>

        {exceptions.length > 0 && (
          <ul className="priority-row-list" aria-label="استثناءات الموارد">
            {exceptions.map((item) => (
              <li key={`${item.type}-${item.reference}`}>
                <article className="priority-row compact" data-tone={item.priorityRank >= 900 ? 'danger' : 'warn'}>
                  <span className="priority-band" />
                  <span><strong>{item.titleAr}</strong><small>{item.reference} · {item.reasonAr}</small></span>
                  <b>{item.severityAr}</b>
                </article>
              </li>
            ))}
          </ul>
        )}

        {canViewAssets && (
          <ul className="unit-load-list resource-assets" aria-label="سجلات الموارد">
            {assets.map((asset) => (
              <li key={asset.id}>
                <article data-status={assetRailStatus(asset.currentStatus)}>
                  <span className="unit-load-title"><strong>{asset.displayName}</strong><small>{asset.assetCode} · {resourceTypeLabel(asset.resourceType)}</small></span>
                  <span>{statusLabel(asset.currentStatus)}</span>
                  <span className="unit-load-values"><b>{asset.operationalFacilityUnitNameAr ?? '-'}</b><small>الموقع</small><b>{asset.hasOpenMaintenance ? 'نعم' : 'لا'}</b><small>صيانة</small></span>
                </article>
              </li>
            ))}
          </ul>
        )}
      </section>

      {canManageAssets && <ResourceCreateForm pending={createMutation.isPending} onSubmit={(body) => createMutation.mutate(body)} />}
      {canImport && (
        <ResourceImportForm
          pending={importPreviewMutation.isPending || importConfirmMutation.isPending}
          preview={importPreview}
          canConfirm={Boolean(importRequest && importPreview && importPreview.validRows > 0)}
          onPreview={(body) => importPreviewMutation.mutate(body)}
          onConfirm={() => {
            if (importRequest) importConfirmMutation.mutate(importRequest)
          }}
        />
      )}
    </main>
  )
}

function Metric({ label, value }: Readonly<{ label: string; value: number | string }>) {
  return <div className="command-metric" data-tone="info"><span>{label}</span><strong>{value}</strong></div>
}

function ResourceCreateForm({ pending, onSubmit }: Readonly<{ pending: boolean; onSubmit: (body: ResourceAssetCreateRequest) => void }>) {
  return (
    <form className="inline-action-form" aria-label="إضافة مورد أساسي" onSubmit={(event) => submitForm(event, (data) => onSubmit({
      resourceType: Number(getFormString(data, 'resourceType')) as ResourceType,
      assetCode: getFormString(data, 'assetCode').trim(),
      displayName: getFormString(data, 'displayName').trim(),
      serialNumber: getOptionalFormString(data, 'serialNumber'),
      ownershipOrganizationId: getFormString(data, 'ownershipOrganizationId').trim(),
      operationalFacilityUnitId: getOptionalFormString(data, 'operationalFacilityUnitId'),
      currentStatus: ResourceStatus.Available,
      condition: ResourceCondition.Good,
      criticality: ResourceCriticality.Medium,
      sourceType: ResourceSourceType.Manual,
      sourceReference: getOptionalFormString(data, 'sourceReference'),
    }))}>
      <h2>إضافة مورد أساسي</h2>
      <label>
        <span>الفئة</span>
        <select name="resourceType" defaultValue={ResourceType.Vehicle}>
          <option value={ResourceType.Vehicle}>مركبة</option>
          <option value={ResourceType.CommunicationDevice}>جهاز اتصال</option>
          <option value={ResourceType.OperationalEquipment}>معدات تشغيلية</option>
          <option value={ResourceType.SecurityEquipment}>معدات أمنية غير أسلحة</option>
          <option value={ResourceType.FacilityAsset}>أصل ثابت</option>
        </select>
      </label>
      <label>كود المورد<input name="assetCode" required /></label>
      <label>الاسم<input name="displayName" required /></label>
      <label>الرقم التسلسلي<input name="serialNumber" /></label>
      <label>معرّف المنظمة المالكة<input name="ownershipOrganizationId" required /></label>
      <label>معرّف الوحدة التشغيلية<input name="operationalFacilityUnitId" /></label>
      <label>مرجع المصدر<input name="sourceReference" /></label>
      <button type="submit" className="command-button" disabled={pending}>{pending ? 'جار الحفظ…' : 'إنشاء المورد'}</button>
    </form>
  )
}

function ResourceImportForm({
  pending,
  preview,
  canConfirm,
  onPreview,
  onConfirm,
}: Readonly<{
  pending: boolean
  preview: ResourceImportResult | null
  canConfirm: boolean
  onPreview: (body: ResourceImportPreviewRequest) => void
  onConfirm: () => void
}>) {
  return (
    <section className="command-section-stack" id="import">
      <form className="inline-action-form" aria-label="معاينة استيراد الموارد" onSubmit={(event) => submitForm(event, (data) => onPreview({
        sourceSystem: getFormString(data, 'sourceSystem').trim(),
        sourceReference: getFormString(data, 'sourceReference').trim(),
        fileHash: getFormString(data, 'fileHash').trim(),
        rows: [{
          resourceType: Number(getFormString(data, 'resourceType')) as ResourceType,
          assetCode: getFormString(data, 'assetCode').trim(),
          displayName: getFormString(data, 'displayName').trim(),
          serialNumber: getOptionalFormString(data, 'serialNumber'),
          currentStatus: Number(getFormString(data, 'currentStatus')) as ResourceStatus,
          condition: ResourceCondition.Good,
          criticality: ResourceCriticality.Medium,
        }],
      }))}>
        <h2>معاينة استيراد الموارد</h2>
        <label>النظام المصدر<input name="sourceSystem" required defaultValue="manual-csv" /></label>
        <label>مرجع الاستيراد<input name="sourceReference" required defaultValue="D5-demo-import" /></label>
        <label>بصمة الملف<input name="fileHash" required defaultValue="phase-d5-demo-hash" /></label>
        <label>
          <span>الفئة</span>
          <select name="resourceType" defaultValue={ResourceType.OperationalEquipment}>
            <option value={ResourceType.Vehicle}>مركبة</option>
            <option value={ResourceType.CommunicationDevice}>جهاز اتصال</option>
            <option value={ResourceType.OperationalEquipment}>معدات تشغيلية</option>
            <option value={ResourceType.SecurityEquipment}>معدات أمنية غير أسلحة</option>
            <option value={ResourceType.FacilityAsset}>أصل ثابت</option>
          </select>
        </label>
        <label>كود المورد<input name="assetCode" required defaultValue={`IMP-${Date.now().toString().slice(-6)}`} /></label>
        <label>الاسم<input name="displayName" required defaultValue="مورد مستورد للمعاينة" /></label>
        <label>الرقم التسلسلي<input name="serialNumber" /></label>
        <label>
          <span>الحالة</span>
          <select name="currentStatus" defaultValue={ResourceStatus.Available}>
            <option value={ResourceStatus.Available}>متاح</option>
            <option value={ResourceStatus.UnderMaintenance}>تحت الصيانة</option>
            <option value={ResourceStatus.OutOfService}>خارج الخدمة</option>
            <option value={ResourceStatus.Unknown}>غير معروف</option>
          </select>
        </label>
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

function resourceTypeLabel(type: ResourceType) {
  if (type === ResourceType.Vehicle) return 'مركبة'
  if (type === ResourceType.CommunicationDevice) return 'جهاز اتصال'
  if (type === ResourceType.FacilityAsset) return 'أصل ثابت'
  return 'معدات'
}

function statusLabel(status: ResourceStatus) {
  if (status === ResourceStatus.Available) return 'متاح'
  if (status === ResourceStatus.InUse) return 'قيد الاستخدام'
  if (status === ResourceStatus.UnderMaintenance) return 'تحت الصيانة'
  if (status === ResourceStatus.OutOfService) return 'خارج الخدمة'
  if (status === ResourceStatus.AwaitingParts) return 'بانتظار قطع'
  return 'غير معروف'
}

function categoryRailStatus(gap: number, total: number) {
  if (gap > 0) return 'partial'
  if (total > 0) return 'complete'
  return 'missing'
}

function assetRailStatus(status: ResourceStatus) {
  if (status === ResourceStatus.OutOfService) return 'unavailable'
  if (status === ResourceStatus.Unknown) return 'missing'
  return 'complete'
}
