# Phase 1A — تقرير الإنجاز (Observation Workspace Foundation and Core Operational Flow)

**الفرع**: `ux-rescue-phase1a-observation-workspace-foundation`
**يُطبِّق جزئيًا**: Issue #143 (Rebuild Observation Workspace)
**استمرار**: Issue #11 (Facility Workspace)
**صلة**: Issue #18 (Unified Operational Timeline — نطاق ملاحظات-الأحداث فقط)، Issue #19 (Command Alerts Center — نطاق التصعيدات الحالية فقط)
**لا يُغلَق في هذه الدفعة**: #143، #11، #18، #19.

## معيار النجاح المُصرَّح به من صاحب المهمة

> "النجاح ليس تغيير شكل شاشة الملاحظات؛ النجاح هو أن يتمكن المستخدم من الدخول من مساحة السجن، فتح الملاحظة، فهمها، تنفيذ الإجراء الأساسي، الانتقال للتالية، والعودة دون فقد السياق أو الحاجة إلى التنقل بين صفحات متعددة."

هذا التقرير يُقيَّم مقابل هذا المعيار تحديدًا، لا مقابل عدد الأسطر المُعاد كتابتها.

## ملخص تنفيذي

اكتشاف مركزي وجَّه كل قرار في هذه الدفعة: **الخادم كان أنضج بكثير مما أوحى به ملخص تدقيق Phase 0**. `GET /notes/workspace` و`GET /notes/{id}/workspace` كانا موجودَين فعليًا (`NoteWorkspaceQueryService`)، فوق طبقة استعلام ناضجة أصلًا (`NoteQueryService`: Pagination/Filter/Sort على الخادم، تصفية نطاق حقيقية، حجب حساس). كذلك `ObservationWorkspacePage.tsx` في الواجهة كانت أصلًا Master-detail حقيقيًا يعمل، لا هيكلاً فارغًا. هذا حوّل خطة التنفيذ بالكامل من "ابنِ خادمًا جديدًا" إلى "أصلِح فجوات محدَّدة ووثِّقها" — أعاد استخدام كل ما كان صحيحًا، وترك أثرًا أصغر بكثير مما لو أُعيد البناء من الصفر.

## Routes: قبل/بعد

| Route | قبل | بعد |
| --- | --- | --- |
| `/notes` | يُعرِّف `ObservationWorkspacePage` مباشرة | `NotesIndexRoute` — Navigate لـ`/notes/workspace` (علم مفعَّل) أو `NotesListPage` فعليًا (علم معطَّل) |
| `/notes/:id` | يُعرِّف `NoteDetailPage` مباشرة | `NoteDetailRoute` — Navigate لـ`/notes/workspace?noteId=` (علم مفعَّل) أو `NoteDetailPage` فعليًا (علم معطَّل) |
| `/notes/workspace` | `ObservationWorkspacePage` | بلا تغيير في المسار، توسيع كبير في العقد والسلوك (تفصيل أدناه) |
| `/notes/:id/edit`, `/notes/:noteId/corrective-actions/new` | صفحات كاملة | بلا تغيير — Legacy fallback مقصود |

## سلوك علم الميزة

`VITE_OBSERVATION_WORKSPACE_V2` — مفعَّل افتراضيًا؛ القيمة الوحيدة التي تُعطِّله `'false'` الحرفية. يتحكم بحلّ الـRoute نفسه (ليس مجرد رابط شريط جانبي) — مؤكَّد بـ5 اختبارات في `NotesRouteResolvers.test.tsx`. تفصيل كامل: `phase1a-observation-route-transition.md`.

## سلوك الـLegacy Fallback

`NotesListPage.tsx`، `NoteDetailPage.tsx`، `NoteCreatePage.tsx`، `NoteEditPage.tsx` — لا شيء منها حُذف أو عُطِّل. `NotesListPage.tsx` تحديدًا كان يتيمًا مؤكَّدًا قبل هذه الدفعة (موثَّق في `screen-and-route-inventory.md`)؛ أصبح الآن مستورَدًا فعليًا ومُستخدَمًا خلف العلم — **لم يعد يتيمًا**، ووظيفته الفريدة (استعادة ملاحظة مؤرشفة عبر RowVersion) بقيت متاحة تلقائيًا بلا أي نقل كود.

## مكوّنات مشتركة جديدة

`src/frontend/src/shared/workspaces/WorkspaceStateView.tsx` (`WorkspaceSkeletonRows`, `WorkspaceEmptyState`, `WorkspaceErrorState`) — أساس مشترك متعمَّد الصغر، مرشَّح لإعادة الاستخدام في #144/#145/Region/HQ دون تعديل. راجع `phase1a-observation-architecture.md` لتبرير عدم بناء مكتبة أكبر.

## Backend read models

لا Endpoint جديد. تعديلات على الموجود:
- `NoteWorkspaceDetailDto`: حذف `Resources`/`Decisions`/`Links` (كانت دائمًا فارغة).
- `NoteWorkspaceQueryService`: إضافة `VERIFY_CLOSURE` لحساب `AllowedActions` (إصلاح فجوة فعلية — Endpoint وعميل واجهة كانا موجودَين، لكن الزر لم يكن يظهر أبدًا)؛ تحديد Timeline بـ30 عنصرًا؛ استخراج `ComputeAllowedActions`/`ResolveProgress`/`ResolveBlocker` كدوال Static نقية قابلة للاختبار المباشر.
- `NoteCommandService.CreateDraftAsync`: تفعيل وراثة `FacilityUnitId` (كانت البنية التحتية موجودة، غير مستدعاة).
- `NoteTypeAccessService`: إصلاح N+1 ذاتي حقيقي داخل الطلب الواحد (ذاكرة تخزين مؤقت بمستوى الطلب) — خفَّض استعلامات تفاصيل الملاحظة من 37 إلى 25 دون تغيير سلوكي.

