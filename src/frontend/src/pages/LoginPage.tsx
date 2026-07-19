import { useState } from 'react'
import { Navigate } from 'react-router-dom'
import { getAuthMode, getTestSubject } from '../api/client'
import { useAuth } from '../auth/AuthProvider'

export function LoginPage() {
  const { isAuthenticated, loginTest, loading, error } = useAuth()
  const [subject, setSubject] = useState(getTestSubject() || 'dev-admin')
  const mode = getAuthMode()

  if (isAuthenticated) return <Navigate to="/regions" replace />

  return (
    <div className="login-page">
      <div className="panel login-card">
        <h1 className="page-title">بصيرة</h1>
        <p className="muted">منصة دعم اتخاذ القرار والإشراف التشغيلي</p>
        {mode === 'test' ? (
          <>
            <p className="muted">وضع التطوير يستخدم TestAuth. أدخل معرف المستخدم التجريبي بعد منحه دورًا ونطاقًا من API.</p>
            <div className="toolbar">
              <input
                aria-label="معرف المستخدم التجريبي"
                value={subject}
                onChange={(e) => setSubject(e.target.value)}
                placeholder="مثال: dev-admin"
              />
              <button disabled={loading || !subject.trim()} onClick={() => void loginTest(subject.trim())}>
                دخول
              </button>
            </div>
          </>
        ) : (
          <p className="muted">سجّل الدخول عبر Microsoft Entra ID من إعدادات البيئة.</p>
        )}
        {error && <div className="error" role="alert">{error}</div>}
      </div>
    </div>
  )
}
