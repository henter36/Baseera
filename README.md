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
| **Phase D.2** — إعادة تصميم مركز قيادة السجن | قيد المراجعة؛ لقطات الشاشة النهائية معلقة — [`docs/phase-d2-facility-command-center-completion-report.md`](docs/phase-d2-facility-command-center-completion-report.md) |
| **Phase D.3** — توسعة مساحة عمليات السجن | مكتملة تقنيًا ضمن النماذج الحالية وقيد القبول النهائي — المجالات التشغيلية غير المتوفرة موثقة كفجوات ومتابعات لاحقة، ولا يُغلق Issue #11 — [`docs/phase-d3-complete-facility-workspace-completion-report.md`](docs/phase-d3-complete-facility-workspace-completion-report.md) |
| **Phase D.4** — إشغال السجن وحركة النزلاء | مكتملة ضمن Issue #124 وتستمر في Issue #11 — [`docs/phase-d4-occupancy-completion-report.md`](docs/phase-d4-occupancy-completion-report.md) |
| **Phase D.5** — مركز جاهزية الموارد الأساسية | مكتملة محليًا كدفعة أولى من Issue #15 وتنتظر مراجعة PR؛ لا تغلق Issue #15 أو Issue #11 — [`docs/phase-d5-resource-completion-report.md`](docs/phase-d5-resource-completion-report.md) |
| **Phase D.5.1** — جاهزية القوى البشرية وتغطية المناوبات | مكتملة ومحصنة على الفرع `phase-d5-1-workforce-hardening` لإغلاق Issue #133 بعد نجاح بوابات PR؛ جزئي من #15 واستمرار #11، بدون أسلحة/Region/HQ/رواتب — [`docs/phase-d5-1-workforce-completion-report.md`](docs/phase-d5-1-workforce-completion-report.md) |
| **Phase D.5.2** — الأسلحة والذخائر والعهد الحساسة | مكتملة ومُدمجة في `main` لإغلاق Issue #140؛ الدفعة الأخيرة محليًا من #15 واستمرار #11 — [`docs/phase-d5-2-sensitive-custody-completion-report.md`](docs/phase-d5-2-sensitive-custody-completion-report.md) |
| **Phase D.6** — سجل المخاطر المؤسسي ومركز معالجة مخاطر السجن | قيد التنفيذ على الفرع `phase-d6-facility-risk-treatment-center`؛ تنفيذ جزئي من Issue #16 (Facility scope فقط) واستمرار #11؛ لا تغلق Issue #16 أو #11 أو #15 — [`docs/phase-d6-risk-completion-report.md`](docs/phase-d6-risk-completion-report.md) |
| **UX Rescue Phase 1A** — أساس مساحة عمل الملاحظات | قيد التنفيذ على الفرع `ux-rescue-phase1a-observation-workspace-foundation`؛ تنفيذ جزئي من Issue #143 واستمرار #11؛ لا تغلق Issue #143 أو #11 أو #18 أو #19 — [`docs/ux-rescue/phase1a-observation-completion-report.md`](docs/ux-rescue/phase1a-observation-completion-report.md) |

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
export ConnectionStrings__Baseera="$BASEERA_CONNECTION"
# Development: isolated local key ring (absolute path required when set)
export DataProtection__KeysPath="$PWD/.local/data-protection-keys"
```

يمكن تمرير سر سلسلة الاتصال عبر متغير البيئة `ConnectionStrings__Baseera` أو عبر `dotnet user-secrets` للتطوير المحلي أو GitHub Actions secrets في CI أو secret store مخصص في الإنتاج. ملف `appsettings.example.json` يترك قيمة `ConnectionStrings:Baseera` فارغة عمدًا حتى لا يحتوي المستودع على credential-like placeholders.

في Production/Staging يجب تعيين مسار مطلق دائم لمفاتيح Data Protection ومشاركته بين كل النسخ:

```bash
export DataProtection__KeysPath="/var/lib/baseera/data-protection-keys"
```

المجلد يجب أن يكون persistent وshared بين الـreplicas، خارج filesystem الحاوية المؤقت، ومحدود الصلاحيات لحساب تشغيل Baseera، ومضمنًا في خطة النسخ الاحتياطي. التفاصيل: [`docs/phase-d5-2-sensitive-data-security.md`](docs/phase-d5-2-sensitive-data-security.md).

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

- `facility-operations`: مركز قرار السجن ضمن Phase D.1/D.2/D.3، متاح عبر `/workspaces/facilities/:facilityId` ويستخدم Workspace Framework وبيانات حقيقية فقط. D.3 توسعة مكتملة لمساحة عمليات السجن ضمن البيانات والنماذج الموجودة حاليًا؛ تعرض المجالات غير المتاحة كفجوات جودة بيانات صريحة بدل بيانات تجريبية، ولا تنفذ كامل محركات الإشغال والموارد والوقوعات والمخاطر والمشاريع والخطط والقرارات. لا تعني هذه المرحلة إغلاق Issue #11؛ تستمر المجالات الناقصة عبر Issues #15 و#16 و#18 و#19 و#124 و#125 و#126 و#127 و#128.
- Phase D.4 يستبدل فجوة الإشغال ضمن مساحة السجن بنموذج حقيقي للطاقة الاستيعابية وSnapshots وحركة النزلاء، مع عدم عرض هوية النزيل في Workspace. تبقى Issue #11 مفتوحة لبقية مجالات السجن، وتقتصر صلة #15 على الطاقة الاستيعابية فقط.
- Phase D.5 يستبدل فجوة الموارد الأساسية بنموذج ResourceAsset حقيقي للمركبات وأجهزة الاتصال والمعدات التشغيلية/الأمنية غير المصنفة كأسلحة والأصول الثابتة، مع status history وplacement وmaintenance وrequirements وresource gaps. لا ينفذ الأسلحة أو العهد الحساسة أو المخزون العام، وتبقى Issues #15 و#11 مفتوحة.
- Phase D.5.1 يضيف مركز جاهزية القوى البشرية وتغطية المناوبات (`WorkforceMember` مستقل عن `User`، أدوار تشغيلية، مؤهلات، تكليفات، احتياج، ورديات، جداول مناوبة، توفر، مواقع حرجة، استيراد محدود، وقسم Workspace `القوى البشرية والتغطية`). يغلق Issue #133 عند نجاح PR، ولا ينفذ الأسلحة أو Region/HQ أو الرواتب، ولا يغلق Issue #15 أو Issue #11.
- Phase D.5.2 يضيف مجالًا مستقلًا للأسلحة والذخائر والعهد الحساسة، مع serial protected/hash، سلسلة عهد append-only، ذخيرة ledger، جرد، فحص، صلاحيات `SensitiveCustody.*`، وقسم Workspace `الأسلحة والعهد الحساسة`. يغلق Issue #140 عند نجاح PR، ويتم تقييم #15 بعد القبول النهائي، وتبقى #11 مفتوحة لمساحات Region/HQ وبقية مركز القرار.
- Phase D.6 يستبدل فجوة المخاطر ("لا يوجد Risk/RiskTreatment engine") بسجل مخاطر مؤسسي حقيقي (`Baseera.Domain.RiskManagement`): مصفوفة تقييم مُصدرة، درجة محسوبة على الخادم، تقييم أصلي/حالي/متبقٍ منفصل، ضوابط منفصلة عن خطط المعالجة، دورة حياة كاملة بفصل مهام (four-eyes)، وقسم Workspace `المخاطر والمعالجات`. تنفيذ جزئي من Issue #16 (Facility scope فقط، دون تجميع Region/HQ)، صلاحيات `Risks.*`، ولا يغلق Issue #16 أو #11 أو #15.
- `reference`: مساحة مرجعية للتطوير من Phase D.0، مفعّلة حسب feature flag.
- UX Rescue Phase 1A يعيد بناء أساس مساحة عمل الملاحظات على `/notes/workspace` (Master-detail حقيقي، فتح ملاحظة من `workspaces/facilities/:facilityId` بوراثة سجن/وحدة آمنة من الخادم، إجراءات Assign/StartWork/RequestVerification/RejectVerification/VerifyClosure/Reopen/Cancel داخل الـWorkspace، تنقّل سابق/تالي، توافق `/notes` و`/notes/:id` القديمة عبر `VITE_OBSERVATION_WORKSPACE_V2`). تنفيذ جزئي من Issue #143 (Vertical Slice أولى، ليست كل عمليات الملاحظات)، ولا يغلق #143 أو #11 أو #18 أو #19 — [`docs/ux-rescue/phase1a-observation-completion-report.md`](docs/ux-rescue/phase1a-observation-completion-report.md).
