# Phase 1A — النطاق (Observation Workspace Foundation and Core Operational Flow)

هذا المستند يثبّت نطاق Phase 1A كما نُفِّذ فعليًا، بالإحالة على `docs/ux-rescue/rescue-roadmap.md` (المرحلة 1) و`docs/ux-rescue/observation-workspace-gap-analysis.md` كمصدر القرار الأصلي. لا يُعاد هنا اشتقاق قرارات UX Rescue Phase 0 — فقط تثبيت ما طُبِّق منها في هذه الدفعة تحديدًا.

## الهدف الوحيد القابل للقياس

> "النجاح هو أن يتمكن المستخدم من الدخول من مساحة السجن، فتح الملاحظة، فهمها، تنفيذ الإجراء الأساسي، الانتقال للتالية، والعودة دون فقد السياق أو الحاجة إلى التنقل بين صفحات متعددة."

كل قرار نطاق أدناه يُقاس بهذا المعيار، لا بعدد الشاشات المُعاد رسمها.

## داخل النطاق (منفَّذ فعليًا)

1. هيكل Observation Workspace موحَّد على `/notes/workspace` — قائمة+تفاصيل حقيقي (Master-detail)، لا Modal كامل فوق التفاصيل، لا Card-in-Card.
2. حالة URL موحَّدة: `facilityId, facilityUnitId, status, severity, noteType, due, search, sort(By/Desc), page, view, noteId, section, source` (تفصيل كامل في `phase1a-observation-workspace-contract.md`؛ `owner`/`assignee` مدعومان في عقد الفلترة الخلفي دون Selector مخصص في هذه الدفعة — أنظر الفجوات).
3. Deep links تعمل: فتح ملاحظة، تصفية بسجن/وحدة، تحديث الصفحة يستعيد نفس الحالة (فلاتر + القسم المفتوح + noteId).
4. سياق تنظيمي (Facility/FacilityUnit) في القائمة والفلاتر، ووراثته الآمنة عند الإنشاء من `workspaces/facilities/:facilityId`.
5. إنشاء ملاحظة من داخل Facility Workspace (Panel مضمَّن `note-create`)، السجن غير قابل لإعادة الاختيار، الوحدة قابلة للتغيير ضمن نفس السجن فقط، الخادم يعيد اشتقاق/التحقق من `FacilityId` دائمًا.
6. عرض تفاصيل موحَّد بخمسة أقسام حسب المهمة: الملخص، المعالجة، التكليف، الأدلة، السجل الزمني — لا جداول، ولا تبويبات دائمة الفراغ.
7. العمليات الأساسية المدعومة حاليًا داخل الـWorkspace: Submit، Assign/Reassign (نموذج Inline بمُنتقي مكلَّف)، StartWork، RequestVerification، RejectVerification، VerifyClosure (نموذج Inline بملخص إغلاق)، Reopen، Cancel، إضافة مرفق. عملية "إضافة إجراء تصحيحي" (`ADD_ACTION`) تبقى Fallback متقدّم مقصود (رابط لصفحة كاملة) — إعادة بناء الإجراءات التصحيحية بالكامل خارج النطاق.
8. تنقّل سابق/تالي ضمن نافذة النتائج المحمَّلة فعليًا (لا تحميل كل المعرّفات)، مع مؤشر الموضع، بلا فقد الفلاتر أو موضع القائمة.
9. حالات تحميل/فراغ/خطأ موحَّدة عبر `src/frontend/src/shared/workspaces/WorkspaceStateView.tsx` (بداية أساس مشترك، ليس مكتبة ضخمة).
10. Rollback على مستوى حلّ الـRoute نفسه عبر `VITE_OBSERVATION_WORKSPACE_V2` — لا مجرد إخفاء رابط شريط جانبي؛ Legacy (`NotesListPage`/`NoteDetailPage`) يبقى مسجَّلاً وقابلاً للعمل الفعلي عند تعطيل العلم.
11. توافق روابط قديمة: `/notes` و`/notes/:id` يعملان (Navigate على مستوى Route، ليس 301/302 HTTP — أي إعادة توجيه HTTP لاحقة قرار Server/CDN منفصل يُوثَّق حين يُطلَب).
12. مراجعة Backend الفعلي قبل أي إضافة (`phase1a-observation-implementation-gap.md`)، وإصلاحات مستهدَفة بدل بناء موازٍ.
13. اختبارات Backend/Frontend جديدة تغطي كل ما سبق (`phase1a-observation-test-matrix.md`).
14. حذف/تحييد المكوّنات اليتيمة المؤكَّدة فقط بعد التحقق — `NotesListPage.tsx` تبيَّن أنه **ليس يتيمًا بعد الآن** (أصبح Legacy fallback مستخدَمًا فعليًا)، فلم يُحذَف؛ التبويبات الدائمة الفراغ (الموارد/الروابط/القرارات) حُذفت لأنها كانت فعليًا بلا Domain خلفها ولا مسار استخدام.
15. توثيق كامل + Compliance Ledger + تقرير إنجاز.

