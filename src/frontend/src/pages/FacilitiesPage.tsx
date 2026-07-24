import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { api } from '../api/client'
import { usePermission } from '../auth/AuthProvider'

export function FacilitiesPage() {
  const canView = usePermission('Organization.View')
  const canViewWorkspaces = usePermission('Workspaces.View')
  const canViewFacilityWorkspaceLevel = usePermission('Workspaces.ViewFacility')
  const canViewFacilityWorkspace = canViewWorkspaces && canViewFacilityWorkspaceLevel
  const [search, setSearch] = useState('')
  const query = useQuery({
    queryKey: ['facilities', search],
    queryFn: () => api.facilities(undefined, search),
    enabled: canView,
  })

  if (!canView) {
    return <div className="error" role="alert">ليست لديك صلاحية عرض السجون.</div>
  }

  return (
    <div className="panel">
      <h1 className="page-title">السجون</h1>
      <p className="muted">القائمة مفلترة على الخادم حسب نطاق المستخدم.</p>
      <div className="toolbar">
        <input
          aria-label="بحث السجون"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="بحث بالاسم أو الرمز"
        />
      </div>
      {query.isLoading && <div className="loading">جاري التحميل…</div>}
      {query.isError && <div className="error" role="alert">{(query.error as Error).message}</div>}
      {query.data && query.data.items.length === 0 && <div className="empty">لا توجد سجون ضمن نطاقك.</div>}
      {query.data && query.data.items.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>الرمز</th>
              <th>الاسم</th>
              <th>النوع</th>
              <th>الحالة</th>
              {canViewFacilityWorkspace && <th>مساحة العمل</th>}
            </tr>
          </thead>
          <tbody>
            {query.data.items.map((facility) => (
              <tr key={facility.id}>
                <td>{facility.code}</td>
                <td>{facility.nameAr}</td>
                <td>{facility.facilityType || '—'}</td>
                <td>
                  <span className={`badge ${facility.isActive ? '' : 'inactive'}`}>
                    {facility.isActive ? 'نشط' : 'غير نشط'}
                  </span>
                </td>
                {canViewFacilityWorkspace && (
                  <td>
                    <Link className="secondary button-link" to={`/workspaces/facilities/${facility.id}`}>مركز القرار</Link>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}
