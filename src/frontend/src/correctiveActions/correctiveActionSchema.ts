import { z } from 'zod'

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
  z.string().refine((v) => v !== '' && !Number.isNaN(Number(v)), { message })

export const correctiveActionFormSchema = z.object({
  title: z.string().trim().min(1, 'العنوان مطلوب.').max(300, 'العنوان يجب ألا يتجاوز 300 حرف.'),
  description: z.string().trim().min(1, 'الوصف مطلوب.').max(8000, 'الوصف يجب ألا يتجاوز 8000 حرف.'),
  priority: requiredEnumString('الأولوية غير صالحة.'),
  classification: requiredEnumString('التصنيف الأمني غير صالح.'),
  ownerDepartmentId: optionalGuid,
  dueAtUtc: optionalDate,
}).superRefine((data, ctx) => {
  if (data.dueAtUtc) {
    const due = Date.parse(data.dueAtUtc)
    if (due < Date.now() - 60_000) {
      ctx.addIssue({ code: 'custom', path: ['dueAtUtc'], message: 'تاريخ الاستحقاق يجب ألا يكون في الماضي.' })
    }
  }
})

export type CorrectiveActionFormValues = z.infer<typeof correctiveActionFormSchema>

export const transitionSchema = z.object({
  reason: z.string().trim().min(1, 'السبب مطلوب.').max(2000, 'السبب طويل جدًا.'),
  completionSummary: z.string().trim().max(4000, 'ملخص الإنجاز طويل جدًا.').optional().or(z.literal('')),
})