## خارج النطاق (بصريح النص، لم يُنفَّذ هنا)

- إعادة بناء الإجراءات التصحيحية بالكامل (تبقى صفحة كاملة Fallback).
- نقل كل عمليات التصعيد إلى الـWorkspace (لا صلة لهذه الدفعة بـ#19 عمليًا، فقط لا تتعارض معه).
- إدارة قطع الغيار/الموارد.
- تعليقات جماعية تعاونية جديدة (Collaboration) لا Domain لها.
- قرارات/روابط بلا Domain حقيقي (تمامًا كما حُذفت تبويبات الموارد/الروابط/القرارات، لم تُستبدَل بأي شيء جديد بلا Domain).
- حذف كل الصفحات القديمة دفعة واحدة (Big Bang) — `NoteCreatePage`/`NoteDetailPage`/`NoteEditPage`/`NotesListPage` تبقى مسجَّلة كـLegacy fallback.
- Region Workspace / Headquarters Workspace.
- إعادة تصميم Form Designer.
- أي تغيير في Domain/الصلاحيات غير مثبَت الحاجة له (لم يحدث فعليًا هنا — صفر صلاحيات جديدة، صفر Migration).
- Database migration (لم تُحتَج؛ كل الإصلاحات كانت على مستوى Application/Presentation فقط عدا تفعيل بنية تحتية Domain موجودة أصلًا).
- إعادة تصميم Facility Workspace بالكامل (تمت إضافة زر وPanel واحد فقط، لا أكثر).
- لقطات شاشة كمعيار قبول — لم تُستخدَم في أي مرحلة من هذا العمل.

## فجوات معروفة صراحة (ليست "منتهية"، مؤجَّلة لـ1B/1C)

- `owner`/`assignee` كمعاملات URL مدعومة في عقد الفلترة الخلفي لكن بلا Selector واجهة مخصَّص في هذه الدفعة (لا مُتصفِّح مستخدمين/أقسام جاهز لإعادة استخدامه بأمان الآن).
- "قبول التكليف" و"تصعيد فوري من الملاحظة": لا يوجد أمر Domain مستقل لهما إطلاقًا (ليس نقصًا في الواجهة) — Not Applicable، موثَّق في `phase1a-observation-implementation-gap.md`.
- توحيد كامل لمكوّن المرفقات (رفع+قائمة+تنزيل) عبر كل السياقات (Notes/CorrectiveActions/غيرها) — نُفِّذ الرفع Inline للملاحظات فقط في هذه الدفعة.
- فحص Keyboard-only end-to-end كامل (Section 16) لم يُنفَّذ كسيناريو اختبار آلي مخصَّص واحد يغطي التدفق بأكمله؛ التغطية الحالية جزئية (aria-live، aria-pressed، تركيز الحوار الموجود مسبقًا في Facility Workspace).
