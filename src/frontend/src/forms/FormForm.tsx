import { useQuery } from '@tanstack/react-query'
import { useFormContext } from 'react-hook-form'
import { api } from '../api/client'
import {
  ClassificationLevelLabelsAr,
  ScopeType,
  ScopeTypeLabelsAr,
  enumOptions,
} from './formEnums'

const FORM_CREATE_SCOPE_TYPES: number[] = [
  ScopeType.Global,
  ScopeType.Headquarters,
  ScopeType.Region,
  ScopeType.Facility,
  ScopeType.FacilityUnit,
]

type FormFormValues = {
  code?: string
  nameAr: string
  nameEn?: string
  description: string
  classification: string
  scopeType?: string
  regionId?: string
  facilityId?: string
  facilityUnitId?: string
  ownerDepartmentId?: string
}

type FormFormProps = {
  mode: 'create' | 'edit'
}

export function FormForm({ mode }: Readonly<FormFormProps>) {
  const {
    register,
    watch,
    formState: { errors },
  } = useFormContext<FormFormValues>()

  const scopeType = watch('scopeType')
  const regionId = watch('regionId')
  const facilityId = watch('facilityId')

  const regionsQuery = useQuery({
    queryKey: ['form-regions'],
    queryFn: () => api.regions(),
    enabled: mode === 'create',
  })

  const facilitiesQuery = useQuery({
    queryKey: ['form-facilities', regionId],
    queryFn: () => api.facilities(regionId || undefined),
    enabled: mode === 'create' && !!regionId,
  })

  const facilityUnitsQuery = useQuery({
    queryKey: ['form-facility-units', facilityId],
    queryFn: () => api.facilityUnits(facilityId!),
    enabled: mode === 'create' && !!facilityId && Number(scopeType) === ScopeType.FacilityUnit,
  })

  const departmentsQuery = useQuery({
    queryKey: ['form-departments'],
    queryFn: () => api.departments(),
  })

  const scopeNum = Number(scopeType)
  const showRegion = mode === 'create' && (scopeNum === ScopeType.Region || scopeNum === ScopeType.Facility || scopeNum === ScopeType.FacilityUnit)
  const showFacility = mode === 'create' && (scopeNum === ScopeType.Facility || scopeNum === ScopeType.FacilityUnit)
  const showUnit = mode === 'create' && scopeNum === ScopeType.FacilityUnit

  return (
    <div className="form-grid">
      {mode === 'create' && (
        <>
          <label className="field">
            <span>رمز النموذج *</span>
            <input aria-label="رمز النموذج" {...register('code')} placeholder="مثال: INCIDENT.REPORT" />
            {errors.code && <span className="field-error">{errors.code.message}</span>}
          </label>
          <label className="field">
            <span>نوع النطاق *</span>
            <select aria-label="نوع النطاق" {...register('scopeType')}>
              {enumOptions(ScopeTypeLabelsAr)
                .filter((o) => FORM_CREATE_SCOPE_TYPES.includes(o.value))
                .map((o) => (
                  <option key={o.value} value={o.value}>{o.labelAr}</option>
                ))}
            </select>
            {errors.scopeType && <span className="field-error">{errors.scopeType.message}</span>}
          </label>
        </>
      )}

      {showRegion && (
        <label className="field">
          <span>المنطقة *</span>
          <select aria-label="المنطقة" {...register('regionId')}>
            <option value="">اختر المنطقة</option>
            {regionsQuery.data?.items.map((r) => (
              <option key={r.id} value={r.id}>{r.nameAr}</option>
            ))}
          </select>
          {errors.regionId && <span className="field-error">{errors.regionId.message}</span>}
        </label>
      )}

      {showFacility && (
        <label className="field">
          <span>السجن *</span>
          <select aria-label="السجن" {...register('facilityId')}>
            <option value="">اختر السجن</option>
            {facilitiesQuery.data?.items.map((f) => (
              <option key={f.id} value={f.id}>{f.nameAr}</option>
            ))}
          </select>
          {errors.facilityId && <span className="field-error">{errors.facilityId.message}</span>}
        </label>
      )}

      {showUnit && (
        <label className="field">
          <span>الوحدة *</span>
          <select aria-label="الوحدة" {...register('facilityUnitId')}>
            <option value="">اختر الوحدة</option>
            {facilityUnitsQuery.data?.items.map((u) => (
              <option key={u.id} value={u.id}>{u.nameAr}</option>
            ))}
          </select>
          {errors.facilityUnitId && <span className="field-error">{errors.facilityUnitId.message}</span>}
        </label>
      )}

      <label className="field">
        <span>الاسم (عربي) *</span>
        <input aria-label="الاسم (عربي)" {...register('nameAr')} />
        {errors.nameAr && <span className="field-error">{errors.nameAr.message}</span>}
      </label>

      <label className="field">
        <span>الاسم (إنجليزي)</span>
        <input aria-label="الاسم (إنجليزي)" {...register('nameEn')} />
        {errors.nameEn && <span className="field-error">{errors.nameEn.message}</span>}
      </label>

      <label className="field field-wide">
        <span>الوصف *</span>
        <textarea aria-label="الوصف" rows={4} {...register('description')} />
        {errors.description && <span className="field-error">{errors.description.message}</span>}
      </label>

      <label className="field">
        <span>مستوى التصنيف الأمني *</span>
        <select aria-label="مستوى التصنيف الأمني" {...register('classification')}>
          {enumOptions(ClassificationLevelLabelsAr).map((o) => (
            <option key={o.value} value={o.value}>{o.labelAr}</option>
          ))}
        </select>
        {errors.classification && <span className="field-error">{errors.classification.message}</span>}
      </label>

      <label className="field">
        <span>الإدارة المالكة</span>
        <select aria-label="الإدارة المالكة" {...register('ownerDepartmentId')}>
          <option value="">بدون تحديد</option>
          {departmentsQuery.data?.items.map((d) => (
            <option key={d.id} value={d.id}>{d.nameAr}</option>
          ))}
        </select>
        {errors.ownerDepartmentId && <span className="field-error">{errors.ownerDepartmentId.message}</span>}
      </label>
    </div>
  )
}
