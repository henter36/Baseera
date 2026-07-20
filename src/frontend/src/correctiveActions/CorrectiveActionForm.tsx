import { useQuery } from '@tanstack/react-query'
import { useFormContext } from 'react-hook-form'
import { api } from '../api/client'
import { ClassificationLevelLabelsAr } from '../notes/noteEnums'
import {
  CorrectiveActionPriorityLabelsAr,
  enumOptions,
} from './correctiveActionEnums'
import type { CorrectiveActionFormValues } from './correctiveActionSchema'

export function CorrectiveActionForm({ noteTitle }: Readonly<{ noteTitle?: string }>) {
  const {
    register,
    formState: { errors },
  } = useFormContext<CorrectiveActionFormValues>()

  const departmentsQuery = useQuery({
    queryKey: ['corrective-action-form-departments'],
    queryFn: () => api.departments(),
  })

  return (
    <div className="form-grid">
      {noteTitle && (
        <div className="field field-wide">
          <span>الملاحظة الأصلية</span>
          <strong>{noteTitle}</strong>
        </div>
      )}

      <label className="field">
        <span>العنوان *</span>
        <input aria-label="عنوان الإجراء التصحيحي" {...register('title')} />
        {errors.title && <span className="field-error">{errors.title.message as string}</span>}
      </label>

      <label className="field">
        <span>الأولوية *</span>
        <select aria-label="أولوية الإجراء التصحيحي" {...register('priority')}>
          {enumOptions(CorrectiveActionPriorityLabelsAr).map((o) => (
            <option key={o.value} value={o.value}>{o.labelAr}</option>
          ))}
        </select>
      </label>

      <label className="field field-wide">
        <span>الوصف *</span>
        <textarea aria-label="وصف الإجراء التصحيحي" rows={4} {...register('description')} />
        {errors.description && <span className="field-error">{errors.description.message as string}</span>}
      </label>

      <label className="field">
        <span>التصنيف الأمني *</span>
        <select aria-label="تصنيف الإجراء التصحيحي" {...register('classification')}>
          {enumOptions(ClassificationLevelLabelsAr).map((o) => (
            <option key={o.value} value={o.value}>{o.labelAr}</option>
          ))}
        </select>
      </label>

      <label className="field">
        <span>الإدارة المالكة</span>
        <select aria-label="الإدارة المالكة للإجراء" {...register('ownerDepartmentId')}>
          <option value="">بدون تحديد إدارة</option>
          {departmentsQuery.data?.items.map((d) => (
            <option key={d.id} value={d.id}>{d.nameAr}</option>
          ))}
        </select>
        {errors.ownerDepartmentId && <span className="field-error">{errors.ownerDepartmentId.message as string}</span>}
      </label>

      <label className="field">
        <span>تاريخ الاستحقاق</span>
        <input aria-label="تاريخ استحقاق الإجراء" type="datetime-local" {...register('dueAtUtc')} />
        {errors.dueAtUtc && <span className="field-error">{errors.dueAtUtc.message as string}</span>}
      </label>
    </div>
  )
}
