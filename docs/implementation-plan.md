# خطة التنفيذ — منصة دعم اتخاذ القرار والإشراف التشغيلي

## المبادئ

- Modular Monolith؛ لا Microservices في الإصدار الأول.
- صلاحيات ونطاقات على الخادم فقط؛ الواجهة للعرض لا للحماية.
- لا hard delete للسجلات التشغيلية الحساسة.
- كل تعديل جوهري يُسجَّل في AuditLog.
- لا انتقال لمرحلة تالية قبل نجاح بناء واختبارات المرحلة الحالية.

## المرحلة A — التأسيس

- توثيق الفجوة والخطة ومصفوفة الصلاحيات وقاموس الحالات.
- طبقات Domain / Application / Infrastructure / Api / Reporting / BackgroundJobs.
- الهيكل التنظيمي، المستخدمون، الأدوار، الصلاحيات، النطاقات.
- Microsoft Entra ID + TestAuth للاختبارات.
- AuditLog append-only، خدمة مرفقات، اختبارات عزل النطاق.

**بوابة الخروج:** `dotnet build` + اختبارات عزل/تدقيق/مرفقات خضراء + واجهة RTL حد أدنى مربوطة بـ API حقيقي.

## المرحلة B — الملاحظات والتكليفات

وحدة الملاحظات، الإجراءات التصحيحية، التكليفات، التصعيد، لوحة متابعة، تقارير أساسية.

### Phase B.2.1 — Corrective Actions Core

- إجراءات تصحيحية متعددة لكل ملاحظة تشغيلية.
- State Machine مستقلة مع RowVersion وHTTP 409 للتعارض والانتقال غير الصالح.
- النطاق مشتق من الملاحظة الأصلية عبر `ICorrectiveActionScopeService`.
- التكليف وإعادة التكليف يحتفظان بسجل سابق ولا يسمحان إلا بتكليف حالي واحد.
- حارس يمنع إغلاق أو إلغاء الملاحظة إذا بقيت إجراءات تصحيحية نشطة.
- لا يشمل التصعيد الآلي أو الإشعارات أو التقارير أو Phase B.2.2.

### Phase B.2.2 — Escalations and In-App Notifications Core

- تصعيد DueSoon وOverdue للملاحظات والإجراءات التصحيحية.
- سياسات وقواعد تصعيد قابلة للتفعيل والتعطيل.
- إشعارات داخلية فقط عبر قناة `InApp`.
- Inbox، عداد غير المقروء، قراءة وأرشفة.
- Background worker قابل للتعطيل مع SQL lease وidempotency.
- لا يشمل Email/SMS أو Dashboard أو تقارير أو Phase B.2.3.

### Phase B.2.3.1 — Note Types, Effective Access and Intake Locking

- أنواع الملاحظات أصبحت بيانات قابلة للإدارة عبر `NoteType`.
- صلاحيات النوع تُحسب من Grants الدور واستثناءات المستخدم المباشرة.
- إنشاء الملاحظة يبدأ بالمنطقة ثم السجن ثم نوع الملاحظة.
- Intake profile يثبت منطقة أو سجن إدخال دون منح نطاق عرض جديد.
- التكليف والمراجعة يستخدمان قوائم مؤهلين مبنية على RBAC + Scope + Type Access + Classification.
- لا يشمل Auto Routing أو Dashboard أو Reports.

### Phase B.2.3.2 — Note Routing and Automatic Assignment

- قواعد توجيه للملاحظات حسب `NoteType` والنطاق الجغرافي.
- التوجيه عند إرسال الملاحظة وفتحها، مع سجل قرارات Append-Only.
- التكليف التلقائي إلى إدارة أو اختيار مستخدم مؤهل من دور محدد.
- أولوية الاستحقاق: التاريخ المدخل من المستخدم، ثم قاعدة التوجيه، ثم نوع الملاحظة، ثم دون تاريخ.
- سجل Append-Only لتغييرات قواعد التوجيه ومنح أنواع الملاحظات.
- مؤشرات تشغيلية محدودة لفاعلية التوجيه دون تصدير أو Dashboard عام.
- تم قبول B.2.3.2 ودمجها في `main`؛ Dashboard يبدأ في B.3.1.

