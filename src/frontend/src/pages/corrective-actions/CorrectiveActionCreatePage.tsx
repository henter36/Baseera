import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { FormProvider, useForm } from 'react-hook-form'
import { useNavigate, useParams } from 'react-router-dom'
import { api, ApiError, type CreateCorrectiveActionRequest } from '../../api/client'
import { CorrectiveActionForm } from '../../correctiveActions/CorrectiveActionForm'
import { type CorrectiveActionFormValues, correctiveActionFormSchema } from '../../correctiveActions/correctiveActionSchema'
import { usePermission } from '../../auth/AuthProvider'

const DEFAULT_VALUES: CorrectiveActionFormValues = {
  title: '',
  description: '',
  priority: '1',
  classification: '0',
  ownerDepartmentId: '',
  dueAtUtc: '',
}

function toIsoOrUndefined(value?: string): string | undefined {
  if (!value) return undefined
  const d = new Date(value)
  return Number.isNaN(d.getTime()) ? undefined : d.toISOString()
}

export function CorrectiveActionCreatePage() {
  const canCreate = usePermission('CorrectiveActions.Create')
  const { noteId } = useParams<{ noteId: string }>()
  const navigate = useNavigate()
  const [serverError, setServerError] = useState<string | null>(null)

  const noteQuery = useQuery({
    queryKey: ['note', noteId],
    queryFn: () => api.notes.get(noteId!),
    enabled: canCreate && !!noteId,
  })

  const methods = useForm<CorrectiveActionFormValues>({
    resolver: zodResolver(correctiveActionFormSchema),
    defaultValues: DEFAULT_VALUES,
  })

  const mutation = useMutation({
    mutationFn: (values: CorrectiveActionFormValues) => {
      const body: CreateCorrectiveActionRequest = {
        title: values.title,
        description: values.description,
        priority: Number(values.priority),
        classification: Number(values.classification),
        ownerDepartmentId: values.ownerDepartmentId || null,
        dueAtUtc: toIsoOrUndefined(values.dueAtUtc) ?? null,
      }
      return api.notes.createCorrectiveAction(noteId!, body)
    },
    onSuccess: (created) => navigate(`/corrective-actions/${created.id}`),
    onError: (err: unknown) => {
      if (err instanceof ApiError) {
        if (err.status === 403) setServerError('ليست لديك صلاحية إنشاء إجراء تصحيحي لهذه الملاحظة.')
        else if (err.status === 404) setServerError('الملاحظة غير موجودة أو خارج نطاقك.')
        else setServerError(err.message)
      } else {
        setServerError('تعذر إنشاء الإجراء التصحيحي.')
      }
    },
  })

  if (!canCreate) return <div className="error" role="alert">ليست لديك صلاحية إنشاء إجراء تصحيحي.</div>
  if (noteQuery.isLoading) return <div className="loading">جاري تحميل الملاحظة…</div>
  if (noteQuery.isError) return <div className="error" role="alert">الملاحظة غير موجودة أو خارج نطاقك.</div>

  return (
    <div className="panel">
      <h1 className="page-title">إجراء تصحيحي جديد</h1>
      <p className="muted">النطاق والتصنيف الأدنى مشتقان من الملاحظة الأصلية ولا يمكن تغيير النطاق من النموذج.</p>
      <FormProvider {...methods}>
        <form onSubmit={methods.handleSubmit((values) => {
          if (mutation.isPending) return
          setServerError(null)
          mutation.mutate(values)
        })}>
          <CorrectiveActionForm noteTitle={noteQuery.data?.title} />
          {serverError && <div className="error" role="alert">{serverError}</div>}
          <div className="form-actions">
            <button type="submit" disabled={mutation.isPending}>{mutation.isPending ? 'جارٍ الحفظ…' : 'حفظ المسودة'}</button>
          </div>
        </form>
      </FormProvider>
    </div>
  )
}
