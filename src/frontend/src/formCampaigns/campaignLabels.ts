export const FormCampaignStatusLabelsAr: Record<number, string> = {
  0: 'مسودة',
  1: 'مجدولة',
  2: 'نشطة',
  3: 'موقوفة',
  4: 'مكتملة',
  5: 'ملغاة',
}

export const FormRecurrenceKindLabelsAr: Record<number, string> = {
  0: 'مرة واحدة',
  1: 'يومي',
  2: 'أسبوعي',
  3: 'شهري',
  4: 'تواريخ مخصصة',
}

export const FormCycleStatusLabelsAr: Record<number, string> = {
  0: 'مجدولة',
  1: 'مفتوحة',
  2: 'مهلة',
  3: 'مغلقة',
  4: 'ملغاة',
}

export function formatCycleStatusAr(status: number): string {
  return FormCycleStatusLabelsAr[status] ?? 'حالة غير معروفة'
}

export function formatRiyadh(iso?: string | null): string {
  if (!iso) return '—'
  return new Date(iso).toLocaleString('ar-SA', { timeZone: 'Asia/Riyadh' })
}
