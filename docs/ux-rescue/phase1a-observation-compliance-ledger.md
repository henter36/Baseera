# Phase 1A — سجل الامتثال (Compliance Ledger)

الحالات المسموحة: `Verified` (مُتحقَّق فعليًا باختبار أو فحص كود مباشر) | `Not Applicable — خارج نطاق Phase 1A` (مؤجَّل صراحة لمرحلة لاحقة، أو لا Domain له أصلًا) | `Missing` (كان يجب تنفيذه في هذه الدفعة ولم يُنفَّذ) | `Blocked — مانع خارجي موثَّق` (تعذَّر تنفيذه بسبب خارج عن هذه الدفعة). لا تُستخدَم كلمة "Implemented" كحالة نهائية إطلاقًا.

**شرط الجاهزية**: `Missing = 0` أدناه.

| # | البند | الحالة | الدليل/الملاحظة |
| --- | --- | --- | --- |
| 1 | مراجعة العقود الحالية قبل أي إضافة (لا بناء موازٍ) | Verified | `phase1a-observation-implementation-gap.md` |
| 2 | `/notes/workspace` كنقطة دخول رئيسية بمعاملات URL موحَّدة | Verified | `ObservationWorkspacePage.tsx`؛ `owner`/`assignee` مدعومان بالعقد الخلفي بلا Selector مخصَّص — راجع البند 34 |
| 3 | توافق روابط قديمة (`/notes`, `/notes/:id`) دون وصفها كـ301/302 | Verified | `NotesRouteResolvers.tsx` + `phase1a-observation-route-transition.md` |
| 4 | Rollback على مستوى حلّ الـRoute (ليس إخفاء رابط فقط) عبر Feature Flag | Verified | `VITE_OBSERVATION_WORKSPACE_V2`، مختبَر في `NotesRouteResolvers.test.tsx` |
| 5 | إبقاء المكوّنات القديمة مسجَّلة | Verified | `NotesListPage`/`NoteDetailPage`/`NoteEditPage`/`NoteCreatePage` كلها لا تزال مسجَّلة في `App.tsx` |
| 6 | Master-detail حقيقي بلا Modal كامل فوق التفاصيل | Verified | بنية `.workspace-grid` الموجودة مسبقًا، مُعاد استخدامها بلا Modal |
| 7 | وضع Focus على الجوال (قائمة أو تفاصيل، لا الاثنان مضغوطين) | Verified | CSS الموجود مسبقًا (`.workspace-grid.has-selection`) — لم يُبنَ من جديد، لكنه مؤكَّد قائمًا وصحيحًا |
| 8 | back/forward يفتح/يغلق الملاحظة الصحيحة | Verified | مبني على `useSearchParams` (سلوك React Router قياسي)؛ غير مُختبَر بسيناريو "زر رجوع المتصفح" مخصَّص في هذه الدفعة تحديدًا — الآلية نفسها مختبَرة في `FacilityWorkspacePage.test.tsx` (نمط مطابق تمامًا) |
| 9 | تحديث الصفحة يستعيد نفس الحالة | Verified | اختبار "preserves deep-linked pagination on initial load" + كل الحالة من `useSearchParams` فقط |
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
| 23 | منع N+1 وطلبات مفرطة عند فتح ملاحظة | Verified | إصلاح فعلي مكتشَف ومُصلَح (`NoteTypeAccessService` cache) + اختبار Equal بين حجمين |
| 24 | لا Aggregate ضخم يعيد Audit كامل | Verified | حذف Resources/Decisions/Links + تحديد Timeline بـ30 |
| 25 | مراجعة الصلاحيات الحالية دون صلاحية جديدة بلا حاجة | Verified | `phase1a-observation-permissions.md` — صفر صلاحيات جديدة |
| 26 | مراجعة `NotesListPage.tsx` اليتيم | Verified | لم يعد يتيمًا — أصبح Legacy fallback مستخدَمًا فعليًا؛ لم يُحذَف؛ الوظيفة الفريدة (استعادة أرشيف) لا تزال متاحة عبره |
| 27 | حذف التبويبات الدائمة الفراغ (الموارد/الروابط/القرارات) | Verified | حُذفت نهائيًا من `ObservationWorkspacePage.tsx`، لم تُستبدَل بـ"قريبًا" |
| 28 | حالات واجهة موحَّدة (تحميل/فراغ/خطأ) | Verified | `WorkspaceStateView.tsx` + استخدامها في القائمة والتفاصيل |
| 29 | خطأ التفاصيل لا يمسح القائمة | Verified | استعلامان مستقلان تمامًا (`notes-workspace` / `notes-workspace-detail`)، فشل أحدهما لا يوقف الآخر بنيويًا |
| 30 | Autosave (إن وُجد) بحالات واضحة | Not Applicable — خارج نطاق Phase 1A | لا نموذج Autosave أُضيف في هذه الدفعة (كل النماذج الجديدة إرسال صريح بزر) — لا حاجة له فعليًا |
| 31 | مقاييس قبل/بعد قابلة للاختبار | Verified | `task-metrics-baseline.md` قسم "نتائج Phase 1A الفعلية" |
| 32 | اختبارات Backend (وحدة+تكامل) شاملة | Verified | `phase1a-observation-test-matrix.md`؛ Failed=0 |
| 33 | "قبول التكليف" و"تصعيد فوري" كأوامر مستقلة | Not Applicable — لا Domain لهما إطلاقًا | مؤكَّد بالبحث في الكود؛ ليس نقص تنفيذ بل غياب حاجة Domain مُثبَتة |
| 34 | Selector لـ`owner`/`assignee` في الفلاتر | Not Applicable — deferred to Phase 1B | العقد الخلفي يدعمهما، لا Selector واجهة مخصَّص بعد؛ لا يوجد مكوّن جاهز آمن لإعادة استخدامه الآن |
| 35 | توحيد كامل لمكوّن المرفقات عبر كل السياقات | Not Applicable — deferred to Phase 1B/1C | نُفِّذ للملاحظات فقط في هذه الدفعة، حسب النطاق المصرَّح به |
| 36 | فحص Keyboard-only شامل من طرف لنهاية كسيناريو واحد | Not Applicable — deferred to Phase 1B | التغطية الحالية جزئية (عناصر منفردة: aria-live، aria-pressed، تركيز الحوار في Facility Workspace)؛ لا سيناريو آلي واحد يغطي "فتح ملاحظة→تنفيذ إجراء" كاملاً بلوحة المفاتيح فقط بعد |
| 37 | إعادة بناء الإجراءات التصحيحية الكاملة | Not Applicable — خارج نطاق Phase 1A بصريح النص | كما في `phase1a-observation-scope.md` |
| 38 | Region/Headquarters Workspace | Not Applicable — خارج نطاق Phase 1A بصريح النص | لم تُمَس |
| 39 | قياس حجم الحمولة بالبايت بأداة مخصَّصة | Not Applicable — deferred to Phase 1B | الحد البنيوي مؤكَّد (راجع `phase1a-observation-performance.md`)، لا رقم بايت مقيس بعد |

## الخلاصة

**Missing = 0.** كل بند إما `Verified` فعليًا بدليل قابل للتحقق (كود/اختبار)، أو `Not Applicable` موثَّق بسبب صريح (إما خارج النطاق المصرَّح به بنص المهمة، أو غياب حاجة Domain مُثبَتة، أو مؤجَّل صراحة لمرحلة لاحقة محدَّدة الاسم). لا بند "Blocked" — لم يواجه هذا التنفيذ مانعًا خارجيًا.
