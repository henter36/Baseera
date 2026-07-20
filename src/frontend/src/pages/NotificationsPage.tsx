import { useCallback, useEffect, useState } from 'react'
import { ApiError, api } from '../api/client'
import type { Notification } from '../api/client'

const statusLabel = ['غير مقروء', 'مقروء', 'مؤرشف']

export function NotificationsPage() {
  const [status, setStatus] = useState<number | undefined>(undefined)
  const [items, setItems] = useState<Notification[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [version, setVersion] = useState(0)
  const [pendingAction, setPendingAction] = useState('')

  const reload = useCallback(() => setVersion((current) => current + 1), [])

  useEffect(() => {
    let active = true
    const load = async () => {
      setLoading(true)
      setError('')
      try {
        const result = await api.notifications.list({ status, pageSize: 20 })
        if (active) setItems(result.items)
      } catch (err) {
        if (active) setError(err instanceof ApiError ? err.message : 'تعذر تحميل الإشعارات.')
      } finally {
        if (active) setLoading(false)
      }
    }
    void load()
    return () => {
      active = false
    }
  }, [status, version])

  const runMutation = async (actionKey: string, operation: () => Promise<void>) => {
    if (pendingAction) return
    setPendingAction(actionKey)
    setError('')
    try {
      await operation()
      window.dispatchEvent(new Event('baseera:notifications-changed'))
      reload()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'تعذر تنفيذ العملية.')
    } finally {
      setPendingAction('')
    }
  }

  const markRead = async (item: Notification) => {
    await runMutation(`read-${item.id}`, () => api.notifications.markRead(item.id, { rowVersion: item.rowVersion }).then(() => undefined))
  }

  const archive = async (item: Notification) => {
    await runMutation(`archive-${item.id}`, () => api.notifications.archive(item.id, { rowVersion: item.rowVersion }).then(() => undefined))
  }

  const markAll = async () => {
    await runMutation('read-all', () => api.notifications.markAllRead().then(() => undefined))
  }

  return (
    <section className="panel" dir="rtl">
      <div className="page-header">
        <div>
          <h1>الإشعارات</h1>
          <p>صندوق الوارد الداخلي الخاص بالمستخدم الحالي.</p>
        </div>
        <button type="button" onClick={markAll} disabled={Boolean(pendingAction)}>تعليم الكل كمقروء</button>
      </div>

      <div className="filters">
        <select value={status ?? ''} onChange={(event) => setStatus(event.target.value === '' ? undefined : Number(event.target.value))}>
          <option value="">النشطة</option>
          <option value={0}>غير مقروء</option>
          <option value={1}>مقروء</option>
          <option value={2}>مؤرشف</option>
        </select>
        <button type="button" className="secondary" onClick={reload}>إعادة المحاولة</button>
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
                  {item.status === 0 && <button type="button" className="secondary" disabled={Boolean(pendingAction)} onClick={() => markRead(item)}>مقروء</button>}
                  {item.status !== 2 && <button type="button" className="secondary" disabled={Boolean(pendingAction)} onClick={() => archive(item)}>أرشفة</button>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  )
}
