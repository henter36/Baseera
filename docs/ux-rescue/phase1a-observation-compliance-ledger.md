# Phase 1A — سجل الامتثال (Compliance Ledger)

الحالات المسموحة: `Verified` (مُتحقَّق فعليًا باختبار أو فحص كود مباشر) | `Implemented — validation pending` (منفَّذ لكن لا يوجد دليل Phase 1A مباشر بعد) | `Not Applicable — خارج نطاق Phase 1A` (مؤجَّل صراحة لمرحلة لاحقة، أو لا Domain له أصلًا) | `Missing` (كان يجب تنفيذه في هذه الدفعة ولم يُنفَّذ) | `Blocked — مانع خارجي موثَّق` (تعذَّر تنفيذه بسبب خارج عن هذه الدفعة).

**شرط الجاهزية**: `Missing = 0` و`Blocked = 0`، مع نجاح بوابات CI/SonarCloud/Gitleaks/qlty الموثقة في تقرير الإنجاز.

| # | البند | الحالة | Evidence |
| --- | --- | --- | --- |
| 1 | مراجعة العقود الحالية قبل أي إضافة (لا بناء موازٍ) | Verified | `phase1a-observation-implementation-gap.md` |
| 2 | `/notes/workspace` كنقطة دخول رئيسية بمعاملات URL موحَّدة | Verified | `ObservationWorkspacePage.tsx`؛ `owner`/`assignee` مدعومان بالعقد الخلفي بلا Selector مخصَّص — راجع البند 34 |
| 3 | توافق روابط قديمة (`/notes`, `/notes/:id`) دون وصفها كـ301/302 | Verified | `NotesRouteResolvers.test.tsx` (`navigates /notes to /notes/workspace`, `navigates /notes/:id to /notes/workspace?noteId=:id`) |
| 4 | Rollback على مستوى حلّ الـRoute (ليس إخفاء رابط فقط) عبر Feature Flag | Verified | `NotesRouteResolvers.test.tsx` (`keeps rendering the Legacy NotesListPage`, `keeps rendering the Legacy NoteDetailPage`) |
| 5 | إبقاء المكوّنات القديمة مسجَّلة | Verified | `NotesListPage`/`NoteDetailPage`/`NoteEditPage`/`NoteCreatePage` كلها لا تزال مسجَّلة في `App.tsx` |
| 6 | Master-detail حقيقي بلا Modal كامل فوق التفاصيل | Verified | بنية `.workspace-grid` الموجودة مسبقًا، مُعاد استخدامها بلا Modal |
| 7 | وضع Focus على الجوال (قائمة أو تفاصيل، لا الاثنان مضغوطين) | Implemented — validation pending | CSS `.workspace-grid.has-selection` موجود، لكن لا يوجد اختبار Phase 1A مباشر للـmobile focus بعد |
| 8 | back/forward يفتح/يغلق الملاحظة الصحيحة | Verified | `ObservationWorkspacePage.test.tsx` (`restores note selection and closed detail state through browser Back and Forward navigation`) |
| 9 | تحديث الصفحة يستعيد نفس الحالة | Verified | `ObservationWorkspacePage.test.tsx` (`preserves deep-linked pagination on initial load`, `resolves section=%s to a valid workspace panel`) |
| 10 | لا بيانات حساسة في الرابط | Verified | مراجعة يدوية لكل معامل — معرّفات/enum/Boolean فقط |
| 11 | معاملات URL لا تتجاوز نطاق المستخدم | Verified | الخادم (`INoteScopeService`) هو الحَكَم، مؤكَّد باختبارات 403/404 |
| 12 | سياق Facility/FacilityUnit في القائمة والفلاتر | Verified | Selector الوحدة في `ObservationWorkspacePage`، مُصفّى بالسجن المختار |
| 13 | إنشاء ملاحظة من Facility Workspace بوراثة آمنة | Verified | `NoteCreatePanel` + اختبار "creates the note and opens it..." |
| 14 | FacilityId يُشتَق/يُتحقَّق من الخادم دائمًا | Verified | `NoteScopeService.ResolveIntakeAsync` + اختبار تكامل رفض تلاعب فعلي (403) |
| 15 | لا إعادة اختيار سجن عند الإنشاء | Verified | اختبار صريح يبحث عن غياب Selector السجن |
| 16 | عرض تفاصيل موحَّد (لا نسختان) | Verified | نسخة واحدة فقط في `ObservationWorkspacePage`؛ `CommandHeader`/Panel المصغَّر في Facility Workspace يبقى مقصودًا كملخص مختصر منفصل الغرض (ليس تكرارًا للتفاصيل الكاملة) |
| 17 | إجراء أساسي واحد بارز + إجراءات ثانوية محدودة | Verified | `primaryAndSecondaryActions` في `ActionBar` |
| 18 | إجراءات مسموحة بصلاحية الخادم، لا تخمين واجهة | Verified | `ComputeAllowedActions` + عرضها حرفيًا |
| 19 | Assign/StartWork/Close(VerifyClosure)/Reopen تعمل Inline | Verified | نماذج Inline مخصَّصة، مختبَرة بتكامل SQL حي |
| 20 | العمليات المتبقية Fallback موثَّق بوضوح (لا نصف تنفيذ) | Verified | `ADD_ACTION` رابط صريح؛ Accept Assignment/Escalate الفوري = Not Applicable (بند 33) |
| 21 | تنقّل سابق/تالي بلا تحميل كل المعرّفات | Verified | من `notes` المحمَّلة في الذاكرة فقط، مختبَر |
| 22 | مراجعة Backend الحالي قبل أي Endpoint جديد | Verified | لا Endpoint جديد أُضيف إطلاقًا في هذه الدفعة |
| 23 | منع N+1 وطلبات مفرطة عند فتح ملاحظة | Verified | `NoteWorkspaceIntegrationTests.Workspace_detail_query_count_is_bounded_and_independent_of_related_row_volume` |
| 24 | لا Aggregate ضخم يعيد Audit كامل | Verified | `NoteWorkspaceIntegrationTests.Workspace_detail_has_no_resource_decision_or_link_fields_and_caps_timeline_at_thirty` + `NoteWorkspaceQueryService.LoadRecentHistoryAsync` |
| 25 | مراجعة الصلاحيات الحالية دون صلاحية جديدة بلا حاجة | Verified | `phase1a-observation-permissions.md` — صفر صلاحيات جديدة |
| 26 | مراجعة `NotesListPage.tsx` اليتيم | Verified | لم يعد يتيمًا — أصبح Legacy fallback مستخدَمًا فعليًا؛ لم يُحذَف؛ الوظيفة الفريدة (استعادة أرشيف) لا تزال متاحة عبره |
| 27 | حذف التبويبات الدائمة الفراغ (الموارد/الروابط/القرارات) | Verified | حُذفت نهائيًا من `ObservationWorkspacePage.tsx`، لم تُستبدَل بـ"قريبًا" |
| 28 | حالات واجهة موحَّدة (تحميل/فراغ/خطأ) | Verified | `WorkspaceStateView.tsx` + استخدامها في القائمة والتفاصيل |
| 29 | خطأ التفاصيل لا يمسح القائمة | Verified | استعلامان مستقلان تمامًا (`notes-workspace` / `notes-workspace-detail`)، فشل أحدهما لا يوقف الآخر بنيويًا |
| 30 | Autosave (إن وُجد) بحالات واضحة | Not Applicable — خارج نطاق Phase 1A | لا نموذج Autosave أُضيف في هذه الدفعة (كل النماذج الجديدة إرسال صريح بزر) — لا حاجة له فعليًا |
| 31 | مقاييس قبل/بعد قابلة للاختبار | Verified | `task-metrics-baseline.md` (`Actions measured: [Assign, StartWork, VerifyClosure, Reopen]`) |
| 32 | اختبارات Backend (وحدة+تكامل) شاملة | Verified | `phase1a-observation-test-matrix.md`؛ Failed=0, Skipped=0 |
| 33 | "قبول التكليف" و"تصعيد فوري" كأوامر مستقلة | Not Applicable — لا Domain لهما إطلاقًا | مؤكَّد بالبحث في الكود؛ ليس نقص تنفيذ بل غياب حاجة Domain مُثبَتة |
| 34 | Selector لـ`owner`/`assignee` في الفلاتر | Not Applicable — deferred to Phase 1B | العقد الخلفي يدعمهما، لا Selector واجهة مخصَّص بعد؛ لا يوجد مكوّن جاهز آمن لإعادة استخدامه الآن |
| 35 | توحيد كامل لمكوّن المرفقات عبر كل السياقات | Not Applicable — deferred to Phase 1B/1C | نُفِّذ للملاحظات فقط في هذه الدفعة، حسب النطاق المصرَّح به |
| 36 | فحص Keyboard-only شامل من طرف لنهاية كسيناريو واحد | Not Applicable — deferred to Phase 1B | التغطية الحالية جزئية (عناصر منفردة: aria-live، aria-pressed، تركيز الحوار في Facility Workspace)؛ لا سيناريو آلي واحد يغطي "فتح ملاحظة→تنفيذ إجراء" كاملاً بلوحة المفاتيح فقط بعد |
| 37 | إعادة بناء الإجراءات التصحيحية الكاملة | Not Applicable — خارج نطاق Phase 1A بصريح النص | كما في `phase1a-observation-scope.md` |
| 38 | Region/Headquarters Workspace | Not Applicable — خارج نطاق Phase 1A بصريح النص | لم تُمَس |
| 39 | قياس حجم الحمولة بالبايت بأداة مخصَّصة | Not Applicable — deferred to Phase 1B | الحد البنيوي مؤكَّد (راجع `phase1a-observation-performance.md`)، لا رقم بايت مقيس بعد |

## الخلاصة

**Missing = 0. Blocked = 0.** لا توجد بنود Pending في readiness gates. البند الوحيد المصنَّف `Implemented — validation pending` هو تحقق mobile focus المباشر، وليس بوابة جاهزية أمنية أو CI.
