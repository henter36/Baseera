export const FormResponseStatusLabelsAr: Record<number, string> = {
  0: 'مسودة',
  1: 'مُرسل',
  2: 'قيد المراجعة',
  3: 'مُعاد',
  4: 'معتمد',
  5: 'مرفوض',
  6: 'مغلق',
}

export const FormAssignmentWorkStatusLabelsAr: Record<number, string> = {
  0: 'لم يبدأ',
  1: 'مسودة',
  2: 'مُرسل',
  3: 'قيد المراجعة',
  4: 'مُعاد',
  5: 'معتمد',
  6: 'مرفوض',
  7: 'مغلق',
  8: 'متأخر',
}

export type AutosaveUiState = 'dirty' | 'saving' | 'saved' | 'offline' | 'error' | 'conflict'

export const AutosaveUiLabelsAr: Record<AutosaveUiState, string> = {
  dirty: 'غير محفوظ',
  saving: 'جاري الحفظ',
  saved: 'تم الحفظ',
  offline: 'غير متصل',
  error: 'فشل الحفظ',
  conflict: 'تعارض في النسخة',
}
