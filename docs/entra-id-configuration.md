# إعداد Microsoft Entra ID لمنصة Baseera

## الهدف

مصادقة إنتاجية عبر Microsoft Entra ID دون تخزين Tenant/Client Secret في المستودع.

## تسجيل التطبيقات

1. **API App Registration**
   - Expose an API: `api://<API_CLIENT_ID>`
   - App role / scope: `.default` أو scope مخصص للقراءة/الكتابة
   - Audience في `AzureAd:Audience` = `api://<API_CLIENT_ID>`

2. **SPA App Registration**
   - Platform: Single-page application
   - Redirect URI: `https://<frontend-host>/` وللتطوير `http://localhost:5173`
   - API permissions: تفويض لـ API scope أعلاه

## إعدادات الخادم (User Secrets / KeyVault / Env)

```
AzureAd__Instance=https://login.microsoftonline.com/
AzureAd__TenantId=<tenant-guid>
AzureAd__ClientId=<api-client-guid>
AzureAd__Audience=api://<api-client-guid>
Auth__UseTestAuth=false
Seed__DemoOrganization=false
```

التحقق من التوكن يتم عبر `Microsoft.Identity.Web` (Issuer/Tenant/Audience/Signature/Lifetime).

## إعدادات الواجهة

```
VITE_AUTH_MODE=entra
VITE_ENTRA_CLIENT_ID=<spa-client-guid>
VITE_ENTRA_TENANT_ID=<tenant-guid>
VITE_ENTRA_API_SCOPE=api://<api-client-guid>/.default
VITE_ENTRA_REDIRECT_URI=https://<frontend-host>/
```

## سياسة المستخدمين

**Pre-Provisioned Only**: يجب إنشاء المستخدم في Baseera مسبقًا بحالة Active قبل أول دخول. لا يُنشأ حساب تلقائي من Claims.

## ملاحظات أمنية

- لا تضع Client Secret في المستودع (SPA يستخدم PKCE).
- TestAuth ممنوع خارج Development/Testing.
- Access Token يُجلب عبر MSAL (sessionStorage cache) ولا يُخزَّن يدويًا في localStorage.
