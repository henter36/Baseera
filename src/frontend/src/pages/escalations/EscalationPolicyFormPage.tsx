import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { ApiError, api } from '../../api/client'
import type { EscalationPolicy } from '../../api/client'

export function EscalationPolicyFormPage({ mode }: Readonly<{ mode: 'create' | 'edit' }>) {
  const { id } = useParams()
  const navigate = useNavigate()
  const [policy, setPolicy] = useState<EscalationPolicy | null>(null)
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    if (mode === 'edit' && id) {
      api.escalationPolicies.get(id).then(setPolicy).catch(() => setError('تعذر تحميل السياسة.'))
    }
  }, [id, mode])

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setSaving(true)
    setError('')
    const form = new FormData(event.currentTarget)
    const textValue = (name: string) => {
      const value = form.get(name)
      return typeof value === 'string' ? value.trim() : ''
    }
    const body = {
      code: textValue('code'),
      nameAr: textValue('nameAr'),
      description: textValue('description') || null,
      targetType: Number(form.get('targetType')),
      scopeType: Number(form.get('scopeType')),
      regionId: null,
      facilityId: null,
      facilityUnitId: null,
    }
    try {
      if (!body.nameAr || (mode === 'create' && !body.code)) {
        setError('الرمز والاسم مطلوبان.')
        return
      }
      const saved = mode === 'create'
        ? await api.escalationPolicies.create(body)
        : await api.escalationPolicies.update(id!, {
            nameAr: body.nameAr,
            description: body.description,
            scopeType: body.scopeType,
            regionId: body.regionId,
            facilityId: body.facilityId,
            facilityUnitId: body.facilityUnitId,
            rowVersion: policy!.rowVersion,
          })
      navigate(`/settings/escalations/${saved.id}`)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'تعذر حفظ السياسة.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="panel" dir="rtl">
      <h1>{mode === 'create' ? 'سياسة تصعيد جديدة' : 'تعديل سياسة التصعيد'}</h1>
      {error && <div className="error">{error}</div>}
      <form onSubmit={submit} className="form-grid">
        <label>
          <span>الرمز</span>
          <input name="code" defaultValue={policy?.code ?? ''} disabled={mode === 'edit'} />
        </label>
        <label>
          <span>الاسم</span>
          <input name="nameAr" defaultValue={policy?.nameAr ?? ''} />
        </label>
        <label>
          <span>نوع الهدف</span>
          <select name="targetType" defaultValue={policy?.targetType ?? 0} disabled={mode === 'edit'}>
            <option value={0}>ملاحظة تشغيلية</option>
            <option value={1}>إجراء تصحيحي</option>
          </select>
        </label>
        <label>
          <span>النطاق</span>
          <select name="scopeType" defaultValue={policy?.scopeType ?? 0}>
            <option value={0}>وطني</option>
            <option value={1}>المستوى الرئيسي</option>
          </select>
        </label>
        <label className="wide">
          <span>الوصف</span>
          <textarea name="description" defaultValue={policy?.description ?? ''} />
        </label>
        <button type="submit" disabled={saving}>{saving ? 'جاري الحفظ…' : 'حفظ'}</button>
      </form>
    </section>
  )
}
