// Numeric values mirror Baseera.Domain.Forms enums (backend serializes enums as numbers).

export const FormDefinitionStatus = {
  Draft: 0,
  InReview: 1,
  ChangesRequested: 2,
  Approved: 3,
  Rejected: 4,
  Archived: 5,
} as const

export const FormDefinitionStatusLabelsAr: Record<number, string> = {
  [FormDefinitionStatus.Draft]: 'مسودة',
  [FormDefinitionStatus.InReview]: 'قيد المراجعة',
  [FormDefinitionStatus.ChangesRequested]: 'تعديلات مطلوبة',
  [FormDefinitionStatus.Approved]: 'معتمد',
  [FormDefinitionStatus.Rejected]: 'مرفوض',
  [FormDefinitionStatus.Archived]: 'مؤرشف',
}

export const FormReviewDecisionType = {
  SubmitForReview: 0,
  RequestChanges: 1,
  Approve: 2,
  Reject: 3,
  Archive: 4,
  Restore: 5,
} as const

export const FormReviewDecisionTypeLabelsAr: Record<number, string> = {
  [FormReviewDecisionType.SubmitForReview]: 'إرسال للمراجعة',
  [FormReviewDecisionType.RequestChanges]: 'طلب تعديلات',
  [FormReviewDecisionType.Approve]: 'اعتماد',
  [FormReviewDecisionType.Reject]: 'رفض',
  [FormReviewDecisionType.Archive]: 'أرشفة',
  [FormReviewDecisionType.Restore]: 'استعادة',
}

export const FormAccessCapability = {
  View: 0,
  Design: 1,
  Review: 2,
  Approve: 3,
  Archive: 4,
  Restore: 5,
  ViewSensitive: 6,
  ManageAccess: 7,
  ManageRetention: 8,
} as const

export const FormAccessCapabilityLabelsAr: Record<number, string> = {
  [FormAccessCapability.View]: 'عرض',
  [FormAccessCapability.Design]: 'تصميم',
  [FormAccessCapability.Review]: 'مراجعة',
  [FormAccessCapability.Approve]: 'اعتماد',
  [FormAccessCapability.Archive]: 'أرشفة',
  [FormAccessCapability.Restore]: 'استعادة',
  [FormAccessCapability.ViewSensitive]: 'عرض حساس',
  [FormAccessCapability.ManageAccess]: 'إدارة الوصول',
  [FormAccessCapability.ManageRetention]: 'إدارة الاحتفاظ',
}

export const FormAccessGrantEffect = { Allow: 0, Deny: 1 } as const

export const FormAccessGrantEffectLabelsAr: Record<number, string> = {
  [FormAccessGrantEffect.Allow]: 'سماح',
  [FormAccessGrantEffect.Deny]: 'منع',
}

export const FormAccessGrantPrincipalType = { User: 0, Role: 1 } as const

export const FormAccessGrantPrincipalTypeLabelsAr: Record<number, string> = {
  [FormAccessGrantPrincipalType.User]: 'مستخدم',
  [FormAccessGrantPrincipalType.Role]: 'دور',
}

export {
  ClassificationLevel,
  ClassificationLevelLabelsAr,
  ScopeType,
  ScopeTypeLabelsAr,
  enumOptions,
} from '../notes/noteEnums'

export function formStatusTone(status: number): 'danger' | 'warn' | 'ok' | 'muted' {
  if (status === FormDefinitionStatus.Rejected) return 'danger'
  if (status === FormDefinitionStatus.Approved) return 'ok'
  if (status === FormDefinitionStatus.InReview || status === FormDefinitionStatus.ChangesRequested) return 'warn'
  if (status === FormDefinitionStatus.Archived) return 'muted'
  return 'muted'
}

export function classificationTone(classification: number): 'danger' | 'warn' | 'ok' | 'muted' {
  if (classification >= 2) return 'danger'
  if (classification === 1) return 'warn'
  return 'muted'
}
