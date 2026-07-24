import { useQuery } from '@tanstack/react-query'
import { Link, useParams } from 'react-router'
import { api, ApiError } from '../../../api/client'
import { usePermission } from '../../../auth/AuthProvider'
import { formatApiError, hasAllowedAction } from '../../../forms/designer/designerHelpers'

export function FormVersionDetailPage() {
  const canViewHistory = usePermission('Forms.ViewVersionHistory')
  const { formId, versionId } = useParams<{ formId: string; versionId: string }>()
  const query = useQuery({
    queryKey: ['form-version', formId, versionId],
    queryFn: () => api.forms.getVersion(formId!, versionId!),
    enabled: canViewHistory && !!formId && !!versionId,
  })

  if (!canViewHistory) {
    return <div className="error" role="alert">ليست لديك صلاحية عرض سجل الإصدارات.</div>
  }

  if (query.isLoading) {
    return <div className="loading">جاري التحميل…</div>
  }

  if (query.isError) {
    return <div className="error" role="alert">{formatApiError(query.error as ApiError)}</div>
  }

  const v = query.data!
  const actions = v.allowedActions
  const canDesign = hasAllowedAction(actions, 'SaveSchema')
  const canReview = hasAllowedAction(actions, 'RequestChanges') || hasAllowedAction(actions, 'Reject') || hasAllowedAction(actions, 'ApproveAndLock')
  const canViewSnapshot = hasAllowedAction(actions, 'ViewSnapshot')

  return (
    <div className="panel" dir="rtl">
      <div className="page-header">
        <h1 className="page-title">إصدار v{v.versionNumber}</h1>
        <div className="toolbar">
          <Link to={`/forms/${formId}/versions`}>كل الإصدارات</Link>
          {canDesign && <Link to={`/forms/${formId}/versions/${versionId}/edit`}>فتح المصمم</Link>}
          {canReview && <Link to={`/forms/${formId}/versions/${versionId}/review`}>مراجعة</Link>}
          {canViewSnapshot && <Link to={`/forms/${formId}/versions/${versionId}/snapshot`}>اللقطة</Link>}
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
