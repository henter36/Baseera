# جرد المكونات وأنماط التفاعل — UX Rescue Phase 0

## الحقيقة البنيوية الأهم أولًا

`src/frontend/src/shared/` — وهو المكان الطبيعي لأي مكتبة مكونات مشتركة — يحتوي ملفًا واحدًا فقط: `listPageUtils.ts` (33 سطرًا)، وهو 4 دوال تنسيق نصية بحتة (`formatListDate`, `listSortIndicator`, `nextListSortState`, `listQueryErrorMessage`). **لا توجد مكتبة Table أو Card أو Modal أو FilterBar أو Toast مشتركة في التطبيق بأكمله.** الاستثناء الوحيد الحقيقي لمكوّن مشترك عبر أكثر من صفحة واحدة هو `src/frontend/src/workspaces/WorkspaceShell.tsx` (301 سطرًا)، لكنه بدوره **مستخدَم فعليًا في مكوّن واحد فقط** (`ReferenceWorkspacePage`، وهو مرجعي/تطويري)؛ أما `FacilityWorkspacePage` (المساحة التشغيلية الحقيقية الوحيدة) فيستورد منه فقط القطع الصغيرة (`WorkspaceEmpty/Error/Loading/Unauthorized/FilterBar`) ويبني ترويسته وتخطيطه الخاص من الصفر.

هذا يعني: كل صفحة في الـ49 ملف Page تقريبًا تُعيد تنفيذ الجدول، شريط الفلاتر، البطاقة، وحالات الفراغ/التحميل/الخطأ **بشكل مستقل**، بأسلوب متشابه ظاهريًا (بفضل CSS مشترك في `index.css`) لكن بمنطق React منفصل تمامًا في كل ملف.

## أنماط Page Header

ثلاثة أنماط مختلفة غير موحّدة:
1. **عنوان بسيط + وصف** (`<h1 className="page-title">` + `<p className="muted">`) — النمط الأشيع، يظهر في `RegionsPage`, `FacilitiesPage`, `FormsListPage`, وغيرها من الصفحات المسطحة.
2. **ترويسة Workspace الحقيقية** (`WorkspaceHeader` في `WorkspaceShell.tsx`) — تعرض مستوى النطاق، اسم النطاق، طابع الطزاجة (Freshness badge)، طابع الثقة (Confidence badge)، وشارة "مرجعية" عند الحاجة. مستخدَمة في `ReferenceWorkspacePage` فقط.
3. **`CommandHeader` مخصّص** داخل `FacilityWorkspacePage` — يعيد تنفيذ نفس مفهوم الترويسة رقم 2 (حالة، ثقة، آخر تحديث) لكن بمكوّن منفصل تمامًا بلا أي كود مشترك مع `WorkspaceHeader`، بالإضافة إلى زر "مركز الإجراءات" وبانر تحذير البيانات الجزئية الخاص به.

**القرار**: نمط 2 و3 يقدّمان نفس المفهوم الوظيفي (حالة + ثقة + طزاجة + إجراءات) بتنفيذين منفصلين بالكامل. يجب توحيدهما في مكوّن واحد يُستخدم من كل مساحات العمل المستقبلية (Region/Headquarters).

## Toolbars / Action bars / Filter bars

- **شريط بحث+فلاتر بسيط**: حقل بحث + Select واحد أو أكثر، بدون مكوّن مشترك — مكرر حرفيًا في `RegionsPage`, `FacilitiesPage`, `FormsListPage`, `CorrectiveActionsListPage`, `NotesListPage` (اليتيمة)، وغيرها. كل صفحة تكتب `useState`/`useSearchParams` بأسلوبها الخاص.
- **`WorkspaceFilterBar`** (من `WorkspaceShell.tsx`) — شريط تاريخ من/إلى موحّد، مستخدَم في كل من `ReferenceWorkspacePage` و`FacilityWorkspacePage` (استثناء إيجابي: هذا أحد المكونات المشتركة القليلة المستخدَمة فعليًا في أكثر من صفحة واحدة).
- **`FormCompliancePage`'s FiltersPanel** — شريط فلاتر خاص بالصفحة، غير مشترك، يدعم `useUrlFilters` لحفظ الحالة في الرابط — نمط جيد لكنه غير معمَّم لبقية الصفحات.
- **شريط أدوات Form Designer** (`DesignerToolbar`) — تراجع/إعادة/تحقق/معاينة/إرسال، بالإضافة إلى نص حالة الحفظ التلقائي (`aria-live`) — نمط تفاعلي متقدم موجود في مجال واحد فقط، لا مكافئ له في مجالات أخرى ذات حاجة مشابهة (مثل حفظ سياسات التصعيد).

