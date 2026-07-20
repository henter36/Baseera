import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { ApiError, api } from '../../api/client'
import type { EscalationPolicy } from '../../api/client'

export function EscalationsSettingsPage() {
  const [items, setItems] = useState<EscalationPolicy[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [runSummary, setRunSummary] = useState('')

  const load = async () => {
    setLoading(true)
    setError('')
    try {
      const result = await api.escalationPolicies.list({ pageSize: 50 })
      setItems(result.items)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'تعذر تحميل سياسات التصعيد.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { void load() }, [])

  const runNow = async () => {
    const result = await api.escalations.run()
    setRunSummary(`تم التشغيل: ${result.occurrencesCreated} حادثة، ${result.notificationsCreated} إشعار.`)
  }

  return (
    <section className="panel" dir="rtl">
      <div className="page-header">
        <div>
          <h1>إعدادات التصعيد</h1>
          <p>سياسات التصعيد الداخلي للملاحظات والإجراءات التصحيحية.</p>
        </div>
        <div>
          <Link className="button" to="/settings/escalations/new">سياسة جديدة</Link>
          <button className="secondary" onClick={runNow}>تشغيل الآن</button>
        </div>
      </div>
      {runSummary && <div className="success">{runSummary}</div>}
      {loading && <div className="loading">جاري التحميل…</div>}
      {error && <div className="error">{error}</div>}
      {!loading && !error && items.length === 0 && <div className="empty">لا توجد سياسات.</div>}
      {!loading && !error && items.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>الرمز</th>
              <th>الاسم</th>
              <th>الهدف</th>
              <th>الحالة</th>
              <th>القواعد</th>
              <th>إجراءات</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={item.id}>
                <td>{item.code}</td>
                <td>{item.nameAr}</td>
                <td>{item.targetType === 0 ? 'ملاحظات' : 'إجراءات'}</td>
                <td>{item.isEnabled ? 'مفعلة' : 'معطلة'}</td>
                <td>{item.ruleCount}</td>
                <td><Link to={`/settings/escalations/${item.id}`}>عرض</Link></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  )
}
