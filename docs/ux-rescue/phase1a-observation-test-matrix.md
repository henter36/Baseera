# Phase 1A — مصفوفة الاختبارات

## Backend — Unit Tests

**الملف**: `src/backend/tests/Baseera.UnitTests/NoteWorkspaceQueryServiceTests.cs` (25 اختبارًا، بلا قاعدة بيانات — منطق نقي فقط عبر `FakeCurrentUser`):

- `VERIFY_CLOSURE` مسموح فقط من `PendingVerification` مع الصلاحية (Theory على كل قيم `NoteStatus` الثمانية).
- `VERIFY_CLOSURE` غير مسموح إطلاقًا بلا صلاحية حتى من `PendingVerification`.
- `REASSIGN` يتطلب تكليفًا حاليًا فعليًا (`CurrentAssignment` غير فارغ).
- حالات القفل النهائي (`Closed`/`Cancelled`) لا تسمح بـ`REASSIGN`/`CANCEL` إطلاقًا.
- الإجراءات المسموحة مرتبطة بالصلاحية بشكل مستقل عن الحالة.
- `ResolveProgress` يُخطِّط كل حالة لنسبة ثابتة (Theory)، وحالة `InProgress` تعتمد على عدد الإجراءات المفتوحة (65 مقابل 55).
- `ResolveBlocker` يُعطي أولوية للتجاوز الزمني، ثم انتظار التحقق، ثم الإجراءات المفتوحة، أو `null` إن لم يوجد عائق.

**الملف**: `src/backend/tests/Baseera.UnitTests/NoteCommandServiceFacilityInheritanceTests.cs` (3 اختبارات — مسارات الرفض فقط؛ مسارات النجاح غير قابلة للاختبار بمزوّد EF InMemory لأن `CreateDraftAsync` يستدعي تسلسل SQL خام غير مدعوم فيه — موثَّق داخل الملف نفسه وموكول لاختبارات التكامل):

- رفض وحدة تابعة لسجن مختلف عن السجن المطلوب (`InvalidOperationException`).
- رفض سجن مُرسَل من العميل خارج نطاق المستخدم المصرَّح له (`UnauthorizedAccessException`).
- رفض وحدة غير موجودة أصلًا (`KeyNotFoundException`).

**إجمالي Unit Tests بعد هذه الدفعة: 960 (كان 932 قبلها) — Failed=0, Skipped=0.**

## Backend — Integration Tests (SQL Server حقيقي)

**الملف الجديد**: `src/backend/tests/Baseera.IntegrationTests/NoteWorkspaceIntegrationTests.cs` (10 اختبارات — يغطي فقط ما لم تكن `NotesCoreIntegrationTests`/`NotesAdditionalIntegrationTests` تغطيه، بلا تكرار متعمَّد):

1. إنشاء بلا وحدة → نطاق Facility، يظهر في قائمة الـWorkspace.
2. إنشاء بوحدة صحيحة → نطاق FacilityUnit، يُصفَّى صحيحًا بمعامل `facilityUnitId` (ويُستبعَد من وحدة أخرى).
3. رفض سجن مُرسَل من العميل خارج نطاقه — عبر HTTP فعليًا (403)، وليس فقط على مستوى الخدمة كما في اختبارات الوحدة.
4. رفض وحدة تابعة لسجن آخر — عبر HTTP فعليًا (409).
5. `VERIFY_CLOSURE` يظهر فقط من `PendingVerification`، ويختفي بعد الإغلاق الفعلي (دورة كاملة: إنشاء→تقديم→تكليف→بدء معالجة→طلب تحقق→اعتماد إغلاق، بمستخدمين منفصلين تمامًا لمعالج/معتمد لتفادي فصل الواجبات).
6. تفاصيل الـWorkspace لا تحتوي حقول `resources`/`decisions`/`links` إطلاقًا (فحص JSON خام)، والـTimeline محدود بـ30 عنصرًا رغم توليد أكثر من 30 سجل تاريخ حقيقي.
7. تفاصيل ملاحظة خارج النطاق التنظيمي تعيد 404.
8. القائمة تدعم Pagination/Sort/Search فعليًا عبر `/notes/workspace`.
9. عدد استعلامات القائمة محدود ومستقل عن حجم البيانات (مقارنة صريحة صغير/كبير).
10. عدد استعلامات التفاصيل محدود ومستقل عن حجم الصفوف المرتبطة (مقارنة صريحة صغير/كبير) — واكتشف وأصلح N+1 ذاتي حقيقي في `NoteTypeAccessService` أثناء كتابة هذا الاختبار تحديدًا.

