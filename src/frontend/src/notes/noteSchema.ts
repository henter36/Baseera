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
  category: requiredEnumString('التصنيف غير صالح.'),
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
    scopeType: z.string().refine((v) => v !== '' && v !== '-1' && !Number.isNaN(Number(v)), {
      message: 'نطاق الملاحظة مطلوب.',
    }),
    regionId: optionalGuid,
    facilityId: optionalGuid,
    facilityUnitId: optionalGuid,
  })
  .superRefine((data, ctx) => {
    const hasRegion = !!data.regionId
    const hasFacility = !!data.facilityId
    const hasUnit = !!data.facilityUnitId
    const scopeType = Number(data.scopeType)

    switch (scopeType) {
      case ScopeType.Global:
      case ScopeType.Headquarters:
        if (hasRegion || hasFacility || hasUnit) {
          ctx.addIssue({
            code: 'custom',
            path: ['scopeType'],
            message: 'نطاق عام/مقر رئيسي لا يقبل تحديد منطقة أو سجن أو وحدة.',
          })
        }
        break
      case ScopeType.Region:
        if (!hasRegion || hasFacility || hasUnit) {
          ctx.addIssue({ code: 'custom', path: ['regionId'], message: 'نطاق المنطقة يتطلب اختيار منطقة فقط.' })
        }
        break
      case ScopeType.Facility:
        if (!hasFacility || hasUnit) {
          ctx.addIssue({ code: 'custom', path: ['facilityId'], message: 'نطاق السجن يتطلب اختيار سجن دون وحدة.' })
        }
        break
      case ScopeType.FacilityUnit:
        if (!hasFacility || !hasUnit) {
          ctx.addIssue({ code: 'custom', path: ['facilityUnitId'], message: 'نطاق الوحدة يتطلب اختيار السجن والوحدة معًا.' })
        }
        break
      default:
        ctx.addIssue({ code: 'custom', path: ['scopeType'], message: 'نطاق الملاحظة غير مدعوم في هذه المرحلة.' })
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
