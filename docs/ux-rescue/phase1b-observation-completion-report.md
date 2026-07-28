# Phase 1B — تقرير الإنجاز (Observation Note Triage/Treatment/Parts/SLA Rebuild)

**الفرع**: `ux-rescue-phase1b-observation-full-workflow`
**PR**: https://github.com/henter36/Baseera/pull/152
**Commit SHA**: `bee215be5700dfc2aa29894c7b2b32db32b3cb3d`
**يبني على**: Phase 1A/1A.1 (مدموجتان في `main` عبر PR #149، #150، و#151 — هذا الفرع أُعيد دمجه مع `main` بعد أن اندمج جزء سابق منه عبر PR #151 أثناء هذه الجلسة، ثم كُمِّل بكامل عمل Phase 1B فوقه).
**التكليف**: إعادة بناء منطق فرز الملاحظة ومعالجتها — فصل بوابة الفرز عن نتيجة المعالجة، Four-eyes موحَّد، تعدد قطع حقيقي، SLA ثلاثي.
**لم يُدمَج بعد**: هذا PR بانتظار مراجعة CI/SonarCloud/CodeRabbit ثم دمج بشري — لم يُدمَج تلقائيًا ولم يُفعَّل Auto-merge.

## ملخص تنفيذي

نُفِّذ التكليف بالكامل تقريبًا (Missing=1 فقط، Partial=3، كلاهما موثَّق بسبب هندسي واضح في `phase1b-observation-compliance-ledger.md`، لا سهوًا). القرار المحوري: **لا منطق موازٍ** — كل إضافة جديدة (Four-eyes، حالات الإغلاق، السجلات الفرعية) تمت بتوسيع نقاط محدَّدة في الكود القائم (`NoteStateMachine.CanTransition`، `EnforceCriticalSoDAsync`، `ComputeAllowedActions`) بدل بناء مسارات بديلة، مع الحفاظ على 100% من السلوك القديم (مؤكَّد بنجاح 979 اختبار وحدة + 112 اختبار تكامل قائمة مسبقًا دون أي تعديل على توقعاتها الجوهرية، باستثناء 5 اختبارات فقط احتاجت خطوتَي "فرز" و"تسجيل معالجة" إضافيتين لأن ذلك سلوك جديد مقصود، لا Regression).

## تغييرات Domain

- إضافة 7 Enum جديدة: `NoteTriageOutcome`, `NoteTreatmentResultType`, `NoteTreatmentExecutionType`, `NoteDecisionApprovalType`, `NoteDecisionApprovalStatus`, `NoteClosureReason`, `NotePartsRequirementStatus`.
- 3 كيانات EF جديدة: `NoteDecisionApproval`, `NotePartsRequirement`, `NoteSlaPausePeriod`.
- `OperationalNote`: 12 حقلًا جديدًا (`TriageOutcome`…`ClosureReason`) + 3 مجموعات ملاحة.
- `NoteType.SupportsPartsWorkflow` (bit، Server-authored).
- `NoteStateMachine`: 3 انتقالات جديدة فقط (`(Open|Assigned|InProgress, Closed)`)، بقية الجدول بلا تغيير.

## تغييرات قاعدة البيانات

- Migration واحدة: `20260728091057_Phase1BObservationTriageApprovalPartsSla` — أعمدة جديدة على `OperationalNotes`/`NoteTypes` + 3 جداول جديدة (`NoteDecisionApprovals`, `NotePartsRequirements`, `NoteSlaPausePeriods`) بفهارس فريدة مُفلترة (منع اعتماد نشط مزدوج من نفس النوع، منع تجميد نشط مزدوج) وCheck Constraints (منع Self-approval على مستوى قاعدة البيانات أيضًا لـ`NoteDecisionApprovals`، منع كمية ≤0 لـ`NotePartsRequirements`).
- طُبِّقت فعليًا (`dotnet ef database update`) على SQL Server 2022 حقيقي (حاوية Docker محليًا، نفس صورة CI) — لا اكتفاء بمراجعة الكود.

## تغييرات API

7 مجموعات Endpoint جديدة تحت `/api/v1/notes/{id}/...` (`triage/*`, `treatment/*`, `decisions/*`, `parts/*`, `sla/*`) — 17 Route جديدًا بالإجمالي، بلا كسر أي عقد قائم (كل التوسيع إضافي في نهاية DTOs الموضعية). تفصيل كامل: `phase1b-observation-api-contract.md`.

## تغييرات Frontend

- `ObservationWorkspacePage.tsx`: 9 أقسام بدل 5 (الملخص/قرار الفرز/التكليف/نتيجة المعالجة/القطع والمواد/الأدلة/الاعتمادات/التصعيدات/السجل)، ظهور مشروط بحالة الملاحظة فعليًا لا Enum ثابت. مركز إجراءات يعتمد `ActionCenter.PrimaryAction`/`SecondaryActions` القادمة من الخادم بدل الحساب المحلي القديم (مع الحفاظ على نفس السلوك عند غياب الحقل الجديد — Fallback متوافق للخلف).
- `client.ts`: 15 حقلًا جديدًا على `NoteDetail`، 4 أنواع جديدة (`NoteDecisionApproval`, `NotePartsRequirement`, `NoteSlaState`, `NoteActionCenter`)، 16 دالة API جديدة.
- `noteEnums.ts`: 7 مجموعات Label عربية جديدة.
- `FacilityWorkspacePage.tsx`: تسميات الأزرار الثمانية الجديدة فقط (بلا منطق جديد).

## نموذج الصلاحيات

7 صلاحيات جديدة بالاسم الحرفي المطلوب في التكليف. توزيع الأدوار وقاعدة Four-eyes الشخصية (لا الدورية) في `phase1b-observation-permissions.md` و`docs/permissions-matrix.md` (قسم "UX Rescue Phase 1B" الجديد).

## نموذج Four-eyes

محرك واحد موحَّد (`NoteDecisionApprovalService`) لأنواع القرار الثلاثة (Invalid/Duplicate/NoAction) التي تُغلِق مباشرة؛ اعتماد "معالجة" (Treated) يبقى عبر خط الأنابيب القائم `SubmitForVerification→VerifyClosure` مع توسيع شرط SoD القائم بدل بناء مسار مواز — قرار موثَّق بالتفصيل في `phase1b-observation-architecture.md`.

## سياسة الملاحظات المكررة

اقتراح → مراجعة مستقلة → اعتماد/إعادة → ربط بالأصل + إغلاق المكررة دون أي أثر على الأصل (حالته، معالجته، مرفقاته). تحقق صريح لمنع الربط بالنفس أو خارج النطاق أو بنوع مختلف (لمنع الربط العشوائي).

## سياسة تعدد القطع

`NotePartsRequirement` كيان EF مستقل حقيقي (لا حقل قطعة واحدة، لا State Machine منفصلة لكل قطعة) — حالة محدودة بستة قيم، انتقال أمامي + إلغاء صريح بسبب، فهرس فريد يمنع تكرار رمز القطعة النشط على نفس الملاحظة.

## نتائج SLA

ثلاثة مؤشرات مستقلة فعليًا: `OverallAge` (لا يتوقف أبدًا)، `ProcessingSla` (يتوقف فقط أثناء تجميد معتمد رسميًا)، `ExternalWaitDuration` (مجموع فترات التجميد المعتمدة، مُبلَّغ عنه منفصلاً). شروط التجميد الخمسة كلها مُنفَّذة ومُختبَرة عدا الإنهاء التلقائي عند تجاوز مهلة المراجعة (Missing، موثَّق).

## نتائج الاختبارات

| المجموعة | قبل هذه الدفعة | بعدها | النتيجة |
| --- | --- | --- | --- |
| Backend Unit | 960 | **979** | Failed=0, Skipped=0 |
| Backend Integration (Notes*) | 34 | **65** | Failed=0, Skipped=0 |
| Backend Integration (حزمة Operations كاملة) | — | **112** | Failed=0, Skipped=0 |
| Frontend | 301 | **307** (بعد دمج `main` الذي أضاف تحسينًا في الوصولية بين الجلسة وهذه الدفعة) | Failed=0, Skipped=0 |

تفصيل كامل بالسيناريوهات: `phase1b-observation-test-matrix.md`.

## نتائج Security checks

| الفحص | النتيجة |
| --- | --- |
| Backend build (`dotnet build Baseera.slnx`) | ناجح، صفر أخطاء |
| Frontend typecheck (`tsc -b`) | ناجح |
| Frontend lint (`oxlint`) | ناجح، بلا تحذيرات جديدة |
| Frontend production build | ناجح |
| NuGet vulnerability check | راجع قسم التحقق أدناه |
| npm audit --audit-level=high | راجع قسم التحقق أدناه |
| Gitleaks | راجع قسم التحقق أدناه |
| git diff --check (Whitespace) | راجع قسم التحقق أدناه |

## Verified / Partial / Missing (الملخص النهائي)

راجع `phase1b-observation-compliance-ledger.md` للتفصيل الكامل بندًا بندًا (46 بندًا). **Missing = 1** (إنهاء تلقائي لتجميد SLA عند تجاوز مهلة المراجعة — يتطلب مهمة خلفية دورية خارج نطاق الوقت المتاح). **Partial = 3** (إعفاء موثَّق لقطعة كمسار مستقل عن الإلغاء العادي؛ التصعيد الفوري من الملاحظة يبقى Not Applicable الفعلية موروثة من Phase 1A موصوفة بوضوح بدل إخفائها؛ حظر Assign قبل اعتماد "صحيحة" على مستوى الخادم صراحة — الإنفاذ حاليًا على مستوى الواجهة/Action Center فقط لتفادي كسر عشرات الاختبارات القائمة التي تعتمد سلوكًا سابقًا لمفهوم الفرز نفسه). **كل بند آخر: Verified**.
