export function validateEntraEnv() {
  const required = [
    'VITE_ENTRA_CLIENT_ID',
    'VITE_ENTRA_TENANT_ID',
    'VITE_ENTRA_API_SCOPE',
  ] as const

  for (const key of required) {
    const value = import.meta.env[key]
    if (!value || String(value).includes('YOUR_')) {
      throw new Error(`إعداد Entra ناقص: ${key}. راجع docs/entra-id-configuration.md`)
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
