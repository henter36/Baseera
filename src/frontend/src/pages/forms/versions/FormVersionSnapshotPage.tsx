import { useQuery } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'
import { api, ApiError } from '../../../api/client'
import { usePermission } from '../../../auth/AuthProvider'

export function FormVersionSnapshotPage() {
  const canViewForms = usePermission('Forms.View')
  const canViewHistory = usePermission('Forms.ViewVersionHistory')
  const canView = canViewForms || canViewHistory
  const { formId, versionId } = useParams<{ formId: string; versionId: string }>()
  const query = useQuery({
    queryKey: ['form-version-snapshot', formId, versionId],
    queryFn: () => api.forms.getVersionSnapshot(formId!, versionId!),
    enabled: canView && !!formId && !!versionId,
  })

  if (!canView) return <div className="error" role="alert">ليست لديك صلاحية العرض.</div>
  if (query.isLoading) return <div className="loading">جاري التحميل…</div>
  if (query.isError) {
    const err = query.error as ApiError
    return <div className="error" role="alert">{err.status === 404 ? 'اللقطة غير موجودة.' : err.message}</div>
  }
  const s = query.data!
  return (
    <div className="panel" dir="rtl">
      <div className="page-header">
        <h1 className="page-title">لقطة المخطط</h1>
        <Link to={`/forms/${formId}/versions/${versionId}`}>عودة</Link>
      </div>
      <dl className="detail-grid">
        <dt>التجزئة</dt><dd><code>{s.schemaHash}</code></dd>
        <dt>الحجم</dt><dd>{s.schemaSizeBytes} بايت</dd>
        <dt>الصفحات / الأقسام / الحقول</dt>
        <dd>{s.pageCount} / {s.sectionCount} / {s.fieldCount}</dd>
      </dl>
      <pre style={{ whiteSpace: 'pre-wrap', maxHeight: 480, overflow: 'auto' }}>{s.canonicalSchemaJson}</pre>
    </div>
  )
}
