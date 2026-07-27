# البنية المستهدفة للمعلومات — UX Rescue Phase 0

## مستويات التنقّل

| المستوى | التمثيل الحالي | التمثيل المستهدف |
| --- | --- | --- |
| 1. Global | لا يوجد تمييز — الشريط الجانبي يعرض كل شيء مسطّحًا | يبقى شريط تنقّل عام واحد، لكن مبوَّب مفاهيميًا حسب المستوى التالي |
| 2. Headquarters | غير موجود | مستقبلاً: `/workspaces/headquarters` (بعد #13) |
| 3. Region | `RegionsPage` (جدول مسطّح بلا Drill-down) | مستقبلاً: `/workspaces/regions/:regionId` (بعد #12)، مع `RegionsPage` كدليل يقود إليها |
| 4. Facility | `/workspaces/facilities/:facilityId` (بلا رابط شريط جانبي مباشر) | نفس المسار، لكن رابط شريط جانبي مباشر + مدخل من كل من Region Workspace وDashboard |
| 5. Facility Unit | لا سياق مستقل اليوم؛ الوحدات تُعرض كقائمة فرعية فقط | حقل سياق اختياري ضمن Facility Workspace (وليس مستوى Route منفصل بالضرورة) |
| 6. Domain workspace | `ObservationWorkspacePage` (الوحيدة الحقيقية اليوم) | يُضاف: Form Operations Workspace (#145)، مع بقاء Observation Workspace كنموذج مرجعي بعد إعادة بنائه (#143) |
| 7. Entity context | متفاوت — أحيانًا Route كامل (`/notes/:id`)، أحيانًا Panel (`?panel=&entityId=`) | موحَّد: Context Panel كافتراضي، Route كامل فقط لدخول مباشر خارجي (روابط دائمة/إشعارات) |

## الفرق بين الأنواع الخمسة

| النوع | التعريف المستهدف | أمثلة حالية مطابقة | أمثلة حالية مخالفة |
| --- | --- | --- | --- |
| **Workspace** | نقطة البداية التشغيلية اليومية لمجال أو نطاق تنظيمي؛ تجمع الحالة والأولويات والإجراءات في مكان واحد | `FacilityWorkspacePage` (بعد إعادة تبنّي `WorkspaceShell`)، `ObservationWorkspacePage` (بعد #143) | لا يوجد Region/Headquarters Workspace بعد |
| **Operations workspace** | مساحة عمل مخصصة لدورة تشغيلية محددة عابرة لعدة كيانات مرتبطة (وليس نطاقًا تنظيميًا) | لا يوجد اليوم | الهدف: Form Operations Workspace (#145) بدل تشتت الحملات/الدورات/الاستجابات/الالتزام عبر 9 Routes منفصلة |
| **Management/admin page** | إعداد وتكوين، ليس تشغيلًا يوميًا | `UsersPage`, `AuditPage`, `/settings/forms-governance`, صفحات التصعيد | `AttachmentsPage` تقع بينهما فعليًا (رفع عام إداري الطابع لكنه يُستخدَم تشغيليًا) — يحتاج توضيح تصنيفه لاحقًا |
| **Settings** | تفضيلات/قواعد نادرة التغيير | `/settings/note-types`, `/settings/note-routing`, `/settings/escalations` | — |
| **Advanced fallback** | شاشة تقنية/نادرة تبقى Route مستقلة لأنها لا تناسب التدفق اليومي أو تخدم حالة استثنائية | `FormVersionSnapshotPage`, `CorrectiveActionsListPage` (كقائمة شاملة عابرة للملاحظات) | — |
| **Context Panel** | إجراء أو فهم سريع لعنصر واحد ضمن سياق Workspace | `NotePanel`, `RiskPanel` (يدعمان إجراءات فعلية) | `CorrectiveActionPanel`, `SensitiveCustodyPreviewPanel` (قراءة فقط اليوم، يجب أن تدعم الإجراء متى كان ممكنًا) |
| **Full detail route** | عمل معقد أو مراجعة متخصصة تحتاج مساحة كاملة ووقتًا مخصصًا | `FormDesignerPage` (تصميم Schema)، `FormVersionReviewPage` | `NoteDetailPage`/`CorrectiveActionDetailPage` تُستخدَمان اليوم كـFull route لمهام بسيطة (تكليف، إغلاق) لا تستحق صفحة كاملة أصلاً — هذا هو جوهر الخلل الذي يعالجه #143 |

## القواعد الملزمة للتنفيذ القادم

1. **Workspace هي نقطة البداية التشغيلية** — أي مجال جديد (Form Operations، مستقبلاً Region/Headquarters) يجب أن يُصمَّم كـWorkspace أولاً، لا كمجموعة صفحات CRUD منفصلة تُجمَّع لاحقًا.
2. **الشاشات العامة لا تكرر Workspace** — `OperationalDashboardPage` يجب أن تتوقف عن تكرار بيانات Facility Workspace بلا تكامل تنقّل (F-24)؛ إما دمج أو Drill-down مباشر.
3. **Admin pages للإعداد والتكوين، لا التشغيل اليومي** — صفحات التصعيد الأربع تبقى هنا، لكن يجب أن تحصل على تحقق صلاحية داخلي حقيقي (F-10) بدل الاعتماد فقط على إخفاء رابط التنقّل.
4. **Context Panel للإجراء أو الفهم السريع** — `CorrectiveActionPanel` و`SensitiveCustodyPreviewPanel` يجب أن تدعما فعليًا كل إجراء ممكن الوصول إليه من `AllowedActions`، لا قراءة فقط بافتراض أن "الإجراءات المركبة تحتاج الصفحة الكاملة" كقاعدة عامة غير مبرَّرة لكل حالة.
5. **Full detail route فقط للعمل المعقد أو المراجعة المتخصصة** — `NoteDetailPage`/`CorrectiveActionDetailPage`/`NoteEditPage`/`NoteCreatePage` يجب أن تُدمَج ضمن Observation Workspace (#143)؛ لا تستحق أيٌّ منها البقاء كصفحة كاملة منفصلة وفق هذا التعريف.
6. **لكل Domain مدخل واحد أساسي واضح** — الملاحظات لديها هذا فعليًا (`/notes/workspace`، بعد إزالة تكرار `/notes`)؛ النماذج **لا تملك هذا اليوم** (مدخلان منفصلان: `/forms/new` و`/form-templates`) ويجب توحيدهما ضمن #144؛ تشغيل النماذج مُشتَّت بين 9 Routes منفصلة ويحتاج مدخلاً واحدًا ضمن #145.
7. **لا توجد Route مستقلة لكل حالة من الكيان** — القفل في Form Designer يجب أن يُعرَض كحالة داخل نفس الـRoute (Canvas للقراءة فقط)، لا حجب الصفحة بالكامل وتحويل المستخدم لصفحة مختلفة بحسب الحالة.
8. **نفس العنصر يجب ألا يملك واجهتين تشغيليتين متعارضتين** — الملاحظة اليوم تُدار من ثلاث واجهات متوازية (`ObservationWorkspacePage`, `NoteDetailPage`, ولوحة `NotePanel` في Facility Workspace) بقدرات متفاوتة لكل منها؛ الهدف: واجهة واحدة (Panel/Split View) تُستهلَك من كل نقاط الدخول الثلاث.

## خريطة Route مستهدفة (نصية)

```
/                                          → إعادة توجيه حسب آخر نطاق مُستخدَم (مستقبلاً)، أو /regions كافتراضي

/regions                                   → دليل مناطق (يقود إلى Region Workspace بعد #12)
/workspaces/regions/:regionId              → [مستقبلي] Region Workspace (#12)

/facilities                                → دليل سجون (يقود إلى Facility Workspace) + رابط شريط جانبي مباشر جديد
/workspaces/facilities/:facilityId         → Facility Workspace (يتبنّى WorkspaceShell فعليًا)
  ?section=occupancy|resources|workforce|… → أقسام مدموجة (بعد #146) بدل Routes منفصلة، أو على الأقل فلاتر محفوظة عبر التنقّل إن بقيت Routes مستقلة مؤقتًا
  ?panel=<type>&entityId=<id>              → Context Panel لأي كيان (ملاحظة/إجراء/خطر/عهدة/مورد/…)

/workspaces/headquarters                   → [مستقبلي] Headquarters Workspace (#13)

/notes/workspace                           → Observation Workspace (بعد إزالة تكرار /notes وإدماج كل شاشات دورة الحياة، #143)
  ?noteId=&panel=&tab=                     → لا Routes منفصلة لإنشاء/تعديل/تفاصيل الملاحظة أو الإجراء التصحيحي
/corrective-actions                        → يبقى كـAdvanced fallback (قائمة شاملة عابرة للملاحظات)
/corrective-actions/:id                    → يبقى كرابط دائم قابل للمشاركة، يفتح نفس تجربة الـPanel وليس صفحة منفصلة الشكل

/forms                                     → استوديو موحَّد (#144): إنشاء + تصميم + قوالب + إصدارات + مراجعة، مدخل واحد
/form-templates                            → يُدمَج كخطوة بداية داخل /forms، لا Route مستقل بمنطق منفصل
/forms/:formId/versions/:versionId/snapshot → يبقى Advanced fallback (JSON خام للتدقيق)
/forms/:id/access                          → يبقى منفصل (إداري، ليس تأليفيًا)

/form-operations                           → [مستهدف #145] Form Operations Workspace موحَّدة تستوعب:
  محتوى /form-campaigns, /form-campaigns/:id/{cycles,preview}, /my-form-responses,
  /form-assignments/:id/respond, /form-response-reviews, /form-responses/:id/review
/form-compliance(/regions/:id|/facilities/:id|/cycles/:id) → يبقى كما هو (نمط مرجعي ناجح)، يُدمَج كقسم ضمن Form Operations Workspace

/settings/*                                → يبقى كما هو (Admin/Settings)، مع إضافة تحقق صلاحية داخلي لصفحات التصعيد
/users, /audit, /notifications, /attachments → تبقى كما هي (Keep)

/403                                        → [جديد] صفحة موحَّدة بدل رسائل نصية متفرقة لكل صفحة
* (catch-all)                              → [جديد] صفحة 404 موحَّدة بدل شاشة فارغة
```

## ملخص الأثر على الأعداد

- Routes مباشرة تُحذَف/تُدمَج (بعد التنفيذ الكامل لـ#143/#144/#145، خارج نطاق هذا الـPR): `NotesListPage` (يتيمة)، `/forms/:formId/versions/new` (ميت)، `/form-campaigns/:campaignId/targeting|schedule` (ميتان)، وحوالي 12 Route ستُدمَج ضمن Workspace موحَّدة بدل صفحات كاملة منفصلة (`/notes/new`, `/notes/:id`, `/notes/:id/edit`, `/notes/:noteId/corrective-actions/new` جزئيًا، `/corrective-actions/:id` كتجربة شكل، `/corrective-actions/:id/edit`, وروابط تشغيل النماذج التسعة).
- Routes جديدة مطلوبة: `/workspaces/regions/:regionId` (#12)، `/workspaces/headquarters` (#13)، صفحة 403 موحَّدة، صفحة 404 موحَّدة، ومسار Form Operations Workspace الموحَّد (#145).
