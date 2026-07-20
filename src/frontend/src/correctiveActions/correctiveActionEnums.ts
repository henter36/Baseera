export const CorrectiveActionStatus = {
  Draft: 0,
  Open: 1,
  Assigned: 2,
  InProgress: 3,
  PendingVerification: 4,
  Completed: 5,
  Reopened: 6,
  Cancelled: 7,
} as const

export const CorrectiveActionStatusLabelsAr: Record<number, string> = {
  [CorrectiveActionStatus.Draft]: 'مسودة',
  [CorrectiveActionStatus.Open]: 'مفتوح',
  [CorrectiveActionStatus.Assigned]: 'مكلّف',
  [CorrectiveActionStatus.InProgress]: 'قيد المعالجة',
  [CorrectiveActionStatus.PendingVerification]: 'بانتظار التحقق',
  [CorrectiveActionStatus.Completed]: 'مكتمل',
  [CorrectiveActionStatus.Reopened]: 'معاد فتحه',
  [CorrectiveActionStatus.Cancelled]: 'ملغى',
}

export const CorrectiveActionPriority = {
  Low: 0,
  Medium: 1,
  High: 2,
  Critical: 3,
} as const

export const CorrectiveActionPriorityLabelsAr: Record<number, string> = {
  [CorrectiveActionPriority.Low]: 'منخفضة',
  [CorrectiveActionPriority.Medium]: 'متوسطة',
  [CorrectiveActionPriority.High]: 'عالية',
  [CorrectiveActionPriority.Critical]: 'حرجة',
}

export function correctiveActionStatusTone(status: number): 'danger' | 'warn' | 'ok' | 'muted' {
  if (status === CorrectiveActionStatus.Cancelled) return 'danger'
  if (status === CorrectiveActionStatus.Completed) return 'ok'
  if (status === CorrectiveActionStatus.PendingVerification || status === CorrectiveActionStatus.Reopened) return 'warn'
  return 'muted'
}

export function correctiveActionPriorityTone(priority: number): 'danger' | 'warn' | 'ok' | 'muted' {
  if (priority === CorrectiveActionPriority.Critical) return 'danger'
  if (priority === CorrectiveActionPriority.High) return 'warn'
  if (priority === CorrectiveActionPriority.Medium) return 'ok'
  return 'muted'
}

export const enumOptions = (labels: Record<number, string>) =>
  Object.entries(labels).map(([value, labelAr]) => ({ value: Number(value), labelAr }))
