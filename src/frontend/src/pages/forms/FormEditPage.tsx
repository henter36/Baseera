import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { FormProvider, useForm } from 'react-hook-form'
import { useNavigate, useParams } from 'react-router-dom'
import { api, ApiError, type UpdateFormRequest } from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import { FormForm } from '../../forms/FormForm'
import { FormDefinitionStatus } from '../../forms/formEnums'
import { type UpdateFormFormValues, updateFormSchema } from '../../forms/formSchema'

export function FormEditPage() {
  const canUpdate = usePermission('Forms.UpdateDraft')
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [serverError, setServerError] = useState<string | null>(null)
  const [conflict, setConflict] = useState(false)

  const formQuery = useQuery({
    queryKey: ['form', id],
    queryFn: () => api.forms.get(id!),
    enabled: canUpdate && !!id,
  })

  const methods = useForm<UpdateFormFormValues>({
    resolver: zodResolver(updateFormSchema),
    defaultValues: {
      nameAr: '',
      nameEn: '',
      description: '',
      classification: '0',
      ownerDepartmentId: '',
    },
  })

  useEffect(() => {
    if (!formQuery.data) return
    methods.reset({
      nameAr: formQuery.data.nameAr,
      nameEn: formQuery.data.nameEn || '',
      description: formQuery.data.description,
      classification: String(formQuery.data.classification),
      ownerDepartmentId: formQuery.data.ownerDepartmentId || '',
    })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [formQuery.data])

  const mutation = useMutation({
    mutationFn: (values: UpdateFormFormValues) => {
      if (!formQuery.data) throw new Error('النموذج غير محمّل.')
      const body: UpdateFormRequest = {
        nameAr: values.nameAr,
        nameEn: values.nameEn || null,
        description: values.description,
        classification: Number(values.classification),
        ownerDepartmentId: values.ownerDepartmentId || null,
        rowVersion: formQuery.data.rowVersion,
      }
      return api.forms.update(id!, body)
    },
    onSuccess: (updated) => navigate(`/forms/${updated.id}`),
    onError: (err: unknown) => {
      if (err instanceof ApiError) {
        if (err.status === 409) {
          setConflict(true)
          setServerError('تم تعديل النموذج من مستخدم آخر. أعد تحميل الصفحة قبل المحاولة مرة أخرى.')
        } else if (err.status === 403) {
          setServerError('ليست لديك صلاحية تعديل هذا النموذج.')
        } else if (err.status === 404) {
          setServerError('النموذج غير موجود أو خارج نطاقك.')
        } else {
          setServerError(err.message)
        }
      } else {
        setServerError('تعذر حفظ التعديلات.')
      }
    },
  })

  if (!canUpdate) {
    return <div className="error" role="alert">ليست لديك صلاحية تعديل النماذج.</div>
  }

  if (formQuery.isLoading) return <div className="loading">جاري التحميل…</div>

  if (formQuery.isError) {
    const err = formQuery.error as ApiError
    const message = err.status === 404 ? 'النموذج غير موجود أو خارج نطاقك.' : err.message || 'تعذر تحميل النموذج.'
    return (
      <div className="error" role="alert">
        <span>{message}</span>
        <button type="button" className="secondary" onClick={() => formQuery.refetch()}>إعادة المحاولة</button>
      </div>
    )
  }

  if (!formQuery.data) return <div className="empty">النموذج غير موجود.</div>

  const editable = formQuery.data.status === FormDefinitionStatus.Draft
    || formQuery.data.status === FormDefinitionStatus.ChangesRequested

  if (!editable) {
    return <div className="error" role="alert">لا يمكن تعديل نموذج في هذه الحالة إلا عبر انتقال صريح.</div>
  }

  return (
    <div className="panel" dir="rtl">
      <h1 className="page-title">تعديل النموذج {formQuery.data.code}</h1>
      <p className="muted">لا يمكن تغيير الرمز أو النطاق من هذه الصفحة.</p>

      <FormProvider {...methods}>
        <form
          onSubmit={methods.handleSubmit((values) => {
            if (mutation.isPending) return
            setServerError(null)
            setConflict(false)
            mutation.mutate(values)
          })}
        >
          <FormForm mode="edit" />

          {serverError && (
            <div className="error" role="alert">
              <span>{serverError}</span>
              {conflict && (
                <button
                  type="button"
                  className="secondary"
                  onClick={() => { setConflict(false); setServerError(null); void formQuery.refetch() }}
                >
                  إعادة تحميل
                </button>
              )}
            </div>
          )}

          <div className="form-actions">
            <button type="submit" disabled={mutation.isPending || conflict}>
              {mutation.isPending ? 'جارٍ الحفظ…' : 'حفظ التعديلات'}
            </button>
          </div>
        </form>
      </FormProvider>
    </div>
  )
}
