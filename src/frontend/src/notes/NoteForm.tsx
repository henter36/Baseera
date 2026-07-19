import { useQuery } from '@tanstack/react-query'
import { useEffect } from 'react'
import { useFormContext } from 'react-hook-form'
import { api, type Me } from '../api/client'
import {
  ClassificationLevelLabelsAr,
  NoteCategoryLabelsAr,
  NoteSeverityLabelsAr,
  NoteSourceTypeLabelsAr,
  ScopeType,
  ScopeTypeLabelsAr,
  enumOptions,
} from './noteEnums'
import { allowedScopeTypes } from './noteScopeOptions'

type NoteFormValues = {
  title: string
  description: string
  category: string
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

export function NoteForm({ mode, me }: Readonly<NoteFormProps>) {
  const {
    register,
    watch,
    setValue,
    formState: { errors },
  } = useFormContext<NoteFormValues>()

  const scopeType = mode === 'create' ? watch('scopeType') : undefined
  const scopeTypeNum = scopeType !== undefined && scopeType !== '' ? Number(scopeType) : undefined
  const regionId = watch('regionId')

  const regionsQuery = useQuery({
    queryKey: ['note-form-regions'],
    queryFn: () => api.regions(),
    enabled:
      mode === 'create' &&
      (scopeTypeNum === ScopeType.Region || scopeTypeNum === ScopeType.Facility || scopeTypeNum === ScopeType.FacilityUnit),
  })

  const facilitiesQuery = useQuery({
    queryKey: ['note-form-facilities', regionId],
    queryFn: () => api.facilities(regionId || undefined),
    enabled: mode === 'create' && (scopeTypeNum === ScopeType.Facility || scopeTypeNum === ScopeType.FacilityUnit),
  })

  const facilityId = watch('facilityId')

  const facilityUnitsQuery = useQuery({
    queryKey: ['note-form-facility-units', facilityId],
    queryFn: () => api.facilityUnits(facilityId!),
    enabled: mode === 'create' && scopeTypeNum === ScopeType.FacilityUnit && !!facilityId,
  })

  const departmentsQuery = useQuery({
    queryKey: ['note-form-departments'],
    queryFn: () => api.departments(),
  })

  useEffect(() => {
    if (mode !== 'create') return
    if (scopeTypeNum !== ScopeType.Region) setValue('regionId', '')
    if (scopeTypeNum !== ScopeType.Facility && scopeTypeNum !== ScopeType.FacilityUnit) {
      setValue('facilityId', '')
    }
    if (scopeTypeNum !== ScopeType.FacilityUnit) setValue('facilityUnitId', '')
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [scopeTypeNum])

  const scopeOptions = mode === 'create' ? allowedScopeTypes(me ?? null) : []

  return (
    <div className="form-grid">
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
        <span>التصنيف *</span>
        <select aria-label="التصنيف" {...register('category')}>
          {enumOptions(NoteCategoryLabelsAr).map((o) => (
            <option key={o.value} value={o.value}>
              {o.labelAr}
            </option>
          ))}
        </select>
      </label>

      <label className="field">
        <span>مستوى الخطورة *</span>
        <select aria-label="مستوى الخطورة" {...register('severity')}>
          {enumOptions(NoteSeverityLabelsAr).map((o) => (
            <option key={o.value} value={o.value}>
              {o.labelAr}
            </option>
          ))}
        </select>
      </label>

      <label className="field">
        <span>نوع المصدر *</span>
        <select aria-label="نوع المصدر" {...register('sourceType')}>
          {enumOptions(NoteSourceTypeLabelsAr).map((o) => (
            <option key={o.value} value={o.value}>
              {o.labelAr}
            </option>
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
          {enumOptions(ClassificationLevelLabelsAr).map((o) => (
            <option key={o.value} value={o.value}>
              {o.labelAr}
            </option>
          ))}
        </select>
      </label>

      <label className="field">
        <span>الإدارة المسؤولة</span>
        <select aria-label="الإدارة المسؤولة" {...register('ownerDepartmentId')}>
          <option value="">بدون تحديد إدارة</option>
          {departmentsQuery.data?.items.map((d) => (
            <option key={d.id} value={d.id}>
              {d.nameAr}
            </option>
          ))}
        </select>
        {errors.ownerDepartmentId && <span className="field-error">{errors.ownerDepartmentId.message as string}</span>}
      </label>

      <label className="field">
        <span>تاريخ الاستحقاق</span>
        <input aria-label="تاريخ الاستحقاق" type="datetime-local" {...register('dueAtUtc')} />
        {errors.dueAtUtc && <span className="field-error">{errors.dueAtUtc.message as string}</span>}
      </label>

      {mode === 'create' && (
        <>
          <label className="field">
            <span>نطاق الملاحظة *</span>
            <select aria-label="نطاق الملاحظة" {...register('scopeType')}>
              <option value="-1">اختر نطاقًا</option>
              {scopeOptions.map((value) => (
                <option key={value} value={value}>
                  {ScopeTypeLabelsAr[value]}
                </option>
              ))}
            </select>
            {errors.scopeType && <span className="field-error">{errors.scopeType.message as string}</span>}
          </label>

          {(scopeTypeNum === ScopeType.Region || scopeTypeNum === ScopeType.Facility || scopeTypeNum === ScopeType.FacilityUnit) && (
            <label className="field">
              <span>المنطقة {scopeTypeNum === ScopeType.Region ? '*' : ''}</span>
              <select aria-label="المنطقة" {...register('regionId')}>
                <option value="">اختر منطقة</option>
                {regionsQuery.data?.items.map((r) => (
                  <option key={r.id} value={r.id}>
                    {r.nameAr}
                  </option>
                ))}
              </select>
              {errors.regionId && <span className="field-error">{errors.regionId.message as string}</span>}
            </label>
          )}

          {(scopeTypeNum === ScopeType.Facility || scopeTypeNum === ScopeType.FacilityUnit) && (
            <label className="field">
              <span>السجن *</span>
              <select aria-label="السجن" {...register('facilityId')}>
                <option value="">اختر سجنًا</option>
                {facilitiesQuery.data?.items.map((f) => (
                  <option key={f.id} value={f.id}>
                    {f.nameAr}
                  </option>
                ))}
              </select>
              {errors.facilityId && <span className="field-error">{errors.facilityId.message as string}</span>}
            </label>
          )}

          {scopeTypeNum === ScopeType.FacilityUnit && (
            <label className="field">
              <span>الوحدة *</span>
              <select aria-label="الوحدة" {...register('facilityUnitId')} disabled={!facilityId}>
                <option value="">{facilityId ? 'اختر وحدة' : 'اختر السجن أولًا'}</option>
                {facilityUnitsQuery.data?.items.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.nameAr}
                  </option>
                ))}
              </select>
              {errors.facilityUnitId && <span className="field-error">{errors.facilityUnitId.message as string}</span>}
            </label>
          )}
        </>
      )}
    </div>
  )
}
