import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { FormProvider, useForm } from 'react-hook-form'
import { useNavigate, useParams } from 'react-router-dom'
import { api, ApiError, type UpdateNoteRequest } from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import { NoteForm } from '../../notes/NoteForm'
import { type UpdateNoteFormValues, updateNoteSchema } from '../../notes/noteSchema'

function toIsoOrUndefined(value?: string): string | undefined {
  if (!value) return undefined
  const d = new Date(value)
  return Number.isNaN(d.getTime()) ? undefined : d.toISOString()
}

function toLocalInput(value?: string | null): string {
  if (!value) return ''
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return ''
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

export function NoteEditPage() {
  const canUpdate = usePermission('Notes.Update')
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [serverError, setServerError] = useState<string | null>(null)
  const [conflict, setConflict] = useState(false)

  const noteQuery = useQuery({
    queryKey: ['note', id],
    queryFn: () => api.notes.get(id!),
    enabled: canUpdate && !!id,
  })

  const methods = useForm<UpdateNoteFormValues>({
    resolver: zodResolver(updateNoteSchema),
    defaultValues: {
      title: '',
      description: '',
      category: '0',
      severity: '0',
      sourceType: '0',
      sourceReference: '',
      classification: '0',
      ownerDepartmentId: '',
      dueAtUtc: '',
    },
  })

  useEffect(() => {
    if (!noteQuery.data) return
    methods.reset({
      title: noteQuery.data.title,
      description: noteQuery.data.description,
      category: String(noteQuery.data.category),
      severity: String(noteQuery.data.severity),
      sourceType: String(noteQuery.data.sourceType),
      sourceReference: noteQuery.data.sourceReference || '',
      classification: String(noteQuery.data.classification),
      ownerDepartmentId: noteQuery.data.ownerDepartmentId || '',
      dueAtUtc: toLocalInput(noteQuery.data.dueAtUtc),
    })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [noteQuery.data])

  const mutation = useMutation({
    mutationFn: (values: UpdateNoteFormValues) => {
      if (!noteQuery.data) throw new Error('الملاحظة غير محمّلة.')
      const body: UpdateNoteRequest = {
        title: values.title,
        description: values.description,
        category: Number(values.category),
        severity: Number(values.severity),
        sourceType: Number(values.sourceType),
        sourceReference: values.sourceReference || null,
        classification: Number(values.classification),
        ownerDepartmentId: values.ownerDepartmentId || null,
        dueAtUtc: toIsoOrUndefined(values.dueAtUtc) ?? null,
        rowVersion: noteQuery.data.rowVersion,
      }
      return api.notes.update(id!, body)
    },
    onSuccess: (updated) => {
      navigate(`/notes/${updated.id}`)
    },
    onError: (err: unknown) => {
      if (err instanceof ApiError) {
        if (err.status === 409) {
          setConflict(true)
          setServerError('تم تعديل الملاحظة من مستخدم آخر. أعد تحميل الصفحة قبل المحاولة مرة أخرى.')
        } else if (err.status === 403) {
          setServerError('ليست لديك صلاحية تعديل هذه الملاحظة.')
        } else if (err.status === 404) {
          setServerError('الملاحظة غير موجودة أو خارج نطاقك.')
        } else {
          setServerError(err.message)
        }
      } else {
        setServerError('تعذر حفظ التعديلات.')
      }
    },
  })

  if (!canUpdate) {
    return <div className="error" role="alert">ليست لديك صلاحية تعديل الملاحظات.</div>
  }

  if (noteQuery.isLoading) {
    return <div className="loading">جاري التحميل…</div>
  }

  if (noteQuery.isError) {
    const err = noteQuery.error as ApiError
    const message = err.status === 404 ? 'الملاحظة غير موجودة أو خارج نطاقك.' : err.message || 'تعذر تحميل الملاحظة.'
    return (
      <div className="error" role="alert">
        <span>{message}</span>
        <button type="button" className="secondary" onClick={() => noteQuery.refetch()}>إعادة المحاولة</button>
      </div>
    )
  }

  if (!noteQuery.data) {
    return <div className="empty">الملاحظة غير موجودة.</div>
  }

  if (noteQuery.data.status === 5 || noteQuery.data.status === 7) {
    return <div className="error" role="alert">لا يمكن تعديل ملاحظة مغلقة أو ملغاة إلا عبر انتقال صريح.</div>
  }

  return (
    <div className="panel">
      <h1 className="page-title">تعديل الملاحظة {noteQuery.data.referenceNumber}</h1>
      <p className="muted">لا يمكن تغيير الحالة أو النطاق من هذه الصفحة.</p>

      <FormProvider {...methods}>
        <form
          onSubmit={methods.handleSubmit((values) => {
            if (mutation.isPending) return
            setServerError(null)
            setConflict(false)
            mutation.mutate(values)
          })}
        >
          <NoteForm mode="edit" />

          {serverError && (
            <div className="error" role="alert">
              <span>{serverError}</span>
              {conflict && (
                <button type="button" className="secondary" onClick={() => noteQuery.refetch()}>
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
