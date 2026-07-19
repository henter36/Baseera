import { useQuery } from '@tanstack/react-query'
import { api } from '../api/client'
import { usePermission } from '../auth/AuthProvider'

export function AuditPage() {
  const canView = usePermission('Audit.View')
  const query = useQuery({
    queryKey: ['audit'],
    queryFn: () => api.auditLogs(),
    enabled: canView,
  })

  if (!canView) {
    return <div className="error" role="alert">ليست لديك صلاحية عرض سجل التدقيق.</div>
  }

  return (
    <div className="panel">
      <h1 className="page-title">سجل التدقيق</h1>
      <p className="muted">سجل غير قابل للتعديل من واجهة التطبيق.</p>
      {query.isLoading && <div className="loading">جاري التحميل…</div>}
      {query.isError && <div className="error" role="alert">{(query.error as Error).message}</div>}
      {query.data && query.data.items.length === 0 && <div className="empty">لا توجد أحداث بعد.</div>}
      {query.data && query.data.items.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>الوقت (السعودية)</th>
              <th>المستخدم</th>
              <th>العملية</th>
              <th>الوحدة</th>
              <th>الكيان</th>
              <th>النتيجة</th>
            </tr>
          </thead>
          <tbody>
            {query.data.items.map((item) => (
              <tr key={item.id}>
                <td>{new Date(item.occurredAtSaudi).toLocaleString('ar-SA')}</td>
                <td>{item.userDisplayName || '—'}</td>
                <td>{item.action}</td>
                <td>{item.module}</td>
                <td>{item.entityType} {item.entityId ? `(${item.entityId.slice(0, 8)}…)` : ''}</td>
                <td>{item.outcome}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}
