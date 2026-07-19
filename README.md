# Baseera — منصة دعم اتخاذ القرار والإشراف التشغيلي

## المرحلة A

تأسيس Modular Monolith: الهيكل التنظيمي، الهوية والصلاحيات والنطاقات، Entra ID (مع TestAuth للتطوير/الاختبار)، AuditLog، المرفقات، واجهة عربية RTL.

## المتطلبات

- .NET 10 SDK
- Node.js 22+
- SQL Server على `localhost:1433` (قاعدة `Baseera`)

## التشغيل

انسخ إعداد التطوير المحلي (لا يُرفع إلى Git):

```bash
cp src/backend/Baseera.Api/appsettings.example.json src/backend/Baseera.Api/appsettings.Development.json
# عدّل كلمة مرور SQL Server في الملف المنسوخ
```

```bash
# API
cd src/backend
dotnet ef database update --project Baseera.Infrastructure --startup-project Baseera.Api
dotnet run --project Baseera.Api

# Frontend
cd src/frontend
npm install
npm run dev
```

افتح `http://localhost:5173` وادخل بـ `dev-admin` (TestAuth) بعد تشغيل الـ API.

## الاختبارات

```bash
cd src/backend
dotnet test

cd src/frontend
npm test
```

## التوثيق

انظر مجلد [`docs/`](docs/).

## المصادقة

- التطوير/الاختبار: `Auth:UseTestAuth=true` وواجهة `VITE_AUTH_MODE=test`
- الإنتاج: عيّن `AzureAd:*` و `Auth:UseTestAuth=false` و `VITE_AUTH_MODE=entra`
