# Phase 1B — مصفوفة الاختبارات

## Backend — Unit Tests

**ملف جديد**: `src/backend/tests/Baseera.UnitTests/NotePhase1BServicesTests.cs` (17 اختبارًا، InMemory EF، بلا SQL Server):

- `DecideValidAsync` يجعل الملاحظة مؤهَّلة دون إغلاقها.
- `ProposeInvalidAsync` يتطلب الصلاحية.
- مقترح "غير صحيحة" لا يستطيع اعتماد اقتراحه (409).
- مراجع مستقل يعتمد "غير صحيحة" فيُغلَق مع `ClosureReason=Invalid`.
- إعادة قرار "غير صحيحة" تتطلب سببًا وتُعيد `TriageOutcome` إلى `null`.
- منع ربط "مكررة" بالنفس.
- منع ربط "مكررة" خارج النطاق (سجن مختلف).
- اعتماد "مكررة" يربط الملاحظة بالأصل ولا يغيّر حالة الأصل.
- مقترح "لا تتطلب إجراء" لا يستطيع اعتماد اقتراحه.
- منع تسجيل نتيجة معالجة قبل اعتماد "صحيحة".
- تعدد القطع + حساب التقدم + الإلغاء بسبب.
- منع إرسال المعالجة للتحقق قبل اكتمال القطع الفعالة.
- منع تجميد SLA بلا رقم طلب/جهة توريد موثَّقة على عنصر واحد على الأقل.
- منع Self-approval لتجميد SLA + التحقق من بدء التجميد الفعلي عند الاعتماد.
- استمرار `OverallAge` أثناء توقف `ProcessingSla` (اختبار حسابي نقي على `NoteSlaService.Compute`).
- `ComputeAllowedActions`: ظهور Token الفرز فقط قبل القرار.
- `ComputeAllowedActions`: ظهور Token المعالجة فقط بعد اعتماد "صحيحة".

**تعديلات على ملفات قائمة**: إضافة أعضاء الواجهة الجديدة (`NoteDecisionApprovals`/`NotePartsRequirements`/`NoteSlaPausePeriods`) إلى Stub`IBaseeraDbContext` في `NoteWorkflowTests.cs`/`FormCampaignCoreTests.cs`؛ إضافة معامل `IAttachmentAppService` الجديد لكل استدعاء `new NoteWorkflowService(...)` (4 مواقع)؛ توسيع `NoteDetailDto` الموضعي في `NoteWorkspaceQueryServiceTests.cs`/`NoteWorkflowTests.cs` بالحقول الـ15 الجديدة (`null` افتراضيًا)؛ نقل `(InProgress, Closed)` من قائمة الانتقالات المرفوضة إلى المسموحة في `NoteStateMachineTests.cs`، وإضافة `(Open, Closed)`/`(Assigned, Closed)` صراحة.

**إجمالي Unit Tests بعد هذه الدفعة: 979 (كان 960 مطلع الدفعة) — Failed=0, Skipped=0.**

## Backend — Integration Tests (SQL Server حقيقي، حاوية Docker محلية مطابقة لصورة CI)

**ملف جديد**: `src/backend/tests/Baseera.IntegrationTests/NotePhase1BIntegrationTests.cs` (11 اختبارًا):

1. فرز صحيحة → معالجة مباشرة → إرسال للتحقق → اعتماد مستقل → إغلاق (`ClosureReason=Treated`).
2. اقتراح غير صحيحة → مراجع مستقل يعتمد → إغلاق؛ ومنع المقترح من الاعتماد (409) في نفس السيناريو.
3. إعادة قرار غير صحيحة (400 بلا سبب، ثم إعادة ناجحة بسبب) + بقاء الملاحظة ظاهرة في البحث.
4. اقتراح مكررة وربطها بأصل ثم اعتماد؛ التحقق من عدم تغيّر حالة الأصل.
5. اقتراح لا تتطلب إجراء + منع المقترح من الاعتماد + اعتماد مستقل ناجح.
6. معالجة تحتاج قطعتين: حظر الإرسال للتحقق حتى تركيب/إلغاء الكل، ثم سماحه.
7. طلب تجميد SLA وتوثيق رقم الطلب والمورد، ثم اعتماد مستقل يبدأ التجميد فعليًا (`IsProcessingSlaPaused=true`).
8. اعتماد قرار خارج النطاق التنظيمي → 404.
9. اعتماد قرار بـRowVersion غير صحيح → 409.
10. اعتماد قرار بلا صلاحية → 403 (بمستخدم منفصل تمامًا عن ثلاثي الاختبار لتفادي تلوّث صلاحيات الدور المشترك — راجع الملاحظة أدناه).
11. اكتمال AuditLog (`NoteInvalidProposed`/`NoteInvalidApproved`) و`NoteStatusHistory` (`ToStatus=Closed`) لمسار الاعتماد.

**ملاحظة منهجية مهمة اكتُشفت أثناء الكتابة**: `SeedUserWithPermissionsAsync` (مساعد اختبار قائم) يمنح الصلاحيات الإضافية على مستوى **الدور المشترك** (`RolePermissions`)، لا المستخدم، وهذا التأثير **يتراكم عبر كامل تشغيلة الاختبارات** لأن قاعدة البيانات لا تُعاد تهيئتها بين الاختبارات. اختبار "403 لعدم امتلاك صلاحية الاعتماد" استُبدِل مستخدمه من دور `FacilityCoordinator` (يتلوَّث لاحقًا بصلاحيات الاعتماد التي يمنحها ثلاثي الاختبار لدوره) إلى `RegionalCoordinator` (يملك Propose* بتصميم الدور الأساسي، ولا يُوسَّع أبدًا في هذا الملف) — لضمان اختبار مستقر لا يعتمد على ترتيب تشغيل الاختبارات. موثَّق كتعليق مباشر في الكود.

