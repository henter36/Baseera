import { useQuery } from '@tanstack/react-query'
import { api, ApiError } from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'

export function NoteTypesSettingsPage() {
  const canManage = usePermission('Notes.ManageTypes')
  const query = useQuery({
    queryKey: ['settings-note-types'],
    queryFn: () => api.noteTypes(true),
    enabled: canManage,
  })

  if (!canManage) {
    return <div className="error" role="alert">ليست لديك صلاحية إدارة أنواع الملاحظات.</div>
  }

  const error = query.error instanceof ApiError ? query.error.message : 'تعذر تحميل أنواع الملاحظات.'

  return (
    <div className="panel">
      <div className="page-header">
        <div>
          <h1 className="page-title">أنواع الملاحظات</h1>
          <p className="muted">إدارة الأنواع الديناميكية وترتيبها والقيم الافتراضية.</p>
        </div>
      </div>

      {query.isLoading && <div className="loading">جاري التحميل…</div>}
      {query.isError && (
        <div className="error" role="alert">
          <span>{error}</span>
          <button type="button" className="secondary" onClick={() => query.refetch()}>إعادة المحاولة</button>
        </div>
      )}
      {query.data?.length === 0 && <div className="empty">لا توجد أنواع ملاحظات.</div>}
      {query.data && query.data.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>الرمز</th>
              <th>الاسم</th>
              <th>الوصف</th>
              <th>الترتيب</th>
              <th>الخطورة الافتراضية</th>
              <th>مدة الاستحقاق</th>
              <th>الحالة</th>
            </tr>
          </thead>
          <tbody>
            {query.data.map((type) => (
              <tr key={type.id}>
                <td>{type.code}</td>
                <td>{type.nameAr}</td>
                <td>{type.descriptionAr || '—'}</td>
                <td>{type.sortOrder}</td>
                <td>{type.defaultSeverityAr}</td>
                <td>{type.defaultDueDays ?? '—'}</td>
                <td><span className="badge" data-tone={type.isActive ? 'success' : 'neutral'}>{type.isActive ? 'فعال' : 'غير فعال'}</span></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}
