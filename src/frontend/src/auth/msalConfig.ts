const NIL_GUID = /^0{8}-0{4}-0{4}-0{4}-0{12}$/i
const ZEROISH_GUID = /00000000-0000-0000-0000-00000000000[0-9a-f]/i

function isPlaceholder(value: unknown): boolean {
  if (!value) return true
  const text = String(value)
  if (text.includes('YOUR_')) return true
  if (NIL_GUID.test(text)) return true
  if (ZEROISH_GUID.test(text)) return true
  return false
}

export function validateEntraEnv() {
  const required = [
    'VITE_ENTRA_CLIENT_ID',
    'VITE_ENTRA_TENANT_ID',
    'VITE_ENTRA_API_SCOPE',
  ] as const

  for (const key of required) {
    const value = import.meta.env[key]
    if (isPlaceholder(value)) {
      throw new Error(`إعداد Entra ناقص: ${key}. راجع docs/entra-id-configuration.md`)
    }
  }

  const redirect = import.meta.env.VITE_ENTRA_REDIRECT_URI as string | undefined
  if (redirect) {
    if (!/^https:\/\//i.test(redirect)) {
      throw new Error('إعداد Entra غير صالح: VITE_ENTRA_REDIRECT_URI يجب أن يكون HTTPS')
    }
    if (/localhost|127\.0\.0\.1/i.test(redirect)) {
      throw new Error('إعداد Entra غير صالح: لا يُسمح بـ localhost في VITE_ENTRA_REDIRECT_URI للإنتاج')
    }
  }
}

export function getMsalConfig() {
  return {
    auth: {
      clientId: import.meta.env.VITE_ENTRA_CLIENT_ID as string,
      authority: `https://login.microsoftonline.com/${import.meta.env.VITE_ENTRA_TENANT_ID}`,
      redirectUri: (import.meta.env.VITE_ENTRA_REDIRECT_URI as string) || window.location.origin,
      scopes: [import.meta.env.VITE_ENTRA_API_SCOPE as string],
    },
    cache: {
      cacheLocation: 'sessionStorage' as const,
    },
  }
}

// Back-compat export used by older imports
export const msalConfig = {
  get auth() {
    return getMsalConfig().auth
  },
  get cache() {
    return getMsalConfig().cache
  },
}