### Phase B.3.1 — Operational Decision Dashboard Core

- أول لوحة متابعة تشغيلية حقيقية تحوّل الملاحظات والتكليفات والإجراءات التصحيحية والتصعيدات والتوجيه إلى مؤشرات وقوائم.
- تطبيق Scope + Effective Note Type Access + Classification access + soft-delete على الخادم.
- صلاحيات Dashboard مخصصة (`Dashboard.ViewOperational`, `ViewRisk`, `ViewRouting`, `ViewCorrectiveActions`).
- Endpoints مجمّعة SQL-side مع drill-down إلى قوائم التفاصيل بنفس الفلاتر.
- اتجاهات زمنية 7/30/90 يومًا بحدود يوم Riyadh.
- لا Export أو Report Builder أو Email/SMS أو AI أو Phase C+.

## المرحلة C — منشئ النماذج

تصميم، إصدارات، نشر، دورات، تعبئة، مراجعة، متأخرات، إشعارات.

### Phase C.1 — Forms Governance & Security Foundation (Issue #52, Epic #45)

- نموذج `FormDefinition` مع دورة حياة حوكمة (مسودة → مراجعة → اعتماد/رفض → أرشفة/استعادة).
- `FormReviewDecision` سجل قرارات Append-Only؛ `FormGovernancePolicy` singleton؛ `FormAccessGrant` مع Allow/Deny.
- صلاحيات C.1 كاملة (`Forms.View` … `Forms.ManageGovernance`) وأدوار متخصصة (FormDesigner, FormReviewer, FormApprover, …).
- Scope + Classification + Grants + SoD + Audit على الخادم؛ 404 للخارج عن النطاق.
- API `/api/v1/forms` وواجهة RTL (7 صفحات).
- لا يشمل مصمم الحقول، النشر، التعبئة، التصدير، أو Issue #46–#51.

### Phase C.2 — Versioned Drag-and-Drop Form Designer (Issue #46, Epic #45)

- `FormVersion` + immutable `FormSchemaSnapshot` + templates.
- Typed schema AST, conditions, formulas, cycle detection, canonical SHA-256.
- Atomic per-form version counters (`FormDefinitionVersionCounters` + MERGE).
- Designer UI with DnD, autosave flush before submit, Undo/Redo, preview.
- Version history gated by `Forms.ViewVersionHistory` (+ View Deny → 404).
- Does **not** include publish/responses (Issue #47 not started).

### Phase C.3 — Form Publishing, Targeting & Recurrence Scheduler (Issue #47, Epic #45)

- FormCampaign + cycles + frozen FacilityAssignments; locked version + snapshot pinned.
- Targeting/exclusions/preview via shared resolver; idempotent multi-instance scheduler.
- Does **not** include FormResponse (#48) or reminders/notifications (#50).

**التالي:** Phase C.4 / Issue #48 — form responses & fill workspace.

## المرحلة D — الموارد والتشغيل

مركبات، قوى عاملة، خطط، أحداث ووقائع.

## المرحلة E — المشاريع والاستراتيجية

مشاريع متعددة المواقع، مبادرات، أهداف، مؤشرات، مستهدفات، لوحة أداء.

## المرحلة F — الوحدات الحساسة والتكامل

متابعة نزلاء (قراءة مرجعية + متابعة محلية)، تسليح، تدقيق متقدم، جودة بيانات.

## المرحلة G — دعم القرار

ملفات قرارات، بدائل، آثار، تكليفات ناتجة، قياس أثر، تقارير قيادية.

## معايير قبول الوحدة

قاعدة بيانات حقيقية، CRUD مكتمل، صلاحيات ونطاق على الخادم، تدقيق، اختبارات، لا Mock إنتاجي، حالات تحميل/خطأ/فراغ في الواجهة، إمكانية تتبع المؤشرات، وثائق تشغيل.
