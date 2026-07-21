import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { useState } from 'react'
import { FormProvider, useForm } from 'react-hook-form'
import { useNavigate } from 'react-router-dom'
import { api, ApiError, type CreateFormRequest } from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import { FormForm } from '../../forms/FormForm'
import { ScopeType } from '../../forms/formEnums'
import { type CreateFormFormValues, createFormSchema } from '../../forms/formSchema'

const DEFAULT_VALUES: CreateFormFormValues = {
  code: '',
  nameAr: '',
  nameEn: '',
  description: '',
  classification: '0',
  scopeType: String(ScopeType.Facility),
  regionId: '',
  facilityId: '',
  facilityUnitId: '',
  ownerDepartmentId: '',
}

export function FormCreatePage() {
  const canCreate = usePermission('Forms.Create')
  const navigate = useNavigate()
  const [serverError, setServerError] = useState<string | null>(null)

  const methods = useForm<CreateFormFormValues>({
    resolver: zodResolver(createFormSchema),
    defaultValues: DEFAULT_VALUES,
  })

  const mutation = useMutation({
    mutationFn: (values: CreateFormFormValues) => {
      const scope = Number(values.scopeType)
      const body: CreateFormRequest = {
        code: values.code.trim(),
        nameAr: values.nameAr,
        nameEn: values.nameEn || null,
        description: values.description,
        classification: Number(values.classification),
        scopeType: scope,
        regionId: scope === ScopeType.Global || scope === ScopeType.Headquarters ? null : values.regionId || null,
        facilityId: scope === ScopeType.Facility || scope === ScopeType.FacilityUnit ? values.facilityId || null : null,
        facilityUnitId: scope === ScopeType.FacilityUnit ? values.facilityUnitId || null : null,
        ownerDepartmentId: values.ownerDepartmentId || null,
      }
      return api.forms.create(body)
    },
    onSuccess: (created) => navigate(`/forms/${created.id}`),
    onError: (err: unknown) => {
      if (err instanceof ApiError) {
        if (err.status === 403) setServerError('ليست لديك صلاحية إنشاء نموذج.')
        else setServerError(err.message)
      } else {
        setServerError('تعذر إنشاء النموذج.')
      }
    },
  })

  if (!canCreate) {
    return <div className="error" role="alert">ليست لديك صلاحية إنشاء نموذج.</div>
  }

  return (
    <div className="panel" dir="rtl">
      <h1 className="page-title">نموذج جديد</h1>
      <p className="muted">الحالة الابتدائية دائمًا «مسودة»؛ أرسل للمراجعة بعد اكتمال التصميم.</p>

      <FormProvider {...methods}>
        <form
          onSubmit={methods.handleSubmit((values) => {
            if (mutation.isPending) return
            setServerError(null)
            mutation.mutate(values)
          })}
        >
          <FormForm mode="create" />

          {serverError && <div className="error" role="alert">{serverError}</div>}

          <div className="form-actions">
            <button type="submit" disabled={mutation.isPending}>
              {mutation.isPending ? 'جارٍ الحفظ…' : 'حفظ المسودة'}
            </button>
          </div>
        </form>
      </FormProvider>
    </div>
  )
}
