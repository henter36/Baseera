import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import {
  api,
  ApiError,
  type NoteRoutingRule,
  type NoteRoutingRuleRequest,
} from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'

const PAGE_SIZE = 50

function describeScope(rule: NoteRoutingRule): string {
  if (rule.facilityUnitId) return 'وحدة'
  if (rule.facilityId) return 'موقع'
  if (rule.regionId) return 'منطقة'
  return rule.scopeType === 1 ? 'المقر' : 'عام'
}

function describeTarget(rule: NoteRoutingRule): string {
  if (rule.processingTargetType === 0) return rule.processingDepartmentNameAr || rule.processingDepartmentId || 'إدارة'
  return rule.processingRoleNameAr || rule.processingRoleId || 'دور'
}

function emptyRequest(): NoteRoutingRuleRequest {
  return {
    code: '',
    nameAr: '',
    descriptionAr: '',
    noteTypeId: '',
    scopeType: 0,
    regionId: '',
    facilityId: '',
    facilityUnitId: '',
    priority: 10,
    processingTargetType: 0,
    processingDepartmentId: '',
    processingRoleId: '',
    reviewerRoleId: '',
    defaultDueDays: null,
    autoAssignOnSubmit: true,
    autoReassignOnReopen: false,
    reason: '',
  }
}

function normalizeRequest(form: NoteRoutingRuleRequest): NoteRoutingRuleRequest {
  return {
    ...form,
    code: form.code.trim(),
    nameAr: form.nameAr.trim(),
    descriptionAr: form.descriptionAr?.trim() || null,
    regionId: form.regionId || null,
    facilityId: form.facilityId || null,
    facilityUnitId: form.facilityUnitId || null,
    processingDepartmentId: form.processingTargetType === 0 ? form.processingDepartmentId || null : null,
    processingRoleId: form.processingTargetType === 1 ? form.processingRoleId || null : null,
    reviewerRoleId: form.reviewerRoleId || null,
    defaultDueDays: form.defaultDueDays === null ? null : Number(form.defaultDueDays),
    reason: form.reason.trim(),
  }
}

function validateRequest(form: NoteRoutingRuleRequest): string | null {
  if (!form.code.trim()) return 'رمز القاعدة مطلوب.'
  if (!form.nameAr.trim()) return 'اسم القاعدة مطلوب.'
  if (!form.noteTypeId) return 'نوع الملاحظة مطلوب.'
  if (!form.reason.trim()) return 'سبب التعديل مطلوب.'
  if (form.priority < 0) return 'الأولوية لا تكون سالبة.'
  if (form.defaultDueDays != null && form.defaultDueDays < 0) return 'مدة الاستحقاق لا تكون سالبة.'
  if (form.processingTargetType === 0 && !form.processingDepartmentId) return 'معرف الإدارة مطلوب لهدف الإدارة.'
  if (form.processingTargetType === 1 && !form.processingRoleId) return 'معرف الدور مطلوب لهدف الدور.'
  return null
}

