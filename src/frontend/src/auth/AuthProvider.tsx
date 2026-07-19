import {
  InteractionRequiredAuthError,
  PublicClientApplication,
  type AccountInfo,
  type AuthenticationResult,
} from '@azure/msal-browser'
import { MsalProvider } from '@azure/msal-react'
import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import {
  api,
  ApiError,
  getAuthMode,
  isTestAuthAllowed,
  setAccessTokenProvider,
  setTestSubject,
  type Me,
} from '../api/client'
import { getMsalConfig, validateEntraEnv } from './msalConfig'

type AuthContextValue = {
  me: Me | null
  loading: boolean
  error: string | null
  configError: string | null
  isAuthenticated: boolean
  loginTest: (subject: string) => Promise<void>
  loginEntra: () => Promise<void>
  logout: () => Promise<void>
  hasPermission: (code: string) => boolean
  refresh: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

let msalInstance: PublicClientApplication | null = null
let msalInitError: string | null = null

try {
  if (getAuthMode() === 'entra') {
    validateEntraEnv()
    msalInstance = new PublicClientApplication(getMsalConfig())
  }
} catch (err) {
  msalInitError = err instanceof Error ? err.message : 'فشل إعداد Entra ID'
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const content = <AuthState>{children}</AuthState>
  if (msalInstance) {
    return <MsalProvider instance={msalInstance}>{content}</MsalProvider>
  }
  return content
}

async function acquireToken(account: AccountInfo): Promise<string | null> {
  if (!msalInstance) return null
  const scopes = getMsalConfig().auth.scopes
  try {
    const result = await msalInstance.acquireTokenSilent({ account, scopes })
    return result.accessToken
  } catch (err) {
    if (err instanceof InteractionRequiredAuthError) {
      const result = await msalInstance.acquireTokenPopup({ account, scopes })
      return result.accessToken
    }
    throw err
  }
}

function AuthState({ children }: { children: ReactNode }) {
  const [me, setMe] = useState<Me | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const mode = getAuthMode()

  const refresh = async () => {
    setLoading(true)
    setError(null)
    try {
      const profile = await api.me()
      setMe(profile)
    } catch (err) {
      setMe(null)
      if (err instanceof ApiError && err.status === 401) {
        setError('انتهت الجلسة. سجّل الدخول مجددًا.')
      } else {
        setError(err instanceof Error ? err.message : 'تعذر تحميل الجلسة')
      }
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    let cancelled = false
    ;(async () => {
      if (msalInitError) {
        setError(msalInitError)
        setLoading(false)
        return
      }

      if (mode === 'entra' && msalInstance) {
        await msalInstance.initialize()
        const redirect = await msalInstance.handleRedirectPromise()
        const account = redirect?.account ?? msalInstance.getAllAccounts()[0] ?? null
        setAccessTokenProvider(async () => {
          const active = msalInstance!.getActiveAccount() ?? msalInstance!.getAllAccounts()[0]
          if (!active) return null
          return acquireToken(active)
        })
        if (account) {
          msalInstance.setActiveAccount(account)
          if (!cancelled) await refresh()
          return
        }
      }

      if (isTestAuthAllowed()) {
        if (!cancelled) await refresh()
        return
      }

      if (!cancelled) setLoading(false)
    })().catch((err) => {
      if (!cancelled) {
        setError(err instanceof Error ? err.message : 'فشل تهيئة المصادقة')
        setLoading(false)
      }
    })
    return () => {
      cancelled = true
    }
  }, [mode])

  const value = useMemo<AuthContextValue>(() => ({
    me,
    loading,
    error,
    configError: msalInitError,
    isAuthenticated: !!me,
    loginTest: async (subject: string) => {
      setTestSubject(subject)
      await refresh()
    },
    loginEntra: async () => {
      if (!msalInstance) throw new Error(msalInitError || 'Entra غير مهيأ')
      await msalInstance.initialize()
      const result: AuthenticationResult = await msalInstance.loginPopup({
        scopes: getMsalConfig().auth.scopes,
      })
      msalInstance.setActiveAccount(result.account)
      await refresh()
    },
    logout: async () => {
      setMe(null)
      if (mode === 'entra' && msalInstance) {
        const account = msalInstance.getActiveAccount() ?? msalInstance.getAllAccounts()[0]
        await msalInstance.logoutPopup({ account: account ?? undefined })
      }
      if (isTestAuthAllowed()) setTestSubject('')
    },
    hasPermission: (code: string) => !!me?.permissions.includes(code),
    refresh,
  }), [me, loading, error, mode])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within AuthProvider')
  return ctx
}

export function usePermission(code: string) {
  const { hasPermission } = useAuth()
  return hasPermission(code)
}
