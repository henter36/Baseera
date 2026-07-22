import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'
import { api, ApiError } from '../../../api/client'
import { usePermission } from '../../../auth/AuthProvider'

const statusAr: Record<number, string> = {
  0: 'مسودة',
  1: 'قيد المراجعة',
  2: 'مطلوب تعديلات',
  3: 'مرفوض',
  4: 'مقفل',
}

export function FormVersionsPage() {
  const canViewForms = usePermission('Forms.View')
  const canViewHistory = usePermission('Forms.ViewVersionHistory')
  const canView = canViewForms || canViewHistory
  const canDesign = usePermission('Forms.UpdateDraft')
  const { formId } = useParams<{ formId: string }>()
  const qc = useQueryClient()

  const versionsQuery = useQuery({
    queryKey: ['form-versions', formId],
    queryFn: () => api.forms.listVersions(formId!),
    enabled: canView && !!formId,
  })

  const createMutation = useMutation({
    mutationFn: () => api.forms.createVersion(formId!),
    onSuccess: (version) => {
      void qc.invalidateQueries({ queryKey: ['form-versions', formId] })
      window.location.assign(`/forms/${formId}/versions/${version.id}/edit`)
    },
  })

  if (!canView) return <div className="error" role="alert">ليست لديك صلاحية عرض إصدارات النموذج.</div>
  if (versionsQuery.isLoading) return <div className="loading">جاري تحميل الإصدارات…</div>
  if (versionsQuery.isError) {
    const err = versionsQuery.error as ApiError
    return <div className="error" role="alert">{err.status === 404 ? 'النموذج غير موجود أو خارج نطاقك.' : err.message}</div>
  }

  const items = versionsQuery.data ?? []

  return (
    <div className="panel" dir="rtl">
      <div className="page-header">
        <h1 className="page-title">إصدارات النموذج</h1>
        <div className="toolbar">
          <Link to={`/forms/${formId}`} className="secondary">عودة للنموذج</Link>
          {canDesign && (
            <button type="button" onClick={() => createMutation.mutate()} disabled={createMutation.isPending}>
              إصدار جديد
            </button>
          )}
        </div>
      </div>
      {items.length === 0 ? (
        <div className="empty">لا توجد إصدارات بعد.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>الرقم</th>
              <th>الحالة</th>
              <th>آخر حفظ</th>
              <th>الإجراءات</th>
            </tr>
          </thead>
          <tbody>
            {items.map((v) => (
              <tr key={v.id}>
                <td>v{v.versionNumber}</td>
                <td>{statusAr[v.status] ?? v.statusAr}</td>
                <td>{v.lastSavedAtUtc ? new Date(v.lastSavedAtUtc).toLocaleString('ar-SA') : '—'}</td>
                <td>
                  <Link to={`/forms/${formId}/versions/${v.id}`}>عرض</Link>
                  {' · '}
                  {(v.status === 0 || v.status === 2) && canDesign && (
                    <Link to={`/forms/${formId}/versions/${v.id}/edit`}>تصميم</Link>
                  )}
                  {v.status === 4 && (
                    <>
                      {' · '}
                      <Link to={`/forms/${formId}/versions/${v.id}/snapshot`}>لقطة</Link>
                    </>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}
