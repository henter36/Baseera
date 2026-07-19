import { PublicClientApplication } from '@azure/msal-browser'
import { MsalProvider } from '@azure/msal-react'
import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api, getAuthMode, setAccessTokenProvider, setTestSubject, type Me } from '../api/client'
import { msalConfig } from './msalConfig'

type AuthContextValue = {
  me: Me | null
  loading: boolean
  error: string | null
  isAuthenticated: boolean
  loginTest: (subject: string) => Promise<void>
  logout: () => void
  hasPermission: (code: string) => boolean
  refresh: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

const msalInstance = getAuthMode() === 'entra' ? new PublicClientApplication(msalConfig) : null

export function AuthProvider({ children }: { children: ReactNode }) {
  const content = <AuthState>{children}</AuthState>
  if (msalInstance) {
    return <MsalProvider instance={msalInstance}>{content}</MsalProvider>
  }
  return content
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
      setError(err instanceof Error ? err.message : 'تعذر تحميل الجلسة')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    if (mode === 'entra' && msalInstance) {
      setAccessTokenProvider(async () => {
        const accounts = msalInstance.getAllAccounts()
        if (!accounts.length) return null
        const result = await msalInstance.acquireTokenSilent({
          account: accounts[0],
          scopes: msalConfig.auth.scopes,
        })
        return result.accessToken
      })
    }
    void refresh()
  }, [mode])

  const value = useMemo<AuthContextValue>(() => ({
    me,
    loading,
    error,
    isAuthenticated: !!me,
    loginTest: async (subject: string) => {
      setTestSubject(subject)
      await refresh()
    },
    logout: () => {
      setMe(null)
      if (mode === 'test') setTestSubject('')
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
