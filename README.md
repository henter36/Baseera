# Baseera — منصة دعم اتخاذ القرار والإشراف التشغيلي

## المرحلة الحالية

| مرحلة | الحالة |
|--------|--------|
| **Phase A** — التأسيس | مكتملة ومقبولة |
| **Phase A.1** — تحصين الأمن والتفويض | مكتملة ومقبولة ومُدمجة في `main` — [`docs/phase-a1-completion-report.md`](docs/phase-a1-completion-report.md) |
| **Phase B.1** — نواة الملاحظات التشغيلية والتكليفات | مكتملة ومقبولة ومُدمجة في `main` — [`docs/phase-b1-completion-report.md`](docs/phase-b1-completion-report.md) |
| **Phase B.2.1** — نواة الإجراءات التصحيحية | مكتملة ومقبولة ومُدمجة في `main` — [`docs/phase-b2-corrective-actions-completion-report.md`](docs/phase-b2-corrective-actions-completion-report.md) |
| **Phase B.2.2** — نواة التصعيد والإشعارات الداخلية | مكتملة ومقبولة ومُدمجة في `main` — [`docs/phase-b22-escalations-notifications-completion-report.md`](docs/phase-b22-escalations-notifications-completion-report.md) |
| **Phase B.2.3.1** — أنواع الملاحظات والصلاحيات الفعلية وتثبيت الإدخال | مكتملة ومقبولة ومُدمجة في `main` — [`docs/phase-b231-note-type-completion-report.md`](docs/phase-b231-note-type-completion-report.md) |
| **Phase B.2.3.2** — توجيه الملاحظات والتكليف التلقائي | مكتملة ومقبولة ومُدمجة في `main` — [`docs/phase-b232-note-routing-completion-report.md`](docs/phase-b232-note-routing-completion-report.md) |
| **Phase B.3.1** — لوحة المتابعة التشغيلية | المرحلة الحالية — [`docs/phase-b31-dashboard-completion-report.md`](docs/phase-b31-dashboard-completion-report.md) |
| **Phase D.0** — إطار مساحات العمل | مكتملة ومقبولة ومُدمجة في `main` — [`docs/phase-d0-workspace-framework-completion-report.md`](docs/phase-d0-workspace-framework-completion-report.md) |
| **Phase D.1** — مركز قرار السجن MVP | مكتملة ومقبولة ومُدمجة في `main` — [`docs/phase-d1-facility-workspace-completion-report.md`](docs/phase-d1-facility-workspace-completion-report.md) |
| **Phase D.2** — إعادة تصميم مركز قيادة السجن | قيد التنفيذ — [`docs/phase-d2-facility-command-center-completion-report.md`](docs/phase-d2-facility-command-center-completion-report.md) |

## المتطلبات

- .NET 10 SDK
- Node.js 22+
- SQL Server (للتطوير المحلي والاختبارات) — أي منفذ/مضيف متاح لديك؛ لا يُفترض حصر المنفذ على 1433

## التشغيل

انسخ إعداد التطوير المحلي (لا يُرفع إلى Git):

```bash
cp src/backend/Baseera.Api/appsettings.example.json src/backend/Baseera.Api/appsettings.Development.json
```

اضبط سلسلة الاتصال عبر متغير بيئة (لا تضع كلمات مرور في Git أو في أوامر تُنسخ إلى التوثيق):

```bash
export BASEERA_CONNECTION='Server=<host>,<port>;Database=Baseera;User Id=<user>;Password=<from-secret-store>;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=true'
```

```bash
# API (Development يسمح بـ TestAuth + Demo Seed عبر appsettings.Development فقط)
cd src/backend
dotnet ef database update --project Baseera.Infrastructure --startup-project Baseera.Api
dotnet run --project Baseera.Api

# Frontend (تطوير)
cd src/frontend
npm ci --ignore-scripts
npm run dev
```

في وضع التطوير مع `VITE_AUTH_MODE=test` يمكن الدخول بمستخدم مُسبق التجهيز بعد تفعيل Seed في Development فقط.

## الاختبارات

```bash
# Unit
dotnet test src/backend/tests/Baseera.UnitTests

# Integration — يتطلب متغير بيئة؛ Fixture ينشئ قاعدة اختبار فريدة
export BASEERA_TEST_CONNECTION='Server=<host>,<port>;User Id=<user>;Password=<from-secret-store>;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=true'
dotnet test src/backend/tests/Baseera.IntegrationTests

cd src/frontend
npm ci --ignore-scripts
npm run typecheck
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

## مساحات العمل

- `facility-operations`: مركز قرار السجن ضمن Phase D.1/D.2، متاح عبر `/workspaces/facilities/:facilityId` ويستخدم Workspace Framework وبيانات حقيقية فقط. D.2 يحول التجربة إلى مركز قيادة مع Context Panel بدل الانتقال الافتراضي بين صفحات منفصلة.
- `reference`: مساحة مرجعية للتطوير من Phase D.0، مفعّلة حسب feature flag.
