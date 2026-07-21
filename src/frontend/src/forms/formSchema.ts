import { z } from 'zod'
import { ScopeType } from './formEnums'

const GUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
const CODE_RE = /^[A-Za-z][A-Za-z0-9._-]{2,79}$/

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
  z.string().refine((v) => v !== '' && !Number.isNaN(Number(v)), { message })

export const createFormSchema = z
  .object({
    code: z
      .string()
      .trim()
      .min(1, 'رمز النموذج مطلوب.')
      .max(80, 'رمز النموذج طويل جدًا.')
      .refine((v) => CODE_RE.test(v), {
        message: 'رمز النموذج يجب أن يبدأ بحرف ويحتوي على أحرف كبيرة وأرقام أو . _ - فقط.',
      }),
    nameAr: z.string().trim().min(1, 'اسم النموذج مطلوب.').max(200, 'الاسم طويل جدًا.'),
    nameEn: z.string().trim().max(200, 'الاسم الإنجليزي طويل جدًا.').optional().or(z.literal('')),
    description: z.string().trim().min(1, 'الوصف مطلوب.').max(2000, 'الوصف طويل جدًا.'),
    classification: requiredEnumString('مستوى التصنيف الأمني غير صالح.'),
    scopeType: requiredEnumString('نوع النطاق غير صالح.'),
    regionId: optionalGuid,
    facilityId: optionalGuid,
    facilityUnitId: optionalGuid,
    ownerDepartmentId: optionalGuid,
  })
  .superRefine((data, ctx) => {
    const scope = Number(data.scopeType)
    if (scope === ScopeType.Region && !data.regionId) {
      ctx.addIssue({ code: 'custom', path: ['regionId'], message: 'يجب اختيار المنطقة.' })
    }
    if (scope === ScopeType.Facility) {
      if (!data.regionId) {
        ctx.addIssue({ code: 'custom', path: ['regionId'], message: 'يجب اختيار المنطقة أولًا.' })
      }
      if (!data.facilityId) {
        ctx.addIssue({ code: 'custom', path: ['facilityId'], message: 'يجب اختيار السجن.' })
      }
    }
    if (scope === ScopeType.FacilityUnit) {
      if (!data.regionId) {
        ctx.addIssue({ code: 'custom', path: ['regionId'], message: 'يجب اختيار المنطقة أولًا.' })
      }
      if (!data.facilityId) {
        ctx.addIssue({ code: 'custom', path: ['facilityId'], message: 'يجب اختيار السجن.' })
      }
      if (!data.facilityUnitId) {
        ctx.addIssue({ code: 'custom', path: ['facilityUnitId'], message: 'يجب اختيار الوحدة.' })
      }
    }
  })

export type CreateFormFormValues = z.infer<typeof createFormSchema>

export const updateFormSchema = z.object({
  nameAr: z.string().trim().min(1, 'اسم النموذج مطلوب.').max(200, 'الاسم طويل جدًا.'),
  nameEn: z.string().trim().max(200, 'الاسم الإنجليزي طويل جدًا.').optional().or(z.literal('')),
  description: z.string().trim().min(1, 'الوصف مطلوب.').max(2000, 'الوصف طويل جدًا.'),
  classification: requiredEnumString('مستوى التصنيف الأمني غير صالح.'),
  ownerDepartmentId: optionalGuid,
})

export type UpdateFormFormValues = z.infer<typeof updateFormSchema>

export const formTransitionSchema = z.object({
  reason: z.string().trim().min(1, 'السبب مطلوب.').max(2000, 'السبب طويل جدًا.'),
})

export type FormTransitionFormValues = z.infer<typeof formTransitionSchema>

export const createFormAccessGrantSchema = z
  .object({
    principalType: requiredEnumString('نوع المستفيد غير صالح.'),
    principalId: z.string().trim().min(1, 'المستفيد مطلوب.').refine((v) => GUID_RE.test(v), {
      message: 'معرف المستفيد غير صالح.',
    }),
    capability: requiredEnumString('الصلاحية غير صالحة.'),
    effect: requiredEnumString('نوع التأثير غير صالح.'),
    scopeType: z.string().optional().or(z.literal('')),
    regionId: optionalGuid,
    facilityId: optionalGuid,
    validFromUtc: optionalDate,
    validToUtc: optionalDate,
    reason: z.string().trim().min(1, 'سبب المنح مطلوب.').max(1000, 'السبب طويل جدًا.'),
  })
  .superRefine((data, ctx) => {
    const scope = data.scopeType === '' ? null : Number(data.scopeType)
    if (scope === ScopeType.Region && !data.regionId) {
      ctx.addIssue({ code: 'custom', path: ['regionId'], message: 'يجب اختيار المنطقة.' })
    }
    if (scope === ScopeType.Facility && !data.facilityId) {
      ctx.addIssue({ code: 'custom', path: ['facilityId'], message: 'يجب اختيار السجن.' })
    }
    if (data.validFromUtc && data.validToUtc) {
      const from = Date.parse(data.validFromUtc)
      const to = Date.parse(data.validToUtc)
      if (!Number.isNaN(from) && !Number.isNaN(to) && to <= from) {
        ctx.addIssue({ code: 'custom', path: ['validToUtc'], message: 'تاريخ الانتهاء يجب أن يكون بعد تاريخ البداية.' })
      }
    }
  })

export type CreateFormAccessGrantFormValues = z.infer<typeof createFormAccessGrantSchema>

export const updateFormGovernancePolicySchema = z.object({
  requireReviewBeforeApproval: z.boolean(),
  requireSeparationOfDuties: z.boolean(),
  allowDesignerToReviewOwnForm: z.boolean(),
  allowReviewerToApproveOwnReview: z.boolean(),
  allowApproverToPublish: z.boolean(),
  defaultRetentionDays: z.number().int().min(0, 'قيمة غير صالحة.'),
  sensitiveRetentionDays: z.number().int().min(0, 'قيمة غير صالحة.'),
  minimumRetentionDays: z.number().int().min(0, 'قيمة غير صالحة.'),
  auditSensitiveViews: z.boolean(),
  auditExports: z.boolean(),
  requireReasonForArchive: z.boolean(),
})

export type UpdateFormGovernancePolicyFormValues = z.infer<typeof updateFormGovernancePolicySchema>
