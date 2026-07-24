import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { FormProvider, useForm } from 'react-hook-form'
import { useNavigate, useParams } from 'react-router'
import { api, ApiError, type UpdateCorrectiveActionRequest } from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import { CorrectiveActionForm } from '../../correctiveActions/CorrectiveActionForm'
import { type CorrectiveActionFormValues, correctiveActionFormSchema } from '../../correctiveActions/correctiveActionSchema'

function toLocalInput(value?: string | null): string {
  if (!value) return ''
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return ''
  return new Date(d.getTime() - d.getTimezoneOffset() * 60_000).toISOString().slice(0, 16)
}

function toIsoOrUndefined(value?: string): string | undefined {
  if (!value) return undefined
  const d = new Date(value)
  return Number.isNaN(d.getTime()) ? undefined : d.toISOString()
}

export function CorrectiveActionEditPage() {
  const canUpdate = usePermission('CorrectiveActions.Update')
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [serverError, setServerError] = useState<string | null>(null)

  const query = useQuery({
    queryKey: ['corrective-action', id],
    queryFn: () => api.correctiveActions.get(id!),
    enabled: canUpdate && !!id,
  })

  const methods = useForm<CorrectiveActionFormValues>({
    resolver: zodResolver(correctiveActionFormSchema),
    defaultValues: {
      title: '',
      description: '',
      priority: '1',
      classification: '0',
      ownerDepartmentId: '',
      dueAtUtc: '',
    },
  })

  useEffect(() => {
    if (!query.data) return
    methods.reset({
      title: query.data.title,
      description: query.data.description,
      priority: String(query.data.priority),
      classification: String(query.data.classification),
      ownerDepartmentId: query.data.ownerDepartmentId ?? '',
      dueAtUtc: toLocalInput(query.data.dueAtUtc),
    })
  }, [methods, query.data])

  const mutation = useMutation({
    mutationFn: (values: CorrectiveActionFormValues) => {
      const current = query.data!
      const body: UpdateCorrectiveActionRequest = {
        title: values.title,
        description: values.description,
        priority: Number(values.priority),
        classification: Number(values.classification),
        ownerDepartmentId: values.ownerDepartmentId || null,
        dueAtUtc: toIsoOrUndefined(values.dueAtUtc) ?? null,
        rowVersion: current.rowVersion,
      }
      return api.correctiveActions.update(current.id, body)
    },
    onSuccess: (updated) => navigate(`/corrective-actions/${updated.id}`),
    onError: (err: unknown) => {
      if (err instanceof ApiError) {
        if (err.status === 409) setServerError('تم تغيير الإجراء بواسطة مستخدم آخر. أعد تحميل الصفحة قبل الحفظ.')
        else if (err.status === 404) setServerError('الإجراء غير موجود أو خارج نطاقك.')
        else if (err.status === 403) setServerError('ليست لديك صلاحية تعديل هذا الإجراء.')
        else setServerError(err.message)
      } else {
        setServerError('تعذر تعديل الإجراء التصحيحي.')
      }
    },
  })

  if (!canUpdate) return <div className="error" role="alert">ليست لديك صلاحية تعديل الإجراءات التصحيحية.</div>
  if (query.isLoading) return <div className="loading">جاري التحميل…</div>
  if (query.isError) return <div className="error" role="alert">الإجراء غير موجود أو خارج نطاقك.</div>
  if (!query.data) return <div className="empty">الإجراء غير موجود.</div>

  return (
    <div className="panel">
      <h1 className="page-title">تعديل إجراء تصحيحي</h1>
      <p className="muted">{query.data.referenceNumber} — لا يمكن تغيير الحالة أو الملاحظة الأصلية من هذا النموذج.</p>
      <FormProvider {...methods}>
        <form onSubmit={methods.handleSubmit((values) => {
          if (mutation.isPending) return
          setServerError(null)
          mutation.mutate(values)
        })}>
          <CorrectiveActionForm noteTitle={query.data.operationalNoteReferenceNumber ?? query.data.operationalNoteId} />
          {serverError && <div className="error" role="alert">{serverError}</div>}
          <div className="form-actions">
            <button type="submit" disabled={mutation.isPending}>{mutation.isPending ? 'جارٍ الحفظ…' : 'حفظ التعديل'}</button>
          </div>
        </form>
      </FormProvider>
    </div>
  )
}
