export const msalConfig = {
  auth: {
    clientId: import.meta.env.VITE_ENTRA_CLIENT_ID || 'YOUR_SPA_CLIENT_ID',
    authority: `https://login.microsoftonline.com/${import.meta.env.VITE_ENTRA_TENANT_ID || 'YOUR_TENANT_ID'}`,
    redirectUri: import.meta.env.VITE_ENTRA_REDIRECT_URI || window.location.origin,
    scopes: [import.meta.env.VITE_ENTRA_API_SCOPE || 'api://YOUR_API_CLIENT_ID/.default'],
  },
  cache: {
    cacheLocation: 'sessionStorage' as const,
  },
}