**تشغيل هذا الملف فقط: 10/10 ناجح، Skipped=0.**

**تشغيل كل اختبارات Notes* (شامل `NotesCoreIntegrationTests` + `NotesAdditionalIntegrationTests` + الملف الجديد): 34/34 ناجح** — يثبت عدم وجود انحدار في التغطية الموجودة مسبقًا (نطاق Region/HQ/Facility، الحجب الحساس، Pagination على `/notes`، فصل الواجبات، التزامن/RowVersion، المرفقات) بعد تعديل `NoteCommandService`/`NoteTypeAccessService`/`NoteWorkspaceQueryService`.

**تشغيل مجموعة التكامل الكاملة (كل الوحدات، لا Notes فقط): 251/251 ناجح، Failed=0, Skipped=0** (16.86 دقيقة، SQL Server حقيقي، كل مجموعات Core/Forms/Operations/Workforce).

## Frontend — Vitest

- `src/frontend/src/pages/notes/ObservationWorkspacePage.test.tsx`: Master-detail + إجراءات الخادم، إرسال الفلاتر للخادم، حفظ Deep link للصفحة، تحقق `section=summary/evidence/bogus/missing`، مسح حالة الإجراء المضمَّن عند تبديل الملاحظة، **تنقّل سابق/تالي ضمن النافذة المحمَّلة**، **Back/Forward حقيقي عبر history**، **مسح file input بعد الرفع**، **نموذج VERIFY_CLOSURE المخصَّص + رابط العودة لمساحة السجن**، **الأقسام الخمسة بلا تبويبات دائمة فارغة**.
- `src/frontend/src/pages/notes/NotesRouteResolvers.test.tsx`: توجيه `/notes`→`/notes/workspace` والعكس عند تعطيل العلم؛ نفس الشيء لـ`/notes/:id`؛ تثبيت feature flag داخل suites؛ فحص location الفعلي؛ نقل الفلاتر الآمنة فقط؛ حذف `unsafeParam`؛ وتغليب `noteId` القادم من المسار على query عدائي.
- `src/frontend/src/pages/workspaces/FacilityWorkspacePage.test.tsx` (30 اختبارًا، 27 موجودة + 3 جديدة): إخفاء زر "فتح ملاحظة" بلا `Notes.Create`؛ عدم وجود Selector سجن في نموذج الإنشاء (السياق موروث لا مُعاد اختياره)؛ إنشاء الملاحظة فعليًا والتحقق من الانتقال إلى `/notes/workspace?noteId=...&facilityId=...&source=facility:...` بجسم طلب يحمل `facilityId`/`scopeType`/`facilityUnitId` الصحيحة.

**إجمالي Frontend: 301/301 ناجح عبر 57 ملفًا** (كان قبل هذه الدفعة رقم أقل بمقدار الاختبارات المذكورة أعلاه) — Failed=0, Skipped=0.

## بوابات الجودة الإضافية (مؤكَّدة فعليًا في هذه الدفعة)

- `npm run typecheck` (tsc -b) — نظيف.
- `npm run lint` (oxlint) — بلا تحذيرات/أخطاء جديدة (تحذيرات موجودة مسبقًا في ملفات لم تُمَس فقط).
- `npm run check:ux-routes` — ناجح (62 Route موثَّقة، 34 نوع Context Panel متطابق).
- `npm audit --audit-level=high` — 0 ثغرات.
- بناء الإنتاج (`tsc -b && vite build`) — ناجح.
