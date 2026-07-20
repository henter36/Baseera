import { useEffect, useState } from 'react'
import { api } from '../../api/client'
import type { EscalationOccurrence } from '../../api/client'

const occurrenceStatus = ['منشأة', 'أُنشئت الإشعارات', 'محجوبة', 'فاشلة']

export function EscalationOccurrencesPage() {
  const [items, setItems] = useState<EscalationOccurrence[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    api.escalations.occurrences({ pageSize: 50 }).then((result) => setItems(result.items)).finally(() => setLoading(false))
  }, [])

  return (
    <section className="panel" dir="rtl">
      <h1>حوادث التصعيد</h1>
      {loading && <div className="loading">جاري التحميل…</div>}
      {!loading && items.length === 0 && <div className="empty">لا توجد حوادث.</div>}
      {!loading && items.length > 0 && (
        <table>
          <thead>
            <tr><th>الهدف</th><th>المستوى</th><th>النوع</th><th>الحالة</th><th>المستلمون</th><th>الاكتشاف</th></tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={item.id}>
                <td>{item.targetReferenceNumber}</td>
                <td>{item.escalationLevel}</td>
                <td>{item.triggerType === 0 ? 'قريب الاستحقاق' : 'متأخر'}</td>
                <td>{occurrenceStatus[item.status] ?? item.status}</td>
                <td>{item.recipientCount}</td>
                <td>{new Date(item.detectedAtUtc).toLocaleString('ar-SA', { timeZone: 'Asia/Riyadh' })}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  )
}
