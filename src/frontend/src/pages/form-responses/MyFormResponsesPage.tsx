import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { useState } from 'react'
import { api } from '../../api/client'
import { FormAssignmentWorkStatusLabelsAr } from './responseLabels'

const tabs = [
  { id: 'current', label: 'الحالية' },
  { id: 'upcoming', label: 'القادمة' },
  { id: 'overdue', label: 'المتأخرة' },
  { id: 'returned', label: 'المعادة' },
  { id: 'submitted', label: 'المرسلة' },
  { id: 'completed', label: 'المكتملة' },
] as const

export function MyFormResponsesPage() {
  const [tab, setTab] = useState<(typeof tabs)[number]['id']>('current')
  const query = useQuery({
    queryKey: ['form-response-workspace', tab],
    queryFn: () => api.formResponses.workspace({ workStatus: tab, page: 1, pageSize: 50 }),
  })

  return (
    <div className="page" dir="rtl">
      <header className="page-header">
        <h1>استحقاقاتي من النماذج</h1>
        <p className="muted">تعبئة الردود ضمن النطاق الزمني والموقعي المسموح.</p>
      </header>

      <div className="tabs" role="tablist" aria-label="تصفية الاستحقاقات">
        {tabs.map((t) => (
          <button
            key={t.id}
            type="button"
            role="tab"
            aria-selected={tab === t.id}
            className={tab === t.id ? 'active' : undefined}
            onClick={() => setTab(t.id)}
          >
            {t.label}
          </button>
        ))}
      </div>

      {query.isLoading && <p>جاري التحميل…</p>}
      {query.isError && <p className="error" role="alert">تعذر تحميل الاستحقاقات.</p>}
      {query.data?.items.length === 0 && <p className="muted">لا توجد استحقاقات في هذا التبويب.</p>}

      {(query.data?.items.length ?? 0) > 0 && (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>الحملة</th>
                <th>الموقع</th>
                <th>الدورة</th>
                <th>الفتح</th>
                <th>الموعد</th>
                <th>الحالة</th>
                <th>آخر حفظ</th>
                <th>إجراء</th>
              </tr>
            </thead>
            <tbody>
              {query.data?.items.map((item) => (
                <tr key={item.assignmentId}>
                  <td>{item.campaignNameAr}</td>
                  <td>{item.facilityNameAr}</td>
                  <td>{item.occurrenceKey}</td>
                  <td>{new Date(item.openAtUtc).toLocaleString('ar-SA')}</td>
                  <td>{new Date(item.effectiveDueAtUtc).toLocaleString('ar-SA')}</td>
                  <td>{FormAssignmentWorkStatusLabelsAr[item.workStatus] ?? item.workStatus}</td>
                  <td>{item.lastSavedAtUtc ? new Date(item.lastSavedAtUtc).toLocaleString('ar-SA') : '—'}</td>
                  <td>
                    <Link to={`/form-assignments/${item.assignmentId}/respond`}>فتح</Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