**تكرار مؤكَّد**: نمط "بحث + Select نطاق (منطقة/سجن)" مكتوب من الصفر في **6 ملفات مختلفة على الأقل** (Notes list/workspace، CorrectiveActions list، Forms list، FormCampaignWizard's facility/region selects، FormCompliancePage's filters) دون أي مكوّن Picker مشترك — حتى الاختيار المتعدد للمناطق/السجون في معالج الحملات (`<select multiple>` خام) لا يُعاد استخدامه بين خطوة الاستهداف وخطوة الاستثناءات في نفس الملف.

## Search

لا يوجد مكوّن بحث موحّد. كل صفحة تربط حقل `<input>` نصي بـ`useState` محلي ثم تمرره كفلتر إلى الاستعلام. لا Debounce موحّد (بعض الصفحات، مثل `RespondPage`'s autosave، تستخدم debounce يدويًا 800ms لغرض مختلف تمامًا — الحفظ التلقائي، وليس البحث). لا Autocomplete/typeahead في أي مكان بالتطبيق باستثناء عناصر `<select>` القياسية.

## Tabs / Section navigation

ثلاثة تطبيقات منفصلة لمفهوم "تبويب/قسم" بلا مكوّن مشترك:
1. **تبويبات `WorkspaceDetail`** في `ObservationWorkspacePage` (10 تبويبات: ملخص/إجراءات/تكليفات/موارد/تحقق/rca/مرفقات/روابط/قرارات/سجل زمني) — بعضها (موارد/روابط/قرارات) عبارة عن Placeholder دائم الفراغ (`EmptyOperationalTab`) بصرف النظر عن البيانات الفعلية.
2. **`command-section-nav`** في `FacilityWorkspacePage` (14 قسمًا: عامة/عاجل/عمليات/إشغال/موارد/عهد حساسة/قوى بشرية/مخاطر/مشاريع/التزام/خطط/قرارات/سجل زمني/جودة بيانات) — محفوظ في الرابط عبر `?section=`.
3. **خطوات معالج الحملة** (`FormCampaignWizardPage`) — 7 "خطوات" قابلة للنقر بأي ترتيب (ليست Wizard حقيقي يمنع التقدّم دون تحقق) — تبدو تبويبات وليست خطوات فعلية.

**تعارض مفاهيمي**: النمط 1 و2 يمثلان نفس الفكرة (أقسام داخل مساحة عمل واحدة، محفوظة في الحالة/الرابط) لكن بتطبيقين منفصلين بالكامل — لا مكوّن `WorkspaceSectionNav` مشترك رغم وجود حاجة مطابقة تمامًا في كلا المكانين.

## Cards / KPI cards / Status rails

- **`Metric`** (في `ReferenceWorkspacePage`) — بطاقة رقم+تسمية+لون بسيطة.
- **`MetricCard`** (في `FormCompliancePage`) — نفس المفهوم تقريبًا، تنفيذ منفصل.
- **مقاييس Facility Workspace** — كل قسم (`NotesOverview`, `CorrectiveActionsPayload`, إلخ) يبني شبكة بطاقاته الخاصة (`workspace-metric-grid` صنف CSS مشترك، لكن بنية React منفصلة لكل قسم).
- **بطاقات ObservationWorkspacePage's list pane** (`ObservationCard`) — بطاقة ملاحظة في القائمة الجانبية، تصميم مختلف تمامًا عن بطاقات المقاييس أعلاه رغم اشتراكهما في المفهوم العام "بطاقة".

**ملاحظة من التعليمات الأصلية**: "لا Design System جديد كاملًا ما لم يثبت أن الموجود غير قابل للتطوير" — الموجود (`WorkspaceWidgetContainer`, `Metric`, صنف CSS `workspace-metric-grid`) **قابل فعلًا للتوسعة**؛ المشكلة ليست غياب الأساس التصميمي بل عدم تعميم استخدامه خارج `ReferenceWorkspacePage`.

## Master-detail / Context Panels

- **النمط الوحيد الحقيقي لـMaster-detail** في التطبيق بأكمله هو `ObservationWorkspacePage` (قائمة يسار + تفاصيل يمين، بلا تنقّل صفحة). يوجد أيضًا مكوّن جاهز غير مستخدَم لهذا الغرض تحديدًا: `MasterDetailWorkspaceLayout` في `WorkspaceShell.tsx` (يتضمن زر "رجوع إلى القائمة" مخصص للموبايل) — **لا يستخدمه أي من `ObservationWorkspacePage` أو `FacilityWorkspacePage` فعليًا**، رغم أنه مصمَّم بالضبط لحالتيهما.
- **Context Panel كـDialog منزلق**: `CommandContextPanel` في `FacilityWorkspacePage` (`<dialog>` حقيقي، محفوظ في الرابط `?panel=&entityId=`) — نمط جيد وسليم من ناحية إمكانية الوصول وdeep-linking، لكنه مطبَّق مرة واحدة فقط بلا تعميم كمكوّن مستقل قابل لإعادة الاستخدام في مساحات عمل أخرى.

## Modals / Drawers / Toasts / Confirmation dialogs

- **لا يوجد Toast موحّد في أي مكان بالتطبيق.** جميع رسائل النجاح/الخطأ تُعرض كنص Inline (`role="alert"` أو نص عادي) ضمن الصفحة نفسها — لا نظام إشعارات لحظي (toast/snackbar) على الإطلاق.
- **لا يوجد Modal حقيقي (overlay فوق المحتوى) في أي صفحة قائمة أو تفاصيل عادية.** الاستثناء الوحيد هو `<dialog>` الخاص بـ`CommandContextPanel` و`ActionCenter` في `FacilityWorkspacePage`، وكلاهما يتحول لعرض ملء الشاشة تحت `720px`.
- **لا يوجد نمط Confirmation Dialog موحّد.** الإجراءات الحساسة (رفض، إلغاء، إغلاق) تُنفَّذ عبر أزرار مباشرة مع حقل نص سبب (Reason) مطلوب، دون طبقة تأكيد ثانية منفصلة — القرار الفعلي بالتطبيق هو "اطلب سببًا نصيًا بدل نافذة تأكيد"، وهو نمط ثابت نسبيًا لكنه غير موثَّق كقرار تصميم رسمي في أي مكان.

## Empty / Loading / Partial / Stale / Error states

هذا أحد الأنماط **القليلة المطبَّقة بجودة واتساق معقول** عبر أغلب الصفحات المدروسة (Notes/CorrectiveActions/Forms/FormCampaigns/FormResponses/FormCompliance)، وإن كان بأربعة تطبيقات كودية منفصلة للمفهوم نفسه:
- نمط "قسم بيانات مفقود عن مجال بأكمله" (`DomainUnavailableSection` في Facility Workspace) — مستخدَم لعرض حالة "غير متاح" لمجالات (إشغال/موارد/عهد حساسة/مخاطر/مشاريع/خطط/قرارات) بلا تمييز حقيقي بين "لا توجد بيانات بعد" و"هذه الميزة غير مبنية أصلًا" (المشاريع/الخطط/القرارات لا تملك عقد Backend إطلاقًا وتُعرض بنفس مكوّن "غير متاح" الذي يُستخدم لمجال حقيقي بلا بيانات).
- نمط Query-per-page اليدوي (`query.isLoading`/`isError`/data?.length===0) — الأشيع في صفحات القوائم البسيطة.
- نمط `WorkspaceLoading/Error/Empty/Unauthorized` المشترك من `WorkspaceShell.tsx` — الأفضل هيكلة، لكن مستخدَم فقط في `ReferenceWorkspacePage` و(جزئيًا) الصفحات الثلاث المنفصلة (Occupancy/Resources/Workforce)، وليس في `FacilityWorkspacePage` نفسه أو صفحات Notes/Forms.
- تحذير "بيانات جزئية" (`widgetFailures`) — موجود فقط في إطار Workspace العام (`command-partial-warning` في Facility Workspace)، غائب تمامًا عن أي صفحة قائمة تقليدية حتى لو فشل أحد استعلاماتها المتعددة.

**لا يوجد نمط "بيانات قديمة/Stale" موحّد** — إطار Workspace يحمل `freshness`/`confidence` كحقلين منفصلين معروضين كشارتين نصيتين، لكن لا صفحة تقليدية (قائمة/تفاصيل) تعرض حداثة البيانات إطلاقًا.

## Permission-hidden states

النمط الثابت عبر التطبيق بأكمله: **الغياب الصامت**. `hasPermission(code)` تُستخدم إما لإخفاء عنصر واجهة كليًا (`{hasPermission('X') && <Button/>}`) أو لعرض جملة "ليست لديك صلاحية…" نصية عند بوابة الصفحة بأكملها. لا يوجد تمييز بصري موحّد بين "الزر مخفي لأنك لا تملك الصلاحية" و"الزر غائب لأن الحالة الحالية لا تسمح به" — كلاهما يُترجَم إلى "الزر غير موجود" من منظور المستخدم. الاستثناء الجزئي: أزرار `WorkspaceActionBar` تُعرض `disabled` مع `title` يشرح السبب (`disabledReasonAr`) بدل الإخفاء الكامل — نمط أفضل، لكن غير معمَّم خارج إطار Workspace.

## Pagination

نمطان متوازيان:
1. **صفحة/حجم صفحة يدوي بسيط** (زر "التالي"/"السابق"، لا رقم صفحة قابل للنقر مباشرة) — الأشيع في كل القوائم.
2. **`TablePagination`** المكوّن الفرعي الخاص بـ`FormCompliancePage` فقط.

لا حجم صفحة افتراضي موحّد بين المجالات (Notes/CorrectiveActions/Forms تفتَرض 20، بينما Risk الافتراضي 50 من جهة الخادم كما ورد في تدقيق العقود الخلفية) — يظهر هذا التفاوت في سلوك التمرير بين القوائم دون سبب واضح للمستخدم.

## Bulk actions

**لا توجد أي عملية جماعية (Bulk action) في أي صفحة بالتطبيق بأكمله** — لا تحديد متعدد للصفوف، لا "تنفيذ على المحدَّد" في أي قائمة (ملاحظات، إجراءات تصحيحية، استجابات، حملات). هذا يتعارض مع نص #145 الذي يفترض "bulk actions آمنة ومحددة بوضوح" كجزء من التصميم المستهدف — أي عملية جماعية للمستقبل تُبنى من الصفر بلا نمط سابق للاستناد إليه.

## Mobile navigation

**لا يوجد نمط تنقّل موبايل مخصص إطلاقًا.** لا Hamburger menu، لا Bottom navigation، لا سلوك JS مرتبط بحجم الشاشة (لا `matchMedia`/`useMediaQuery`/`window.innerWidth` في أي مكان). كل الاستجابة للشاشات الصغيرة قائمة على CSS media queries فقط (`index.css`، نقاط الانكسار 1100px/720px لصفحة Facility Workspace، 720px للشريط الجانبي العام). زر "رجوع" الخاص بالموبايل (`.mobile-back`) معرَّف في CSS ومكوّن `MasterDetailWorkspaceLayout` لكنه **غير مستخدَم في أي صفحة فعلية بالتطبيق حاليًا**.

## ملخص القرارات

| الفئة | القرار |
| --- | --- |
| Page Header (نمط CommandHeader/WorkspaceHeader) | **توحيد** إلى مكوّن واحد يُستخدم من كل مساحات العمل الحالية والمستقبلية |
| Filter bars وPickers (منطقة/سجن) | **توحيد** — بناء Picker واحد قابل لإعادة الاستخدام بدل 6 تطبيقات منفصلة |
| Section/Tab navigation | **توحيد** بين نمط `ObservationWorkspacePage` و`FacilityWorkspacePage` |
| `WorkspaceShell`/`MasterDetailWorkspaceLayout`/`WorkspaceFilterBar` | **تعميم الاستخدام** — موجودة وجيدة، لكن غير مُستهلَكة إلا في صفحة مرجعية واحدة |
| Metric/KPI cards | **توحيد** تدريجي حول `WorkspaceWidgetContainer` + `Metric` |
| Toasts | **إيقاف الاعتماد الكلي على النص المضمَّن** لصالح نظام إشعار لحظي موحّد (يحتاج قرارًا معماريًا جديدًا صغيرًا، وليس مكوّنًا موجودًا يُعمَّم فقط) |
| Confirmation pattern (سبب نصي بدل نافذة) | **توثيق رسمي** كنمط ملزم بدل تكراره ضمنيًا |
| Empty/Loading/Error/Partial states | **توحيد** حول نمط `WorkspaceLoading/Error/Empty/Unauthorized` + تمييز صريح بين "لا بيانات" و"ميزة غير مبنية" |
| Bulk actions | **بناء نمط جديد** من الصفر (لا شيء موجود لتعميمه) |
| Mobile navigation | **قرار تصميمي مطلوب** قبل أي بناء — إما نمط تنقّل موبايل حقيقي أو توثيق صريح بأن الموبايل "وضع عرض فقط" لبعض المجالات (كما فعل Form Designer ضمنيًا) |
