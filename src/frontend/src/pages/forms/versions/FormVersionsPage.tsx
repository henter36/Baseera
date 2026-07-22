import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api, ApiError } from '../../../api/client'
import { usePermission } from '../../../auth/AuthProvider'
import { formatApiError } from '../../../forms/designer/designerHelpers'
import type { FormVersionStatus } from '../../../api/client'

export function FormVersionsPage() {
  const canViewHistory = usePermission('Forms.ViewVersionHistory')
  const canDesign = usePermission('Forms.UpdateDraft')
  const { formId } = useParams<{ formId: string }>()
  const qc = useQueryClient()
  const navigate = useNavigate()

  const versionsQuery = useQuery({
    queryKey: ['form-versions', formId],
    queryFn: () => api.forms.listVersions(formId!),
    enabled: canViewHistory && !!formId,
  })

  const createMutation = useMutation({
    mutationFn: () => api.forms.createVersion(formId!),
    onSuccess: (version) => {
      void qc.invalidateQueries({ queryKey: ['form-versions', formId] })
      void navigate(`/forms/${formId}/versions/${version.id}/edit`)
    },
  })

  if (!canViewHistory) {
    return <div className="error" role="alert">ليست لديك صلاحية عرض إصدارات النموذج.</div>
  }

  if (versionsQuery.isLoading) {
    return <div className="loading">جاري تحميل الإصدارات…</div>
  }

  if (versionsQuery.isError) {
    return <div className="error" role="alert">{formatApiError(versionsQuery.error as ApiError)}</div>
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
            {items.map((v) => {
              const status = v.status as FormVersionStatus
              const editable = status === 0 || status === 2
              return (
                <tr key={v.id}>
                  <td>v{v.versionNumber}</td>
                  <td>{v.statusAr}</td>
                  <td>{v.lastSavedAtUtc ? new Date(v.lastSavedAtUtc).toLocaleString('ar-SA') : '—'}</td>
                  <td>
                    <Link to={`/forms/${formId}/versions/${v.id}`}>عرض</Link>
                    {editable && canDesign && (
                      <>
                        {' · '}
                        <Link to={`/forms/${formId}/versions/${v.id}/edit`}>تصميم</Link>
                      </>
                    )}
                    {status === 4 && (
                      <>
                        {' · '}
                        <Link to={`/forms/${formId}/versions/${v.id}/snapshot`}>لقطة</Link>
                      </>
                    )}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      )}
    </div>
  )
}
