import { useCallback, useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ApiError, api } from '../../api/client'
import type { EscalationPolicy, EscalationRule } from '../../api/client'

export function EscalationPolicyDetailPage() {
  const { id } = useParams()
  const [policy, setPolicy] = useState<EscalationPolicy | null>(null)
  const [rules, setRules] = useState<EscalationRule[]>([])
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    if (!id) return
    try {
      setPolicy(await api.escalationPolicies.get(id))
      setRules(await api.escalationPolicies.rules(id))
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'تعذر تحميل السياسة.')
    }
  }, [id])

  useEffect(() => { void load() }, [load])

  const toggle = async () => {
    if (!policy) return
    if (policy.isEnabled) await api.escalationPolicies.deactivate(policy.id, { rowVersion: policy.rowVersion })
    else await api.escalationPolicies.activate(policy.id, { rowVersion: policy.rowVersion })
    await load()
  }

  const addRule = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    await api.escalationPolicies.createRule(id!, {
      level: Number(form.get('level')),
      priority: Number(form.get('priority')),
      triggerType: Number(form.get('triggerType')),
      thresholdDays: Number(form.get('thresholdDays')),
      repeatEveryDays: Number(form.get('repeatEveryDays')) || null,
      maximumOccurrences: Number(form.get('maximumOccurrences')) || null,
      recipientStrategy: Number(form.get('recipientStrategy')),
      recipientRoleCode: String(form.get('recipientRoleCode') ?? '') || null,
      specificRecipientUserId: null,
      titleTemplateAr: String(form.get('titleTemplateAr') ?? '').trim(),
      messageTemplateAr: String(form.get('messageTemplateAr') ?? '').trim(),
    })
    event.currentTarget.reset()
    await load()
  }

  return (
    <section className="panel" dir="rtl">
      {error && <div className="error">{error}</div>}
      {!policy && !error && <div className="loading">جاري التحميل…</div>}
      {policy && (
        <>
          <div className="page-header">
            <div>
              <h1>{policy.nameAr}</h1>
              <p>{policy.code}</p>
            </div>
            <div>
              <Link className="secondary" to={`/settings/escalations/${policy.id}/edit`}>تعديل</Link>
              <button onClick={toggle}>{policy.isEnabled ? 'تعطيل' : 'تفعيل'}</button>
            </div>
          </div>
          <h2>القواعد</h2>
          {rules.length === 0 && <div className="empty">لا توجد قواعد.</div>}
          {rules.length > 0 && (
            <table>
              <thead>
                <tr><th>المستوى</th><th>المشغل</th><th>الحد</th><th>المستلم</th><th>الحالة</th></tr>
              </thead>
              <tbody>
                {rules.map((rule) => (
                  <tr key={rule.id}>
                    <td>{rule.level}</td>
                    <td>{rule.triggerType === 0 ? 'قريب الاستحقاق' : 'متأخر'}</td>
                    <td>{rule.thresholdDays} يوم</td>
                    <td>{rule.recipientRoleCode ?? rule.recipientStrategy}</td>
                    <td>{rule.isEnabled ? 'مفعلة' : 'معطلة'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          <h2>إضافة قاعدة</h2>
          <form className="form-grid" onSubmit={addRule}>
            <input name="level" type="number" placeholder="المستوى" min={1} required />
            <select name="triggerType" defaultValue={0}><option value={0}>قريب الاستحقاق</option><option value={1}>متأخر</option></select>
            <input name="thresholdDays" type="number" min={0} placeholder="الأيام" required />
            <input name="priority" type="number" min={0} placeholder="الأولوية" required />
            <input name="repeatEveryDays" type="number" min={1} placeholder="التكرار بالأيام" />
            <input name="maximumOccurrences" type="number" min={1} placeholder="الحد الأقصى" />
            <select name="recipientStrategy" defaultValue={2}><option value={2}>دور داخل النطاق</option><option value={0}>المكلف الحالي</option><option value={3}>مدير السجن</option><option value={4}>مدير المنطقة</option><option value={5}>تنفيذي رئيسي</option></select>
            <input name="recipientRoleCode" placeholder="RoleCode عند الحاجة" />
            <input name="titleTemplateAr" placeholder="العنوان، مثال: تصعيد {reference}" required />
            <textarea name="messageTemplateAr" placeholder="الرسالة" required />
            <button>إضافة</button>
          </form>
        </>
      )}
    </section>
  )
}
