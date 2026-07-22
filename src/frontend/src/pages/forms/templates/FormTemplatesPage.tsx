import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api, ApiError } from '../../../api/client'
import { usePermission } from '../../../auth/AuthProvider'

export function FormTemplatesPage() {
  const canView = usePermission('Forms.View')
  const canManage = usePermission('Forms.ManageTemplates')
  const [code, setCode] = useState('')
  const [nameAr, setNameAr] = useState('')
  const qc = useQueryClient()

  const listQuery = useQuery({
    queryKey: ['form-templates'],
    queryFn: () => api.formTemplates.list(),
    enabled: canView,
  })

  const createForm = useMutation({
    mutationFn: (templateId: string) =>
      api.formTemplates.createForm(templateId, {
        code: code || `TPL_${Date.now()}`,
        nameAr: nameAr || 'نموذج من قالب',
        description: 'أُنشئ من قالب',
        classification: 1,
        scopeType: 0,
      }),
    onSuccess: (form) => {
      void qc.invalidateQueries({ queryKey: ['forms'] })
      window.location.assign(`/forms/${form.id}/versions`)
    },
  })

  if (!canView) return <div className="error" role="alert">ليست لديك صلاحية عرض القوالب.</div>
  if (listQuery.isLoading) return <div className="loading">جاري التحميل…</div>
  if (listQuery.isError) return <div className="error" role="alert">{(listQuery.error as ApiError).message}</div>

  return (
    <div className="panel" dir="rtl">
      <div className="page-header">
        <h1 className="page-title">قوالب النماذج</h1>
        <Link to="/forms">عودة للنماذج</Link>
      </div>
      {canManage && (
        <div className="form-grid">
          <label className="field">رمز النموذج الجديد<input value={code} onChange={(e) => setCode(e.target.value)} /></label>
          <label className="field">الاسم العربي<input value={nameAr} onChange={(e) => setNameAr(e.target.value)} /></label>
        </div>
      )}
      {(listQuery.data ?? []).length === 0 ? (
        <div className="empty">لا توجد قوالب.</div>
      ) : (
        <ul>
          {(listQuery.data ?? []).map((t) => (
            <li key={t.id}>
              <strong>{t.nameAr}</strong> — {t.category} — {t.fieldCount} حقول
              {canManage && (
                <button type="button" onClick={() => createForm.mutate(t.id)} disabled={createForm.isPending}>
                  إنشاء نموذج
                </button>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
