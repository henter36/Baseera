import { useMutation, useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { api } from '../api/client'
import { usePermission } from '../auth/AuthProvider'

export function AttachmentsPage() {
  const canUpload = usePermission('Attachments.Upload')
  const facilities = useQuery({
    queryKey: ['facilities-for-attach'],
    queryFn: () => api.facilities(),
    enabled: canUpload,
  })
  const [facilityId, setFacilityId] = useState('')
  const [reason, setReason] = useState('مستند تشغيلي')
  const [file, setFile] = useState<File | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  const upload = useMutation({
    mutationFn: async () => {
      if (!file || !facilityId) throw new Error('اختر ملفًا وسجنًا.')
      return api.uploadAttachment(file, 'Facility', facilityId, reason)
    },
    onSuccess: () => setMessage('تم رفع المرفق وتسجيله في التدقيق.'),
    onError: (err: Error) => setMessage(err.message),
  })

  if (!canUpload) {
    return <div className="error" role="alert">ليست لديك صلاحية رفع المرفقات.</div>
  }

  return (
    <div className="panel">
      <h1 className="page-title">المرفقات</h1>
      <p className="muted">رفع مرفق مرتبط بسجن ضمن نطاقك، مع تحقق النوع والحجم والبصمة.</p>
      <div className="toolbar">
        <select
          aria-label="السجن"
          value={facilityId}
          onChange={(e) => setFacilityId(e.target.value)}
        >
          <option value="">اختر سجنًا</option>
          {facilities.data?.items.map((f) => (
            <option key={f.id} value={f.id}>{f.nameAr}</option>
          ))}
        </select>
        <input
          aria-label="سبب الرفع"
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          placeholder="سبب الرفع"
        />
        <input
          aria-label="ملف المرفق"
          type="file"
          onChange={(e) => setFile(e.target.files?.[0] ?? null)}
        />
        <button disabled={upload.isPending} onClick={() => upload.mutate()}>
          رفع
        </button>
      </div>
      {message && <div className={upload.isError ? 'error' : 'muted'} role="status">{message}</div>}
    </div>
  )
}
