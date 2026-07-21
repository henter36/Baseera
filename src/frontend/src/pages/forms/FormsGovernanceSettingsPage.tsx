import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { api, ApiError, type UpdateFormGovernancePolicyRequest } from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import { type UpdateFormGovernancePolicyFormValues, updateFormGovernancePolicySchema } from '../../forms/formSchema'

export function FormsGovernanceSettingsPage() {
  const canManage = usePermission('Forms.ManageGovernance')
  const [serverError, setServerError] = useState<string | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)
  const [conflict, setConflict] = useState(false)
  const [rowVersion, setRowVersion] = useState('')

  const policyQuery = useQuery({
    queryKey: ['form-governance-policy'],
    queryFn: () => api.formGovernance.getPolicy(),
    enabled: canManage,
  })

  const { register, handleSubmit, reset, formState: { errors } } = useForm<UpdateFormGovernancePolicyFormValues>({
    resolver: zodResolver(updateFormGovernancePolicySchema),
    defaultValues: {
      requireReviewBeforeApproval: true,
      requireSeparationOfDuties: true,
      allowDesignerToReviewOwnForm: false,
      allowReviewerToApproveOwnReview: false,
      allowApproverToPublish: true,
      defaultRetentionDays: 365,
      sensitiveRetentionDays: 730,
      minimumRetentionDays: 30,
      auditSensitiveViews: true,
      auditExports: true,
      requireReasonForArchive: true,
    },
  })

  useEffect(() => {
    if (!policyQuery.data) return
    setRowVersion(policyQuery.data.rowVersion)
    reset({
      requireReviewBeforeApproval: policyQuery.data.requireReviewBeforeApproval,
      requireSeparationOfDuties: policyQuery.data.requireSeparationOfDuties,
      allowDesignerToReviewOwnForm: policyQuery.data.allowDesignerToReviewOwnForm,
      allowReviewerToApproveOwnReview: policyQuery.data.allowReviewerToApproveOwnReview,
      allowApproverToPublish: policyQuery.data.allowApproverToPublish,
      defaultRetentionDays: policyQuery.data.defaultRetentionDays,
      sensitiveRetentionDays: policyQuery.data.sensitiveRetentionDays,
      minimumRetentionDays: policyQuery.data.minimumRetentionDays,
      auditSensitiveViews: policyQuery.data.auditSensitiveViews,
      auditExports: policyQuery.data.auditExports,
      requireReasonForArchive: policyQuery.data.requireReasonForArchive,
    })
  }, [policyQuery.data, reset])

  const mutation = useMutation({
    mutationFn: (values: UpdateFormGovernancePolicyFormValues) => {
      const body: UpdateFormGovernancePolicyRequest = { ...values, rowVersion }
      return api.formGovernance.updatePolicy(body)
    },
    onSuccess: (updated) => {
      setRowVersion(updated.rowVersion)
      setSuccessMessage('تم حفظ سياسة الحوكمة.')
      setServerError(null)
      setConflict(false)
    },
    onError: (err: unknown) => {
      setSuccessMessage(null)
      if (err instanceof ApiError) {
        if (err.status === 409) {
          setConflict(true)
          setServerError('تم تعديل السياسة من مستخدم آخر. أعد تحميل الصفحة.')
        } else if (err.status === 403) {
          setServerError('ليست لديك صلاحية إدارة حوكمة النماذج.')
        } else {
          setServerError(err.message)
        }
      } else {
        setServerError('تعذر حفظ السياسة.')
      }
    },
  })

  if (!canManage) {
    return <div className="error" role="alert">ليست لديك صلاحية إدارة حوكمة النماذج.</div>
  }

  if (policyQuery.isLoading) return <div className="loading">جاري التحميل…</div>

  if (policyQuery.isError) {
    const err = policyQuery.error as ApiError
    return (
      <div className="error" role="alert">
        <span>{err.status === 403 ? 'ليست لديك صلاحية عرض سياسة الحوكمة.' : err.message}</span>
        <button type="button" className="secondary" onClick={() => policyQuery.refetch()}>إعادة المحاولة</button>
      </div>
    )
  }

  return (
    <section className="panel" dir="rtl">
      <div className="page-header">
        <div>
          <h1>حوكمة النماذج</h1>
          <p>سياسات المراجعة والفصل بين المهام والاحتفاظ والتدقيق.</p>
        </div>
      </div>

      {successMessage && <div className="success">{successMessage}</div>}

      <form onSubmit={handleSubmit((values) => {
        if (mutation.isPending) return
        setSuccessMessage(null)
        mutation.mutate(values)
      })}>
        <div className="panel-section">
          <h2 className="section-title">المراجعة والفصل بين المهام</h2>
          <div className="form-grid">
            <label className="checkbox-field">
              <input type="checkbox" {...register('requireReviewBeforeApproval')} />
              <span>يتطلب مراجعة قبل الاعتماد</span>
            </label>
            <label className="checkbox-field">
              <input type="checkbox" {...register('requireSeparationOfDuties')} />
              <span>يتطلب فصل بين المهام</span>
            </label>
            <label className="checkbox-field">
              <input type="checkbox" {...register('allowDesignerToReviewOwnForm')} />
              <span>السماح للمصمم بمراجعة نموذجه</span>
            </label>
            <label className="checkbox-field">
              <input type="checkbox" {...register('allowReviewerToApproveOwnReview')} />
              <span>السماح للمراجع باعتماد مراجعته</span>
            </label>
            <label className="checkbox-field">
              <input type="checkbox" {...register('allowApproverToPublish')} />
              <span>السماح للمعتمد بالنشر</span>
            </label>
          </div>
        </div>

        <div className="panel-section">
          <h2 className="section-title">الاحتفاظ</h2>
          <div className="form-grid">
            <label className="field">
              <span>مدة الاحتفاظ الافتراضية (يوم)</span>
              <input aria-label="مدة الاحتفاظ الافتراضية" type="number" min={0} {...register('defaultRetentionDays', { valueAsNumber: true })} />
              {errors.defaultRetentionDays && <span className="field-error">{errors.defaultRetentionDays.message}</span>}
            </label>
            <label className="field">
              <span>مدة الاحتفاظ للحساس (يوم)</span>
              <input aria-label="مدة الاحتفاظ للحساس" type="number" min={0} {...register('sensitiveRetentionDays', { valueAsNumber: true })} />
              {errors.sensitiveRetentionDays && <span className="field-error">{errors.sensitiveRetentionDays.message}</span>}
            </label>
            <label className="field">
              <span>الحد الأدنى للاحتفاظ (يوم)</span>
              <input aria-label="الحد الأدنى للاحتفاظ" type="number" min={0} {...register('minimumRetentionDays', { valueAsNumber: true })} />
              {errors.minimumRetentionDays && <span className="field-error">{errors.minimumRetentionDays.message}</span>}
            </label>
          </div>
        </div>

        <div className="panel-section">
          <h2 className="section-title">التدقيق والأرشفة</h2>
          <div className="form-grid">
            <label className="checkbox-field">
              <input type="checkbox" {...register('auditSensitiveViews')} />
              <span>تدقيق العروض الحساسة</span>
            </label>
            <label className="checkbox-field">
              <input type="checkbox" {...register('auditExports')} />
              <span>تدقيق التصدير</span>
            </label>
            <label className="checkbox-field">
              <input type="checkbox" {...register('requireReasonForArchive')} />
              <span>يتطلب سببًا للأرشفة</span>
            </label>
          </div>
        </div>

        {serverError && (
          <div className="error" role="alert">
            <span>{serverError}</span>
            {conflict && (
              <button type="button" className="secondary" onClick={() => { setConflict(false); void policyQuery.refetch() }}>
                إعادة تحميل
              </button>
            )}
          </div>
        )}

        <div className="form-actions">
          <button type="submit" disabled={mutation.isPending || conflict}>
            {mutation.isPending ? 'جارٍ الحفظ…' : 'حفظ السياسة'}
          </button>
        </div>
      </form>
    </section>
  )
}
