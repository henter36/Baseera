import { useQuery } from '@tanstack/react-query'
import { useEffect } from 'react'
import { useFormContext } from 'react-hook-form'
import { api, type Me, type NoteType } from '../api/client'
import {
  ClassificationLevelLabelsAr,
  NoteSeverityLabelsAr,
  NoteSourceTypeLabelsAr,
  ScopeType,
  enumOptions,
} from './noteEnums'

type NoteFormValues = {
  title: string
  description: string
  noteTypeId: string
  severity: string
  sourceType: string
  sourceReference?: string
  classification: string
  ownerDepartmentId?: string
  dueAtUtc?: string
  scopeType?: string
  regionId?: string
  facilityId?: string
  facilityUnitId?: string
}

type NoteFormProps = {
  mode: 'create' | 'edit'
  me?: Me | null
}

function localDateAfterDays(days: number): string {
  const due = new Date()
  due.setDate(due.getDate() + days)
  return due.toISOString().slice(0, 16)
}

function selectedNoteType(types: NoteType[], id?: string): NoteType | undefined {
  return types.find((type) => type.id === id)
}

export function NoteForm({ mode }: Readonly<NoteFormProps>) {
  const {
    register,
    watch,
    setValue,
    formState: { errors },
  } = useFormContext<NoteFormValues>()

  const regionId = watch('regionId')
  const noteTypeId = watch('noteTypeId')

  const intakeQuery = useQuery({
    queryKey: ['note-intake-context'],
    queryFn: () => api.myNoteIntakeContext(),
    enabled: mode === 'create',
  })

  const noteTypesQuery = useQuery({
    queryKey: ['note-types-form', mode],
    queryFn: () => (mode === 'create' ? api.myNoteTypes() : api.noteTypes(true)),
  })

  const facilitiesQuery = useQuery({
    queryKey: ['note-form-facilities', regionId, !!intakeQuery.data],
    queryFn: () => intakeQuery.data ? api.myNoteIntakeFacilities(regionId!) : api.facilities(regionId || undefined).then((r) => r.items),
    enabled: mode === 'create' && !!regionId,
  })

  const departmentsQuery = useQuery({
    queryKey: ['note-form-departments'],
    queryFn: () => api.departments(),
  })

  useEffect(() => {
    if (mode !== 'create') return
    setValue('scopeType', String(ScopeType.Facility))
    if (intakeQuery.data?.lockedRegionId) {
      setValue('regionId', intakeQuery.data.lockedRegionId)
    }
    if (intakeQuery.data?.lockedFacilityId) {
      setValue('facilityId', intakeQuery.data.lockedFacilityId)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [intakeQuery.data, mode])

  useEffect(() => {
    if (mode !== 'create') return
    const type = selectedNoteType(noteTypesQuery.data ?? intakeQuery.data?.creatableNoteTypes ?? [], noteTypeId)
    if (!type) return
    setValue('severity', String(type.defaultSeverity))
    if (type.defaultDueDays !== null && type.defaultDueDays !== undefined) {
      setValue('dueAtUtc', localDateAfterDays(type.defaultDueDays))
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [noteTypeId])

  const regions = intakeQuery.data?.regions ?? []
  const noteTypes = mode === 'create' ? intakeQuery.data?.creatableNoteTypes ?? noteTypesQuery.data ?? [] : noteTypesQuery.data ?? []
  const facilities = facilitiesQuery.data ?? []
  const type = selectedNoteType(noteTypes, noteTypeId)
  const regionLocked = !!intakeQuery.data?.lockedRegionId
  const facilityLocked = !!intakeQuery.data?.lockedFacilityId

  return (
    <div className="form-grid">
      {mode === 'create' && (
        <>
          <input type="hidden" value={ScopeType.Facility} {...register('scopeType')} />
          <label className="field">
            <span>المنطقة *</span>
            <select aria-label="المنطقة" {...register('regionId')} disabled={regionLocked}>
              <option value="">اختر منطقة</option>
              {regions.map((region) => (
                <option key={region.id} value={region.id}>{region.nameAr}</option>
              ))}
            </select>
            {errors.regionId && <span className="field-error">{errors.regionId.message as string}</span>}
          </label>

          <label className="field">
            <span>السجن *</span>
            <select aria-label="السجن" {...register('facilityId')} disabled={facilityLocked || !regionId}>
              <option value="">اختر سجنًا</option>
              {facilities.map((facility) => (
                <option key={facility.id} value={facility.id}>{facility.nameAr}</option>
              ))}
            </select>
            {errors.facilityId && <span className="field-error">{errors.facilityId.message as string}</span>}
          </label>
        </>
      )}

      <label className="field field-wide">
        <span>نوع الملاحظة *</span>
        <select aria-label="نوع الملاحظة" {...register('noteTypeId')}>
          <option value="">اختر نوع الملاحظة</option>
          {noteTypes.map((item) => (
            <option key={item.id} value={item.id}>{item.nameAr}{!item.isActive ? ' - غير فعال للإنشاء' : ''}</option>
          ))}
        </select>
        {errors.noteTypeId && <span className="field-error">{errors.noteTypeId.message as string}</span>}
        {type?.descriptionAr && <span className="muted">{type.descriptionAr}</span>}
        {type?.entryInstructionsAr && <span className="muted">{type.entryInstructionsAr}</span>}
      </label>

      <label className="field">
        <span>العنوان *</span>
        <input aria-label="العنوان" {...register('title')} />
        {errors.title && <span className="field-error">{errors.title.message as string}</span>}
      </label>

      <label className="field field-wide">
        <span>الوصف *</span>
        <textarea aria-label="الوصف" rows={4} {...register('description')} />
        {errors.description && <span className="field-error">{errors.description.message as string}</span>}
      </label>

      <label className="field">
        <span>مستوى الخطورة *</span>
        <select aria-label="مستوى الخطورة" {...register('severity')}>
          {enumOptions(NoteSeverityLabelsAr).map((option) => (
            <option key={option.value} value={option.value}>{option.labelAr}</option>
          ))}
        </select>
      </label>

      <label className="field">
        <span>تاريخ الاستحقاق</span>
        <input aria-label="تاريخ الاستحقاق" type="datetime-local" {...register('dueAtUtc')} />
        {errors.dueAtUtc && <span className="field-error">{errors.dueAtUtc.message as string}</span>}
      </label>

      <label className="field">
        <span>نوع المصدر *</span>
        <select aria-label="نوع المصدر" {...register('sourceType')}>
          {enumOptions(NoteSourceTypeLabelsAr).map((option) => (
            <option key={option.value} value={option.value}>{option.labelAr}</option>
          ))}
        </select>
      </label>

      <label className="field">
        <span>مرجع المصدر</span>
        <input aria-label="مرجع المصدر" {...register('sourceReference')} />
        {errors.sourceReference && <span className="field-error">{errors.sourceReference.message as string}</span>}
      </label>

      <label className="field">
        <span>مستوى التصنيف الأمني *</span>
        <select aria-label="مستوى التصنيف الأمني" {...register('classification')}>
          {enumOptions(ClassificationLevelLabelsAr).map((option) => (
            <option key={option.value} value={option.value}>{option.labelAr}</option>
          ))}
        </select>
      </label>

      <label className="field">
        <span>الإدارة المسؤولة</span>
        <select aria-label="الإدارة المسؤولة" {...register('ownerDepartmentId')}>
          <option value="">بدون تحديد إدارة</option>
          {departmentsQuery.data?.items.map((department) => (
            <option key={department.id} value={department.id}>{department.nameAr}</option>
          ))}
        </select>
        {errors.ownerDepartmentId && <span className="field-error">{errors.ownerDepartmentId.message as string}</span>}
      </label>
    </div>
  )
}
