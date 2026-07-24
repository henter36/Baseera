import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { api, ApiError } from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'

export function NoteRoutingEffectivenessPage() {
  const canView = usePermission('Notes.ViewRoutingDiagnostics')
  const query = useQuery({
    queryKey: ['note-routing-effectiveness'],
    queryFn: () => api.noteRoutingRules.effectiveness(),
    enabled: canView,
  })

  if (!canView) return <div className="error" role="alert">ليست لديك صلاحية عرض مؤشرات التوجيه.</div>

  const error = query.error instanceof ApiError ? query.error.message : 'تعذر تحميل مؤشرات التوجيه.'
  const data = query.data

  return (
    <div className="panel">
      <div className="page-header">
        <div>
          <h1 className="page-title">فاعلية التوجيه</h1>
          <p className="muted">مؤشرات تشغيلية محدودة لآخر فترة مسموحة دون تصدير أو لوحة قيادية.</p>
        </div>
        <Link className="secondary" to="/settings/note-routing">قواعد التوجيه</Link>
      </div>

      {query.isLoading && <div className="loading">جاري التحميل…</div>}
      {query.isError && (
        <div className="error" role="alert">
          <span>{error}</span>
          <button type="button" className="secondary" onClick={() => query.refetch()}>إعادة المحاولة</button>
        </div>
      )}
      {data && (
        <div className="cards-grid">
          <div className="metric-card"><strong>{data.totalAttempts}</strong><span>إجمالي المحاولات</span></div>
          <div className="metric-card"><strong>{Math.round(data.autoAssignmentSuccessRate * 100)}%</strong><span>نجاح التكليف التلقائي</span></div>
          <div className="metric-card"><strong>{data.assignedToDepartment}</strong><span>تكليف إدارة</span></div>
          <div className="metric-card"><strong>{data.assignedToUser}</strong><span>تكليف مستخدم</span></div>
          <div className="metric-card"><strong>{data.noMatchingRule}</strong><span>لا توجد قاعدة</span></div>
          <div className="metric-card"><strong>{data.noEligibleUser}</strong><span>لا يوجد مستخدم مؤهل</span></div>
          <div className="metric-card"><strong>{data.invalidTarget}</strong><span>هدف غير صالح</span></div>
          <div className="metric-card"><strong>{data.manualOverride}</strong><span>تجاوز يدوي</span></div>
          <div className="metric-card"><strong>{data.requiresRoutingCount}</strong><span>تتطلب توجيهًا</span></div>
        </div>
      )}
    </div>
  )
}