export function NoteRoutingSettingsPage() {
  const canView = usePermission('Notes.ViewRouting')
  const canManage = usePermission('Notes.ManageRoutingRules')
  const canActivate = usePermission('Notes.ActivateRoutingRules')
  const [form, setForm] = useState<NoteRoutingRuleRequest>(emptyRequest)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const queryClient = useQueryClient()

  const rulesQuery = useQuery({
    queryKey: ['note-routing-rules'],
    queryFn: () => api.noteRoutingRules.list({ page: 1, pageSize: PAGE_SIZE }),
    enabled: canView,
  })
  const noteTypesQuery = useQuery({
    queryKey: ['note-routing-note-types'],
    queryFn: () => api.noteTypes(true),
    enabled: canView,
  })

  const createMutation = useMutation({
    mutationFn: (body: NoteRoutingRuleRequest) => api.noteRoutingRules.create(body),
    onSuccess: () => {
      setForm(emptyRequest())
      setMessage('تم إنشاء قاعدة التوجيه.')
      void queryClient.invalidateQueries({ queryKey: ['note-routing-rules'] })
    },
    onError: (err) => setError(err instanceof Error ? err.message : 'تعذر حفظ قاعدة التوجيه.'),
  })

  const toggleMutation = useMutation({
    mutationFn: async (rule: NoteRoutingRule) => {
      const body = { rowVersion: rule.rowVersion, reason: 'تغيير حالة قاعدة التوجيه من واجهة الإدارة' }
      return rule.isActive
        ? api.noteRoutingRules.deactivate(rule.id, body)
        : api.noteRoutingRules.activate(rule.id, body)
    },
    onSuccess: () => {
      setMessage('تم تحديث حالة القاعدة.')
      void queryClient.invalidateQueries({ queryKey: ['note-routing-rules'] })
    },
    onError: (err) => setError(err instanceof Error ? err.message : 'تعذر تحديث حالة القاعدة.'),
  })

  if (!canView) return <div className="error" role="alert">ليست لديك صلاحية عرض قواعد التوجيه.</div>

  const submit = (event: FormEvent) => {
    event.preventDefault()
    setError(null)
    setMessage(null)
    const validation = validateRequest(form)
    if (validation) {
      setError(validation)
      return
    }
    createMutation.mutate(normalizeRequest(form))
  }

  const loadError = rulesQuery.error instanceof ApiError ? rulesQuery.error.message : 'تعذر تحميل قواعد التوجيه.'

  return (
    <div className="panel">
      <div className="page-header">
        <div>
          <h1 className="page-title">قواعد توجيه الملاحظات</h1>
          <p className="muted">إدارة قواعد المطابقة والتكليف التلقائي حسب نوع الملاحظة والنطاق.</p>
        </div>
        <Link className="secondary" to="/settings/note-routing/effectiveness">فاعلية التوجيه</Link>
      </div>

      {message && <div className="success" role="status">{message}</div>}
      {error && <div className="error" role="alert">{error}</div>}

      {canManage && (
        <form className="panel-section" onSubmit={submit}>
          <h2 className="section-title">قاعدة جديدة</h2>
          <div className="form-grid">
            <label>الرمز<input value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} /></label>
            <label>الاسم<input value={form.nameAr} onChange={(e) => setForm({ ...form, nameAr: e.target.value })} /></label>
            <label>نوع الملاحظة
              <select value={form.noteTypeId} onChange={(e) => setForm({ ...form, noteTypeId: e.target.value })}>
                <option value="">اختر النوع</option>
                {noteTypesQuery.data?.map((type) => <option key={type.id} value={type.id}>{type.nameAr}</option>)}
              </select>
            </label>
            <label>نوع النطاق
              <select value={form.scopeType} onChange={(e) => setForm({ ...form, scopeType: Number(e.target.value) })}>
                <option value={0}>عام</option>
                <option value={1}>المقر</option>
                <option value={2}>منطقة</option>
                <option value={3}>موقع</option>
                <option value={4}>وحدة</option>
              </select>
            </label>
            <label>RegionId<input value={form.regionId || ''} onChange={(e) => setForm({ ...form, regionId: e.target.value })} /></label>
            <label>FacilityId<input value={form.facilityId || ''} onChange={(e) => setForm({ ...form, facilityId: e.target.value })} /></label>
            <label>FacilityUnitId<input value={form.facilityUnitId || ''} onChange={(e) => setForm({ ...form, facilityUnitId: e.target.value })} /></label>
            <label>الأولوية<input type="number" value={form.priority} onChange={(e) => setForm({ ...form, priority: Number(e.target.value) })} /></label>
            <label>هدف المعالجة
              <select value={form.processingTargetType} onChange={(e) => setForm({ ...form, processingTargetType: Number(e.target.value) })}>
                <option value={0}>إدارة</option>
                <option value={1}>دور</option>
              </select>
            </label>
            <label>ProcessingDepartmentId<input value={form.processingDepartmentId || ''} onChange={(e) => setForm({ ...form, processingDepartmentId: e.target.value })} /></label>
            <label>ProcessingRoleId<input value={form.processingRoleId || ''} onChange={(e) => setForm({ ...form, processingRoleId: e.target.value })} /></label>
            <label>ReviewerRoleId<input value={form.reviewerRoleId || ''} onChange={(e) => setForm({ ...form, reviewerRoleId: e.target.value })} /></label>
            <label>DefaultDueDays<input type="number" value={form.defaultDueDays ?? ''} onChange={(e) => setForm({ ...form, defaultDueDays: e.target.value ? Number(e.target.value) : null })} /></label>
            <label>السبب<input value={form.reason} onChange={(e) => setForm({ ...form, reason: e.target.value })} /></label>
          </div>
          <div className="toolbar">
            <label className="checkbox-field"><input type="checkbox" checked={form.autoAssignOnSubmit} onChange={(e) => setForm({ ...form, autoAssignOnSubmit: e.target.checked })} /><span>توجيه عند الإرسال</span></label>
            <label className="checkbox-field"><input type="checkbox" checked={form.autoReassignOnReopen} onChange={(e) => setForm({ ...form, autoReassignOnReopen: e.target.checked })} /><span>إعادة التوجيه عند الفتح</span></label>
            <button type="submit" disabled={createMutation.isPending}>حفظ القاعدة</button>
          </div>
        </form>
      )}

      {rulesQuery.isLoading && <div className="loading">جاري التحميل…</div>}
      {rulesQuery.isError && (
        <div className="error" role="alert">
          <span>{loadError}</span>
          <button type="button" className="secondary" onClick={() => rulesQuery.refetch()}>إعادة المحاولة</button>
        </div>
      )}
      {rulesQuery.data?.items.length === 0 && <div className="empty">لا توجد قواعد توجيه.</div>}
      {rulesQuery.data && rulesQuery.data.items.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>الرمز</th>
              <th>الاسم</th>
              <th>النوع</th>
              <th>النطاق</th>
              <th>الأولوية</th>
              <th>الهدف</th>
              <th>الاستحقاق</th>
              <th>الحالة</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {rulesQuery.data.items.map((rule) => (
              <tr key={rule.id}>
                <td>{rule.code}</td>
                <td>{rule.nameAr}</td>
                <td>{rule.noteTypeNameAr || rule.noteTypeId}</td>
                <td>{describeScope(rule)}</td>
                <td>{rule.priority}</td>
                <td>{describeTarget(rule)}</td>
                <td>{rule.defaultDueDays ?? '—'}</td>
                <td><span className="badge" data-tone={rule.isActive ? 'success' : 'neutral'}>{rule.isActive ? 'فعال' : 'غير فعال'}</span></td>
                <td>
                  {canActivate && (
                    <button type="button" className="secondary" disabled={toggleMutation.isPending} onClick={() => toggleMutation.mutate(rule)}>
                      {rule.isActive ? 'تعطيل' : 'تفعيل'}
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}
