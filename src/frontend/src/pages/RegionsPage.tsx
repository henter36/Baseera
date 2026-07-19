import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { api } from '../api/client'
import { usePermission } from '../auth/AuthProvider'

export function RegionsPage() {
  const canView = usePermission('Organization.View')
  const [search, setSearch] = useState('')
  const query = useQuery({
    queryKey: ['regions', search],
    queryFn: () => api.regions(search),
    enabled: canView,
  })

  if (!canView) {
    return <div className="error" role="alert">ليست لديك صلاحية عرض المناطق.</div>
  }

  return (
    <div className="panel">
      <h1 className="page-title">المناطق</h1>
      <p className="muted">عرض المناطق ضمن نطاقك التنظيمي من قاعدة البيانات الفعلية.</p>
      <div className="toolbar">
        <input
          aria-label="بحث المناطق"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="بحث بالاسم أو الرمز"
        />
      </div>
      {query.isLoading && <div className="loading">جاري التحميل…</div>}
      {query.isError && <div className="error" role="alert">{(query.error as Error).message}</div>}
      {query.data && query.data.items.length === 0 && <div className="empty">لا توجد مناطق ضمن نطاقك.</div>}
      {query.data && query.data.items.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>الرمز</th>
              <th>الاسم</th>
              <th>الحالة</th>
            </tr>
          </thead>
          <tbody>
            {query.data.items.map((region) => (
              <tr key={region.id}>
                <td>{region.code}</td>
                <td>{region.nameAr}</td>
                <td>
                  <span className={`badge ${region.isActive ? '' : 'inactive'}`}>
                    {region.isActive ? 'نشطة' : 'غير نشطة'}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}
