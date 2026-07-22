import { useQuery } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'
import { api, ApiError } from '../../../api/client'
import { usePermission } from '../../../auth/AuthProvider'

export function FormVersionDetailPage() {
  const canViewForms = usePermission('Forms.View')
  const canViewHistory = usePermission('Forms.ViewVersionHistory')
  const canView = canViewForms || canViewHistory
  const { formId, versionId } = useParams<{ formId: string; versionId: string }>()
  const query = useQuery({
    queryKey: ['form-version', formId, versionId],
    queryFn: () => api.forms.getVersion(formId!, versionId!),
    enabled: canView && !!formId && !!versionId,
  })

  if (!canView) return <div className="error" role="alert">ليست لديك صلاحية العرض.</div>
  if (query.isLoading) return <div className="loading">جاري التحميل…</div>
  if (query.isError) {
    const err = query.error as ApiError
    return <div className="error" role="alert">{err.status === 404 ? 'الإصدار غير موجود.' : err.message}</div>
  }
  const v = query.data!
  return (
    <div className="panel" dir="rtl">
      <div className="page-header">
        <h1 className="page-title">إصدار v{v.versionNumber}</h1>
        <div className="toolbar">
          <Link to={`/forms/${formId}/versions`}>كل الإصدارات</Link>
          {(v.status === 0 || v.status === 2) && <Link to={`/forms/${formId}/versions/${versionId}/edit`}>فتح المصمم</Link>}
          {v.status === 1 && <Link to={`/forms/${formId}/versions/${versionId}/review`}>مراجعة</Link>}
          {v.snapshotId && <Link to={`/forms/${formId}/versions/${versionId}/snapshot`}>اللقطة</Link>}
        </div>
      </div>
      <dl className="detail-grid">
        <dt>الحالة</dt><dd>{v.statusAr}</dd>
        <dt>تجزئة المخطط</dt><dd><code>{v.draftSchemaHash ?? '—'}</code></dd>
        <dt>آخر حفظ</dt><dd>{v.lastSavedAtUtc ? new Date(v.lastSavedAtUtc).toLocaleString('ar-SA') : '—'}</dd>
      </dl>
    </div>
  )
}
