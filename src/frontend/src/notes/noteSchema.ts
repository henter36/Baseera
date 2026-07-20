import { z } from 'zod'
import { ScopeType } from './noteEnums'

// Form values are kept as plain strings (matching raw <select>/<input> DOM values) to avoid
// zod v4 `coerce` widening react-hook-form's inferred field types to `unknown`. Numeric
// conversion happens once, in the page's mutation payload builder, right before the API call.

const GUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

const optionalGuid = z
  .string()
  .trim()
  .optional()
  .or(z.literal(''))
  .refine((v) => !v || GUID_RE.test(v), { message: 'معرف غير صالح (يجب أن يكون UUID).' })

const optionalDate = z
  .string()
  .trim()
  .optional()
  .or(z.literal(''))
  .refine((v) => !v || !Number.isNaN(Date.parse(v)), { message: 'تاريخ غير صالح.' })

const requiredEnumString = (message: string) =>
  z
    .string()
    .refine((v) => v !== '' && !Number.isNaN(Number(v)), { message })

export const noteCommonFieldsSchema = z.object({
  title: z
    .string()
    .trim()
    .min(1, 'العنوان مطلوب.')
    .max(300, 'العنوان يجب ألا يتجاوز 300 حرف.'),
  description: z
    .string()
    .trim()
    .min(1, 'الوصف مطلوب.')
    .max(8000, 'الوصف يجب ألا يتجاوز 8000 حرف.'),
  noteTypeId: z.string().trim().min(1, 'نوع الملاحظة مطلوب.').refine((v) => GUID_RE.test(v), {
    message: 'نوع الملاحظة غير صالح.',
  }),
  severity: requiredEnumString('مستوى الخطورة غير صالح.'),
  sourceType: requiredEnumString('نوع المصدر غير صالح.'),
  sourceReference: z
    .string()
    .trim()
    .max(200, 'مرجع المصدر طويل جدًا.')
    .optional()
    .or(z.literal('')),
  classification: requiredEnumString('مستوى التصنيف الأمني غير صالح.'),
  ownerDepartmentId: optionalGuid,
  dueAtUtc: optionalDate,
})

export const createNoteSchema = noteCommonFieldsSchema
  .extend({
    scopeType: z.string().refine((v) => v === String(ScopeType.Facility), {
      message: 'نطاق الملاحظة مطلوب.',
    }),
    regionId: optionalGuid,
    facilityId: optionalGuid,
    facilityUnitId: optionalGuid,
  })
  .superRefine((data, ctx) => {
    const hasFacility = !!data.facilityId
    if (!data.regionId) {
      ctx.addIssue({ code: 'custom', path: ['regionId'], message: 'يجب اختيار المنطقة أولًا.' })
    }
    if (!hasFacility) {
      ctx.addIssue({ code: 'custom', path: ['facilityId'], message: 'يجب اختيار السجن.' })
    }

    if (data.dueAtUtc) {
      const due = Date.parse(data.dueAtUtc)
      if (due < Date.now() - 60_000) {
        ctx.addIssue({ code: 'custom', path: ['dueAtUtc'], message: 'تاريخ الاستحقاق يجب أن يكون في المستقبل.' })
      }
    }
  })

export type CreateNoteFormValues = z.infer<typeof createNoteSchema>

export const updateNoteSchema = noteCommonFieldsSchema

export type UpdateNoteFormValues = z.infer<typeof updateNoteSchema>

export const assignNoteSchema = z
  .object({
    assignedToUserId: optionalGuid,
    assignedToDepartmentId: optionalGuid,
    dueAtUtc: optionalDate,
    reason: z.string().trim().min(1, 'سبب التكليف مطلوب.').max(2000, 'السبب طويل جدًا.'),
  })
  .superRefine((data, ctx) => {
    const hasUser = !!data.assignedToUserId
    const hasDept = !!data.assignedToDepartmentId
    if (hasUser === hasDept) {
      ctx.addIssue({
        code: 'custom',
        path: ['assignedToUserId'],
        message: 'يجب تحديد مستخدم أو إدارة واحدة فقط للتكليف.',
      })
    }
  })

export type AssignNoteFormValues = z.infer<typeof assignNoteSchema>
