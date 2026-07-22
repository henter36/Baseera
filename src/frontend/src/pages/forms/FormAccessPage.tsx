import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { api, ApiError } from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import {
  FormAccessCapabilityLabelsAr,
  FormAccessGrantEffectLabelsAr,
  FormAccessGrantPrincipalTypeLabelsAr,
  ScopeType,
  ScopeTypeLabelsAr,
  enumOptions,
} from '../../forms/formEnums'
import { type CreateFormAccessGrantFormValues, createFormAccessGrantSchema } from '../../forms/formSchema'

const GRANT_SCOPE_TYPES = new Set<number>([
  ScopeType.Global,
  ScopeType.Headquarters,
  ScopeType.Region,
  ScopeType.Facility,
])

function formatDate(value?: string | null): string {
  if (!value) return '—'
  return new Date(value).toLocaleString('ar-SA')
}

function toIsoOrUndefined(value?: string): string | undefined {
  if (!value) return undefined
  const d = new Date(value)
  return Number.isNaN(d.getTime()) ? undefined : d.toISOString()
}

export function FormAccessPage() {
  const canManage = usePermission('Forms.ManageAccess')
  const { id } = useParams<{ id: string }>()
  const queryClient = useQueryClient()
  const [serverError, setServerError] = useState<string | null>(null)
  const [revokeDialog, setRevokeDialog] = useState<{
    grantId: string
    rowVersion: string
    principal: string
    capability: string
  } | null>(null)
  const [revokeReason, setRevokeReason] = useState('')
  const [revokeGrantId, setRevokeGrantId] = useState<string | null>(null)
  const [revokeConflict, setRevokeConflict] = useState(false)
  const revokeDialogRef = useRef<HTMLDialogElement>(null)

  useEffect(() => {
    const dialog = revokeDialogRef.current
    if (!dialog) return
    if (revokeDialog) {
      if (!dialog.open) dialog.showModal()
    } else if (dialog.open) {
      dialog.close()
    }
  }, [revokeDialog])

  const closeRevokeDialog = () => {
    setRevokeDialog(null)
    setRevokeReason('')
    if (!revokeConflict) setServerError(null)
  }

  const formQuery = useQuery({
    queryKey: ['form', id],
    queryFn: () => api.forms.get(id!),
    enabled: canManage && !!id,
  })

  const grantsQuery = useQuery({
    queryKey: ['form-access-grants', id],
    queryFn: () => api.forms.accessGrants(id!),
    enabled: canManage && !!id,
  })

  const regionsQuery = useQuery({
    queryKey: ['form-access-regions'],
    queryFn: () => api.regions(),
    enabled: canManage,
  })

  const { register, handleSubmit, watch, reset, formState: { errors } } = useForm<CreateFormAccessGrantFormValues>({
    resolver: zodResolver(createFormAccessGrantSchema),
    defaultValues: {
      principalType: '0',
      principalId: '',
      capability: '0',
      effect: '0',
      scopeType: '',
      regionId: '',
      facilityId: '',
      validFromUtc: '',
      validToUtc: '',
      reason: '',
    },
  })

  const scopeType = watch('scopeType')
  const regionId = watch('regionId')

  const facilitiesQuery = useQuery({
    queryKey: ['form-access-facilities', regionId],
    queryFn: () => api.facilities(regionId || undefined),
    enabled: canManage && !!regionId,
  })

  const createMutation = useMutation({
    mutationFn: (values: CreateFormAccessGrantFormValues) => {
      const scope = values.scopeType === '' ? null : Number(values.scopeType)
      return api.forms.createAccessGrant(id!, {
        principalType: Number(values.principalType),
        principalId: values.principalId,
        capability: Number(values.capability),
        effect: Number(values.effect),
        scopeType: scope,
        regionId: scope === ScopeType.Region || scope === ScopeType.Facility ? values.regionId || null : null,
        facilityId: scope === ScopeType.Facility ? values.facilityId || null : null,
        validFromUtc: toIsoOrUndefined(values.validFromUtc) ?? null,
        validToUtc: toIsoOrUndefined(values.validToUtc) ?? null,
        reason: values.reason,
      })
    },
    onSuccess: () => {
      reset()
      setServerError(null)
      void queryClient.invalidateQueries({ queryKey: ['form-access-grants', id] })
    },
    onError: (err: unknown) => {
      if (err instanceof ApiError) {
        if (err.status === 403) setServerError('ليست لديك صلاحية إدارة الوصول.')
        else setServerError(err.message)
      } else {
        setServerError('تعذر إنشاء المنح.')
      }
    },
  })

  const openRevokeDialog = (grant: { id: string; rowVersion: string; principalDisplayName?: string | null; principalId: string; capabilityAr: string }) => {
    setServerError(null)
    setRevokeConflict(false)
    setRevokeReason('')
    setRevokeDialog({
      grantId: grant.id,
      rowVersion: grant.rowVersion,
      principal: grant.principalDisplayName || grant.principalId,
      capability: grant.capabilityAr,
    })
  }

  const confirmRevoke = async () => {
    if (!revokeDialog) return
    if (!revokeReason.trim()) {
      setServerError('سبب الإلغاء مطلوب.')
      return
    }
    setRevokeGrantId(revokeDialog.grantId)
    setServerError(null)
    setRevokeConflict(false)
    try {
      await api.forms.revokeAccessGrant(id!, revokeDialog.grantId, {
        reason: revokeReason,
        rowVersion: revokeDialog.rowVersion,
      })
      setRevokeDialog(null)
      setRevokeReason('')
      await queryClient.invalidateQueries({ queryKey: ['form-access-grants', id] })
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) {
        setRevokeConflict(true)
        setServerError(err.message || 'تعارض: المنح ملغى مسبقًا أو تغيّر إصدار السجل.')
      } else {
        setServerError(err instanceof ApiError ? err.message : 'تعذر إلغاء المنح.')
      }
    } finally {
      setRevokeGrantId(null)
    }
  }

  if (!canManage) {
    return <div className="error" role="alert">ليست لديك صلاحية إدارة وصول النماذج.</div>
  }

  if (formQuery.isLoading || grantsQuery.isLoading) {
    return <div className="loading">جاري التحميل…</div>
  }

  if (formQuery.isError) {
    const err = formQuery.error as ApiError
    return (
      <div className="error" role="alert">
        {err.status === 404 ? 'النموذج غير موجود أو خارج نطاقك.' : err.message}
      </div>
    )
  }

  const form = formQuery.data
  if (!form) return <div className="empty">النموذج غير موجود.</div>

  const scopeNum = scopeType === '' ? null : Number(scopeType)

  return (
    <div className="panel" dir="rtl">
      <div className="page-header">
        <div>
          <h1 className="page-title">إدارة الوصول — {form.code}</h1>
          <p className="muted">{form.nameAr}</p>
        </div>
        <Link to={`/forms/${form.id}`}><button type="button" className="secondary">العودة للتفاصيل</button></Link>
      </div>

      <div className="panel-section">
        <h2 className="section-title">منح جديد</h2>
        <form onSubmit={handleSubmit((values) => {
          if (createMutation.isPending) return
          createMutation.mutate(values)
        })}>
          <div className="form-grid">
            <label className="field">
              <span>نوع المستفيد *</span>
              <select aria-label="نوع المستفيد" {...register('principalType')}>
                {enumOptions(FormAccessGrantPrincipalTypeLabelsAr).map((o) => (
                  <option key={o.value} value={o.value}>{o.labelAr}</option>
                ))}
              </select>
            </label>
            <label className="field">
              <span>معرف المستفيد (UUID) *</span>
              <input aria-label="معرف المستفيد" {...register('principalId')} />
              {errors.principalId && <span className="field-error">{errors.principalId.message}</span>}
            </label>
            <label className="field">
              <span>الصلاحية *</span>
              <select aria-label="الصلاحية" {...register('capability')}>
                {enumOptions(FormAccessCapabilityLabelsAr).map((o) => (
                  <option key={o.value} value={o.value}>{o.labelAr}</option>
                ))}
              </select>
            </label>
            <label className="field">
              <span>التأثير *</span>
              <select aria-label="التأثير" {...register('effect')}>
                {enumOptions(FormAccessGrantEffectLabelsAr).map((o) => (
                  <option key={o.value} value={o.value}>{o.labelAr}</option>
                ))}
              </select>
            </label>
            <label className="field">
              <span>نطاق المنح (اختياري)</span>
              <select aria-label="نطاق المنح" {...register('scopeType')}>
                <option value="">بدون تقييد نطاق</option>
                {enumOptions(ScopeTypeLabelsAr)
                  .filter((o) => GRANT_SCOPE_TYPES.has(o.value))
                  .map((o) => (
                    <option key={o.value} value={o.value}>{o.labelAr}</option>
                  ))}
              </select>
            </label>
            {(scopeNum === ScopeType.Region || scopeNum === ScopeType.Facility) && (
              <label className="field">
                <span>المنطقة *</span>
                <select aria-label="منطقة المنح" {...register('regionId')}>
                  <option value="">اختر المنطقة</option>
                  {regionsQuery.data?.items.map((r) => (
                    <option key={r.id} value={r.id}>{r.nameAr}</option>
                  ))}
                </select>
                {errors.regionId && <span className="field-error">{errors.regionId.message}</span>}
              </label>
            )}
            {scopeNum === ScopeType.Facility && (
              <label className="field">
                <span>السجن *</span>
                <select aria-label="سجن المنح" {...register('facilityId')}>
                  <option value="">اختر السجن</option>
                  {facilitiesQuery.data?.items.map((f) => (
                    <option key={f.id} value={f.id}>{f.nameAr}</option>
                  ))}
                </select>
                {errors.facilityId && <span className="field-error">{errors.facilityId.message}</span>}
              </label>
            )}
            <label className="field">
              <span>صالح من</span>
              <input aria-label="صالح من" type="datetime-local" {...register('validFromUtc')} />
            </label>
            <label className="field">
              <span>صالح إلى</span>
              <input aria-label="صالح إلى" type="datetime-local" {...register('validToUtc')} />
              {errors.validToUtc && <span className="field-error">{errors.validToUtc.message}</span>}
            </label>
            <label className="field field-wide">
              <span>سبب المنح *</span>
              <textarea aria-label="سبب المنح" rows={2} {...register('reason')} />
              {errors.reason && <span className="field-error">{errors.reason.message}</span>}
            </label>
          </div>
          {serverError && <div className="error" role="alert">{serverError}</div>}
          <div className="form-actions">
            <button type="submit" disabled={createMutation.isPending}>
              {createMutation.isPending ? 'جارٍ الحفظ…' : 'إضافة منح'}
            </button>
          </div>
        </form>
      </div>

      <div className="panel-section">
        <h2 className="section-title">المنح الحالية</h2>
        {grantsQuery.isError && (
          <div className="error" role="alert">
            <span>تعذر تحميل المنح.</span>
            <button type="button" className="secondary" onClick={() => grantsQuery.refetch()}>إعادة المحاولة</button>
          </div>
        )}
        {grantsQuery.data?.length === 0 && <div className="empty">لا توجد منح وصول.</div>}
        {grantsQuery.data && grantsQuery.data.length > 0 && (
          <table>
            <thead>
              <tr>
                <th>المستفيد</th>
                <th>الصلاحية</th>
                <th>التأثير</th>
                <th>الصلاحية الزمنية</th>
                <th>السبب</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {grantsQuery.data.map((grant) => (
                <tr key={grant.id}>
                  <td>{grant.principalDisplayName || grant.principalId}</td>
                  <td>{grant.capabilityAr}</td>
                  <td>{grant.effect === 0 ? 'سماح' : 'منع'}</td>
                  <td>{formatDate(grant.validFromUtc)} — {formatDate(grant.validToUtc)}</td>
                  <td>{grant.reason}</td>
                  <td>
                    <button
                      type="button"
                      className="secondary"
                      disabled={revokeGrantId === grant.id}
                      onClick={() => openRevokeDialog(grant)}
                    >
                      إلغاء
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <dialog
        ref={revokeDialogRef}
        className="panel-section"
        aria-label="تأكيد إلغاء المنح"
        onCancel={(e) => {
          if (revokeGrantId) {
            e.preventDefault()
            return
          }
          closeRevokeDialog()
        }}
        onClose={() => {
          if (revokeDialog) closeRevokeDialog()
        }}
      >
        <h2 className="section-title">تأكيد إلغاء المنح</h2>
        {revokeDialog && (
          <p>
            المستفيد: <strong>{revokeDialog.principal}</strong>
            {' — '}
            الصلاحية: <strong>{revokeDialog.capability}</strong>
          </p>
        )}
        <label className="field field-wide">
          <span>سبب الإلغاء *</span>
          <input
            aria-label="سبب إلغاء المنح"
            value={revokeReason}
            onChange={(e) => setRevokeReason(e.target.value)}
            disabled={!!revokeGrantId}
            autoFocus
          />
        </label>
        {serverError && <div className="error" role="alert">{serverError}</div>}
        <div className="form-actions">
          <button
            type="button"
            disabled={!!revokeGrantId || !revokeDialog}
            onClick={() => void confirmRevoke()}
          >
            {revokeGrantId ? 'جارٍ الإلغاء…' : 'تأكيد الإلغاء'}
          </button>
          <button
            type="button"
            className="secondary"
            disabled={!!revokeGrantId}
            onClick={closeRevokeDialog}
          >
            إغلاق
          </button>
        </div>
      </dialog>
    </div>
  )
}
