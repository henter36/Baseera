import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { api } from '../api/client'
import { usePermission } from '../auth/AuthProvider'

export function UsersPage() {
  const canView = usePermission('Users.View')
  const [search, setSearch] = useState('')
  const query = useQuery({
    queryKey: ['users', search],
    queryFn: () => api.users(search),
    enabled: canView,
  })

  if (!canView) {
    return <div className="error" role="alert">ليست لديك صلاحية عرض المستخدمين.</div>
  }

  return (
    <div className="panel">
      <h1 className="page-title">المستخدمون</h1>
      <p className="muted">إدارة الهوية المحلية المرتبطة بـ Entra Object Id.</p>
      <div className="toolbar">
        <input
          aria-label="بحث المستخدمين"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="بحث بالاسم"
        />
      </div>
      {query.isLoading && <div className="loading">جاري التحميل…</div>}
      {query.isError && <div className="error" role="alert">{(query.error as Error).message}</div>}
      {query.data && query.data.items.length === 0 && <div className="empty">لا يوجد مستخدمون.</div>}
      {query.data && query.data.items.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>الاسم</th>
              <th>المعرف الخارجي</th>
              <th>الأدوار</th>
              <th>الحالة</th>
            </tr>
          </thead>
          <tbody>
            {query.data.items.map((user) => (
              <tr key={user.id}>
                <td>{user.displayNameAr}</td>
                <td>{user.externalSubject}</td>
                <td>{user.roles.join(', ') || '—'}</td>
                <td>
                  <span className={`badge ${user.isActive ? '' : 'inactive'}`}>
                    {user.isActive ? 'نشط' : 'موقوف'}
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
