import { useState } from 'react'
import { Navigate } from 'react-router-dom'
import { getAuthMode, getTestSubject, isTestAuthAllowed } from '../api/client'
import { useAuth } from '../auth/AuthProvider'

export function LoginPage() {
  const { isAuthenticated, loginTest, loginEntra, loading, error, configError } = useAuth()
  const [subject, setSubject] = useState(getTestSubject())
  const mode = getAuthMode()

  if (isAuthenticated) return <Navigate to="/regions" replace />

  return (
    <div className="login-page">
      <div className="panel login-card">
        <h1 className="page-title">بصيرة</h1>
        <p className="muted">منصة دعم اتخاذ القرار والإشراف التشغيلي</p>
        {configError && <div className="error" role="alert">{configError}</div>}
        {mode === 'entra' && !configError && (
          <div className="toolbar">
            <button disabled={loading} onClick={() => void loginEntra()}>
              تسجيل الدخول عبر Microsoft Entra ID
            </button>
          </div>
        )}
        {isTestAuthAllowed() && (
          <>
            <p className="muted">وضع التطوير (TestAuth) — للتنمية المحلية فقط.</p>
            <div className="toolbar">
              <input
                aria-label="معرف المستخدم التجريبي"
                value={subject}
                onChange={(e) => setSubject(e.target.value)}
                placeholder="معرف مستخدم مُسبق التجهيز"
              />
              <button disabled={loading || !subject.trim()} onClick={() => void loginTest(subject.trim())}>
                دخول تجريبي
              </button>
            </div>
          </>
        )}
        {mode === 'entra' && !isTestAuthAllowed() && !configError && (
          <p className="muted">استخدم زر Entra لتسجيل الدخول. لا يتوفر وضع الاختبار في هذا البناء.</p>
        )}
        {error && <div className="error" role="alert">{error}</div>}
      </div>
    </div>
  )
}