## عمليات أصبحت الآن داخل الـWorkspace

Submit، Assign، Reassign (نموذج Inline بمُنتقي مكلَّف عبر `eligible-assignees`)، StartWork، RequestVerification، RejectVerification، VerifyClosure (نموذج Inline بملخص إغلاق)، Reopen، Cancel، إضافة مرفق.

## عمليات مؤجَّلة (موثَّقة، لا مخفية)

`ADD_ACTION` (إضافة إجراء تصحيحي) يبقى رابط Fallback صريح لصفحة كاملة — إعادة بناء الإجراءات التصحيحية بالكامل خارج نطاق هذه الدفعة. "قبول التكليف" و"تصعيد فوري من الملاحظة": Not Applicable — لا أمر Domain مستقل لهما إطلاقًا (ليس نقص واجهة).

## أمان وراثة السجن/الوحدة

`NoteScopeService.ResolveIntakeAsync` (موجودة مسبقًا) تتحقق من نطاق المستخدم التنظيمي وقفل ملف الإدخال بغضّ النظر عمّا يرسله العميل. اختبار تكامل جديد (`Create_rejects_client_supplied_facility_outside_the_callers_scope_over_http`) يثبت رفضًا فعليًا (403) عند تلاعب حقيقي عبر HTTP، لا فحصًا نظريًا فقط.

## المقاييس قبل/بعد

راجع `docs/ux-rescue/task-metrics-baseline.md` (قسم "نتائج Phase 1A الفعلية"). أبرز الأرقام: مغادرة الـWorkspace للدورة الكاملة انخفضت من 4 مسارات إلى مسار Fallback مقصود واحد؛ التنقّل بين ملاحظتين من 2-3 نقرات + فقد تحديد إلى نقرة واحدة بلا فقد؛ إنشاء ملاحظة من Facility Workspace: صفر إعادة اختيار سجن، صفر شاشات إضافية؛ 3 تبويبات دائمة الفراغ أُزيلت.

## عدد الاختبارات

- **Backend Unit**: 960 (كان 932)، Failed=0, Skipped=0.
- **Backend Integration — Notes* فقط**: 34/34 ناجح (10 جديدة + 24 موجودة مسبقًا، بلا انحدار).
- **Backend Integration — المجموعة الكاملة**: **251/251 ناجح، Failed=0, Skipped=0** (تشغيل فعلي كامل على SQL Server حقيقي، 16.86 دقيقة، عبر كل مجموعات الاختبار الأربع: Core/Forms/Operations/Workforce). لا انحدار في أي مجال آخر (Forms، التصعيد، الإشغال، الموارد، القوى البشرية، العهد الحساسة، المخاطر، لوحة المتابعة، الإجراءات التصحيحية) بعد تعديلات `NoteCommandService`/`NoteTypeAccessService`/`NoteWorkspaceQueryService`.
- **Frontend**: 294/294 عبر 57 ملفًا، بلا Skipped.

## عدّ الاستعلامات وحجم الحمولة

راجع `phase1a-observation-performance.md`. الخلاصة: قائمة الـWorkspace وتفاصيلها كلاهما مثبَتان تجريبيًا (Equal assertion بين حجم صغير وكبير) كمستقلَّين عن حجم البيانات — لا N+1. اكتُشف وأُصلِح N+1 ذاتي فعلي أثناء كتابة هذا الاختبار بالذات.

## CI / SonarCloud / Gitleaks

راجع الفقرة الأخيرة من هذا التقرير (تُستكمَل بعد فتح الـPR وتشغيل CI الفعلي — هذا القسم لا يمكن تعبئته قبل الدفع).

## سجل الامتثال

`docs/ux-rescue/phase1a-observation-compliance-ledger.md` — **Missing = 0**.

## الفجوات المتبقية لـPhase 1B/1C

- Selector مخصَّص لـ`owner`/`assignee` في الفلاتر.
- توحيد كامل لمكوّن المرفقات عبر كل السياقات (ليس الملاحظات فقط).
- فحص Keyboard-only شامل من طرف لنهاية كسيناريو آلي واحد.
- نقل قدرة "استعادة ملاحظة مؤرشفة" من `NotesListPage.tsx` إلى الـWorkspace نفسه، عند إزالة الـLegacy fallback نهائيًا.
- قياس حجم الحمولة بالبايت بأداة مخصَّصة (الحد البنيوي مؤكَّد الآن، لا رقم مقيس).
- إعادة بناء الإجراءات التصحيحية بالكامل داخل الـWorkspace (تبقى Fallback).

## تأكيدات صريحة

- **#143 يبقى مفتوحًا** — لم يُغلَق في هذه الدفعة.
- **لا صور/لقطات شاشة استُخدِمت كمعيار قبول** في أي مرحلة من هذا العمل.
- **#144/#145 لم يبدآ** — لا تغيير على أي ملف يخصهما.
