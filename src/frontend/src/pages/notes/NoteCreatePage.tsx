import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { useState } from 'react'
import { FormProvider, useForm } from 'react-hook-form'
import { useNavigate } from 'react-router-dom'
import { api, ApiError, type CreateNoteRequest } from '../../api/client'
import { useAuth, usePermission } from '../../auth/AuthProvider'
import { NoteForm } from '../../notes/NoteForm'
import { type CreateNoteFormValues, createNoteSchema } from '../../notes/noteSchema'

const DEFAULT_VALUES: CreateNoteFormValues = {
  title: '',
  description: '',
  category: '0',
  severity: '0',
  sourceType: '0',
  sourceReference: '',
  classification: '0',
  ownerDepartmentId: '',
  dueAtUtc: '',
  scopeType: '-1',
  regionId: '',
  facilityId: '',
  facilityUnitId: '',
}

function toIsoOrUndefined(value?: string): string | undefined {
  if (!value) return undefined
  const d = new Date(value)
  return Number.isNaN(d.getTime()) ? undefined : d.toISOString()
}

export function NoteCreatePage() {
  const canCreate = usePermission('Notes.Create')
  const { me } = useAuth()
  const navigate = useNavigate()
  const [serverError, setServerError] = useState<string | null>(null)

  const methods = useForm<CreateNoteFormValues>({
    resolver: zodResolver(createNoteSchema),
    defaultValues: DEFAULT_VALUES,
  })

  const mutation = useMutation({
    mutationFn: (values: CreateNoteFormValues) => {
      const body: CreateNoteRequest = {
        title: values.title,
        description: values.description,
        category: Number(values.category),
        severity: Number(values.severity),
        sourceType: Number(values.sourceType),
        sourceReference: values.sourceReference || null,
        classification: Number(values.classification),
        scopeType: Number(values.scopeType),
        regionId: values.regionId || null,
        facilityId: values.facilityId || null,
        facilityUnitId: values.facilityUnitId || null,
        ownerDepartmentId: values.ownerDepartmentId || null,
        dueAtUtc: toIsoOrUndefined(values.dueAtUtc) ?? null,
      }
      return api.notes.create(body)
    },
    onSuccess: (created) => {
      navigate(`/notes/${created.id}`)
    },
    onError: (err: unknown) => {
      if (err instanceof ApiError) {
        if (err.status === 403) setServerError('ليست لديك صلاحية إنشاء ملاحظة.')
        else setServerError(err.message)
      } else {
        setServerError('تعذر إنشاء الملاحظة.')
      }
    },
  })

  if (!canCreate) {
    return <div className="error" role="alert">ليست لديك صلاحية إنشاء ملاحظة.</div>
  }

  return (
    <div className="panel">
      <h1 className="page-title">ملاحظة جديدة</h1>
      <p className="muted">الحالة الابتدائية دائمًا "مسودة"؛ استخدم زر الإرسال بعد الحفظ لفتحها رسميًا.</p>

      <FormProvider {...methods}>
        <form
          onSubmit={methods.handleSubmit((values) => {
            if (mutation.isPending) return
            setServerError(null)
            mutation.mutate(values)
          })}
        >
          <NoteForm mode="create" me={me} />

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