**تعديلات على 3 ملفات تكامل قائمة** (`NotesCoreIntegrationTests.cs` [3 اختبارات]، `NoteWorkspaceIntegrationTests.cs` [2 اختبار]): إضافة استدعاء `DecideTriageValidAsync` بعد `Submit` واستدعاء `RecordDirectTreatmentAsync` بعد `StartWork`، قبل `SubmitForVerification` — لأن الحارس الجديد (`EnsureTreatmentReadyForVerificationAsync`) يرفض الإرسال للتحقق بلا "صحيحة" + نتيجة معالجة مسجَّلة، وهذا سلوك مقصود جديد وليس Regression. اختباران إضافيان (`NoteSeverity.Critical`) احتاجا أيضًا `UploadEvidenceAsync` (سياسة الأدلة الإلزامية للخطورة العالية/الحرجة).

**تشغيل كل اختبارات Notes* (شامل الملف الجديد): 65/65 ناجح.**
**تشغيل كل حزمة Operations (Notes + CorrectiveActions + Escalations + Dashboard + Workspace + Occupancy + Resources): 112/112 ناجح، Failed=0, Skipped=0** (~7 دقائق، SQL Server 2022 حقيقي عبر Docker محليًا بنفس صورة CI).

**تحديث CI**: أُضيف `NoteWorkspaceIntegrationTests` (كان مفقودًا من فلتر `operations` قبل هذه الدفعة — فجوة سابقة غير متعلقة بهذه الدفعة، أُصلِحت لأنها في نفس الملف) و`NotePhase1BIntegrationTests` إلى `.github/workflows/ci.yml` سطر `suite: operations`.

## Frontend — Vitest

**`ObservationWorkspacePage.test.tsx`**: أُعيدت كتابته بالكامل (18 اختبارًا، كان 11) ليعكس القسمين الجديدين وحقول العقد الجديدة:

- الأقسام التسعة الصحيحة تظهر لملاحظة غير مفروزة (الملخص/قرار الفرز/التكليف/الأدلة/الاعتمادات/التصعيدات/السجل)، وغياب "نتيجة المعالجة"/"القطع والمواد" قبل التأهل.
- بوابة الفرز تعرض فقط صحيحة/غير صحيحة/مكررة.
- قسم نتيجة المعالجة يظهر فقط بعد `TriageOutcome=Valid`، ولا يحتوي أزرار غير صحيحة/مكررة/إسناد/تصعيد.
- قسم القطع يظهر فقط عند `TreatmentExecutionType=RequiresParts` (Server-authored، لا مقارنة نصية في الاختبار نفسه أيضًا).
- بانتظار اعتماد قرار: لا يظهر زر "اعتماد" حين `canSelfApprove=false`.
- مؤشرات SLA الثلاثة تظهر في رأس التفاصيل.
- كل الاختبارات الـ11 القديمة (تنقّل، Back/Forward، رفع مرفق، VERIFY_CLOSURE، الفلاتر) بلا تغيير في التوقّع، فقط تحديث الـFixture بالحقول الجديدة (قيم محايدة تُحافظ على نفس السلوك القديم تمامًا).

**`FacilityWorkspacePage.tsx`**: إضافة تسميات الأزرار الثمانية الجديدة إلى `noteActionLabel` (مصفوفة عرض فقط، لا منطق).

**إجمالي Frontend: 306 اختبارًا ناجحًا عبر 57 ملفًا — Failed=0, Skipped=0.**

## بوابات الجودة الإضافية (مؤكَّدة فعليًا)

- `npm run typecheck` (tsc -b) — نظيف.
- `npm run lint` (oxlint) — بلا تحذيرات/أخطاء جديدة في الملفات المعدَّلة.
- بناء الإنتاج (`tsc -b && vite build`, بمتغيرات Entra الوهمية بصيغة CI) — ناجح.
- Backend build (`dotnet build Baseera.slnx`) — نظيف، صفر أخطاء.
- Migration جديدة (`Phase1BObservationTriageApprovalPartsSla`) طُبِّقت فعليًا على SQL Server حقيقي (`dotnet ef database update`) بلا خطأ.

## فجوات موثَّقة في التغطية (Partial، غير مُخفاة)

- لا سيناريو Keyboard-only آلي واحد يغطي دورة الفرز→الاعتماد كاملةً (نفس الفجوة الموروثة من Phase 1A، لم تُغلَق هنا).
- لا اختبار Mobile-viewport مخصَّص للأقسام الجديدة (يُعاد استخدام نفس بنية `.workspace-grid` المختبَرة بنيويًا في Phase 1A فقط).
- لا اختبار مخصَّص لانتهاء تجميد SLA التلقائي عند تجاوز `ReviewDueAtUtc` (لأن الميزة نفسها غير منفَّذة — راجع `phase1b-observation-architecture.md`).
- لا اختبار مخصَّص لعدّ استعلامات N+1 لنقاط النهاية الجديدة تحديدًا (الاستعلامات الإضافية محدودة العدد الثابت لكل طلب تفاصيل واحد، لا تتناسب مع حجم القائمة — نفس نمط Phase 1A، لكن بلا اختبار Assertion صريح جديد يقيسها رقميًا).
