import { useCallback, useEffect, useState } from 'react'
import { ApiError, api } from '../api/client'
import type { Notification } from '../api/client'

const statusLabel = ['غير مقروء', 'مقروء', 'مؤرشف']

export function NotificationsPage() {
  const [status, setStatus] = useState<number | undefined>(undefined)
  const [items, setItems] = useState<Notification[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const result = await api.notifications.list({ status, pageSize: 20 })
      setItems(result.items)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'تعذر تحميل الإشعارات.')
    } finally {
      setLoading(false)
    }
  }, [status])

  useEffect(() => {
    void load()
  }, [load])

  const markRead = async (item: Notification) => {
    await api.notifications.markRead(item.id, { rowVersion: item.rowVersion })
    await load()
  }

  const archive = async (item: Notification) => {
    await api.notifications.archive(item.id, { rowVersion: item.rowVersion })
    await load()
  }

  const markAll = async () => {
    await api.notifications.markAllRead()
    await load()
  }

  return (
    <section className="panel" dir="rtl">
      <div className="page-header">
        <div>
          <h1>الإشعارات</h1>
          <p>صندوق الوارد الداخلي الخاص بالمستخدم الحالي.</p>
        </div>
        <button onClick={markAll}>تعليم الكل كمقروء</button>
      </div>

      <div className="filters">
        <select value={status ?? ''} onChange={(event) => setStatus(event.target.value === '' ? undefined : Number(event.target.value))}>
          <option value="">النشطة</option>
          <option value={0}>غير مقروء</option>
          <option value={1}>مقروء</option>
          <option value={2}>مؤرشف</option>
        </select>
        <button className="secondary" onClick={load}>إعادة المحاولة</button>
      </div>

      {loading && <div className="loading">جاري تحميل الإشعارات…</div>}
      {error && <div className="error">{error}</div>}
      {!loading && !error && items.length === 0 && <div className="empty">لا توجد إشعارات.</div>}
      {!loading && !error && items.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>العنوان</th>
              <th>الهدف</th>
              <th>الأولوية</th>
              <th>الحالة</th>
              <th>تاريخ الإنشاء</th>
              <th>إجراءات</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={item.id}>
                <td>
                  <strong>{item.titleAr}</strong>
                  <div>{item.messageAr}</div>
                </td>
                <td>{item.targetReferenceNumber}</td>
                <td>{item.priority}</td>
                <td>{statusLabel[item.status] ?? item.status}</td>
                <td>{new Date(item.createdAtUtc).toLocaleString('ar-SA', { timeZone: 'Asia/Riyadh' })}</td>
                <td>
                  {item.status === 0 && <button className="secondary" onClick={() => markRead(item)}>مقروء</button>}
                  {item.status !== 2 && <button className="secondary" onClick={() => archive(item)}>أرشفة</button>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  )
}
