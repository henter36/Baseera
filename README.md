# Baseera — منصة دعم اتخاذ القرار والإشراف التشغيلي

## المرحلة الحالية

- **Phase A** — التأسيس (مكتملة)
- **Phase A.1** — تحصين الأمن والتفويض — انظر [`docs/phase-a1-completion-report.md`](docs/phase-a1-completion-report.md)

لا تبدأ المرحلة B قبل قرار **Phase A Accepted**.

## المتطلبات

- .NET 10 SDK
- Node.js 22+
- SQL Server (للتطوير المحلي والاختبارات)

## التشغيل

انسخ إعداد التطوير المحلي (لا يُرفع إلى Git):

```bash
cp src/backend/Baseera.Api/appsettings.example.json src/backend/Baseera.Api/appsettings.Development.json
# عدّل ConnectionStrings:Baseera عبر متغير/ملف محلي — لا تضع أسرارًا في Git
```

```bash
# API (Development يسمح بـ TestAuth + Demo Seed عبر appsettings.Development فقط)
cd src/backend
export BASEERA_CONNECTION='...'   # من بيئة محلية فقط
dotnet ef database update --project Baseera.Infrastructure --startup-project Baseera.Api
dotnet run --project Baseera.Api

# Frontend (تطوير)
cd src/frontend
npm install
npm run dev
```

في وضع التطوير مع `VITE_AUTH_MODE=test` يمكن الدخول بـ `dev-admin` بعد تفعيل Seed في Development فقط.

## الاختبارات

```bash
# Unit
dotnet test src/backend/tests/Baseera.UnitTests

# Integration — يتطلب متغير بيئة بدون fallback في الكود
export BASEERA_TEST_CONNECTION='Server=...;Database=Baseera_Test;...'
dotnet test src/backend/tests/Baseera.IntegrationTests

cd src/frontend
npm test
npm run check:prod-auth   # يجب أن يفشل إذا VITE_AUTH_MODE=test
npm run build             # إنتاج: Entra إلزامي
```

## المصادقة

| بيئة | الخادم | الواجهة |
|------|--------|---------|
| Development / Testing | `Auth:UseTestAuth=true` مسموح | `VITE_AUTH_MODE=test` في `.env.development` |
| Production / Staging | Fail-Fast إذا TestAuth أو Demo Seed | `VITE_AUTH_MODE=entra` + قيم Entra |

تفاصيل Entra: [`docs/entra-id-configuration.md`](docs/entra-id-configuration.md)

## التوثيق

انظر مجلد [`docs/`](docs/).
