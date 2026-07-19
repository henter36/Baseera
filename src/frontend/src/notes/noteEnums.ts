// Numeric values mirror Baseera.Domain.Notes enums (backend serializes enums as numbers).

export const NoteStatus = {
  Draft: 0,
  Open: 1,
  Assigned: 2,
  InProgress: 3,
  PendingVerification: 4,
  Closed: 5,
  Reopened: 6,
  Cancelled: 7,
} as const

export const NoteStatusLabelsAr: Record<number, string> = {
  [NoteStatus.Draft]: 'مسودة',
  [NoteStatus.Open]: 'مفتوحة',
  [NoteStatus.Assigned]: 'مكلّفة',
  [NoteStatus.InProgress]: 'قيد المعالجة',
  [NoteStatus.PendingVerification]: 'بانتظار التحقق',
  [NoteStatus.Closed]: 'مغلقة',
  [NoteStatus.Reopened]: 'معاد فتحها',
  [NoteStatus.Cancelled]: 'ملغاة',
}

export const NoteSeverity = { Low: 0, Medium: 1, High: 2, Critical: 3 } as const

export const NoteSeverityLabelsAr: Record<number, string> = {
  [NoteSeverity.Low]: 'منخفضة',
  [NoteSeverity.Medium]: 'متوسطة',
  [NoteSeverity.High]: 'عالية',
  [NoteSeverity.Critical]: 'حرجة',
}

export const NoteCategory = {
  Security: 0,
  Technical: 1,
  Operational: 2,
  HealthAndSafety: 3,
  Administrative: 4,
  Other: 5,
} as const

export const NoteCategoryLabelsAr: Record<number, string> = {
  [NoteCategory.Security]: 'أمنية',
  [NoteCategory.Technical]: 'فنية',
  [NoteCategory.Operational]: 'تشغيلية',
  [NoteCategory.HealthAndSafety]: 'صحة وسلامة',
  [NoteCategory.Administrative]: 'إدارية',
  [NoteCategory.Other]: 'أخرى',
}

export const NoteSourceType = { Manual: 0, Inspection: 1, Report: 2, Incident: 3, Form: 4 } as const

export const NoteSourceTypeLabelsAr: Record<number, string> = {
  [NoteSourceType.Manual]: 'يدوي',
  [NoteSourceType.Inspection]: 'تفتيش',
  [NoteSourceType.Report]: 'تقرير',
  [NoteSourceType.Incident]: 'واقعة',
  [NoteSourceType.Form]: 'نموذج',
}

export const ClassificationLevel = { Internal: 0, Restricted: 1, Confidential: 2, Secret: 3 } as const

export const ClassificationLevelLabelsAr: Record<number, string> = {
  [ClassificationLevel.Internal]: 'داخلي',
  [ClassificationLevel.Restricted]: 'مقيّد',
  [ClassificationLevel.Confidential]: 'سري',
  [ClassificationLevel.Secret]: 'سري للغاية',
}

export const ScopeType = {
  Global: 0,
  Headquarters: 1,
  Region: 2,
  Facility: 3,
  FacilityUnit: 4,
  MultipleRegions: 5,
  MultipleFacilities: 6,
} as const

export const ScopeTypeLabelsAr: Record<number, string> = {
  [ScopeType.Global]: 'عام (وطني)',
  [ScopeType.Headquarters]: 'المقر الرئيسي',
  [ScopeType.Region]: 'منطقة',
  [ScopeType.Facility]: 'سجن',
  [ScopeType.FacilityUnit]: 'وحدة داخل سجن',
  [ScopeType.MultipleRegions]: 'مناطق متعددة',
  [ScopeType.MultipleFacilities]: 'سجون متعددة',
}

export const AttachmentScanStatus = { PendingScan: 0, Clean: 1, Quarantined: 2, Rejected: 3 } as const

export const AttachmentScanStatusLabelsAr: Record<number, string> = {
  [AttachmentScanStatus.PendingScan]: 'قيد الفحص',
  [AttachmentScanStatus.Clean]: 'سليم',
  [AttachmentScanStatus.Quarantined]: 'محجوز',
  [AttachmentScanStatus.Rejected]: 'مرفوض',
}

export function severityTone(severity: number): 'danger' | 'warn' | 'ok' | 'muted' {
  if (severity === NoteSeverity.Critical) return 'danger'
  if (severity === NoteSeverity.High) return 'warn'
  if (severity === NoteSeverity.Medium) return 'ok'
  return 'muted'
}

export function statusTone(status: number): 'danger' | 'warn' | 'ok' | 'muted' {
  if (status === NoteStatus.Cancelled) return 'danger'
  if (status === NoteStatus.Closed) return 'ok'
  if (status === NoteStatus.PendingVerification || status === NoteStatus.Reopened) return 'warn'
  return 'muted'
}

export const enumOptions = (labels: Record<number, string>) =>
  Object.entries(labels).map(([value, labelAr]) => ({ value: Number(value), labelAr }))
