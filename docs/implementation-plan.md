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

### Phase D.0 — Workspace Framework Foundation (Issue #10)

- إطار Workspace مشترك لمستويات Facility وRegion وHeadquarters وDomain.
- Widget registry عبر DI مع منع المفاتيح المكررة وفصل Core عن تفاصيل الوحدات.
- API محدود `/api/v1/workspaces/*` مع صلاحيات ونطاق على الخادم.
- مكونات واجهة RTL مشتركة وRoute مرجعي feature-flagged يستخدم بيانات dashboard حقيقية.
- لا ينفذ Facility/Region/Headquarters Workspace الكاملة (#11-#13)، ولا persistence للـpersonalization (#21).

### Phase D.1 — Facility Workspace MVP (Issue #11 جزئيًا)

- مساحة `facility-operations` على مستوى السجن فقط فوق Workspace Framework.
- تعرض تعريف السجن، الملخص التشغيلي، مؤشرات الملاحظات، الإجراءات التصحيحية، التصعيدات، التزام النماذج، قائمة الأولويات، وآخر الأحداث.
- تعتمد على `OperationalDashboardFilterBuilder`, `IFormComplianceQueryService`, وكيانات التصعيد الحالية دون Mock أو migration.
- تؤجل Region/Headquarters Workspaces (#12/#13)، الموارد (#15)، المخاطر (#16)، Timeline الكامل (#18)، Alert Center الكامل (#19)، وSaved Views (#21).

### Phase D.2 — Facility Command Center UX (Issue #11 استمرار جزئي)

- إعادة تصميم تجربة `facility-operations` من Dashboard كروت إلى مركز قيادة وتشغيل.
- Command Header، Situation Overview، Operational Pulse، Intervention Queue، Context Panel، وAction Center.
- فتح تفاصيل الملاحظات والإجراءات والتصعيدات والنماذج والأحداث داخل نفس مساحة العمل قدر الإمكان.
- الحفاظ على APIs وصلاحيات ونطاق D.1 دون migration أو Backend بديل.
- لا يشمل Region/Headquarters Workspaces (#12/#13)، ولا محركات AI أو تخصيص محفوظ.

### Phase D.3 — Facility Operations Workspace Expansion and Data Quality Gaps (Issue #11 استمرار)

- توسيع `facility-operations` إلى 12 قسمًا داخليًا يغطي المشهد العام، العمل العاجل، التشغيل، الإشغال، الموارد، المخاطر، المشاريع، الالتزام، الخطط، القرارات، السجل، وجودة البيانات.
- إضافة widget حقيقي لهيكل السجن من الوحدات والمباني ومواقع الأصول، وwidget لجودة بيانات المجالات.
- إبقاء المجالات غير الموجودة Domainيًا كفجوات typed واضحة دون أرقام وهمية أو GenericEntity.
- استمرار Context Panel داخل الشاشة للوحدات والمجالات المتاحة والفجوات، مع بقاء الصفحات الكاملة خيارًا ثانويًا.
- مكتملة تقنيًا ضمن النماذج الحالية وقيد القبول النهائي؛ لا تعني إغلاق Issue #11.
- لا تنفذ كامل محركات الإشغال والموارد والوقوعات والمخاطر والمشاريع والخطط والقرارات؛ تستمر المجالات الناقصة عبر Issues #15 و#16 و#18 و#19 و#124 و#125 و#126 و#127 و#128.
- لا ينفذ Region/Headquarters Workspaces، ولا يضيف migration أو AI أو محركات موارد/مخاطر/مشاريع كاملة.

### Phase D.4 — Facility Occupancy and Inmate Movement (Issue #124, استمرار #11)

- يضيف Domain حقيقيًا للطاقة الاستيعابية وSnapshots الإحصائية وحركات النزلاء غير التعريفية.
- يدمج الإشغال في `facility-operations` كمؤشر حقيقي في النبض التشغيلي، القسم الداخلي، قائمة التدخل، جودة البيانات، والسجل المحدود.
- يضيف إدارة محدودة للإشغال عبر `/facilities/:facilityId/occupancy` لتسجيل الطاقة وSnapshot واستيراد الحركة المنضبط.
- يمنع عرض هوية النزيل في Workspace، ولا يمنح `Workspaces.ViewFacility` حق الإشغال دون صلاحيات `Occupancy.*`.
- لا ينفذ Region/HQ Workspaces، ولا بقية Resource Center خارج الطاقة الاستيعابية، ولا reconciliation workflow كامل.

### Phase D.5 — Facility Resource Readiness Center: Core Assets (Issue #15 جزئيًا، استمرار #11)

- يضيف Domain حقيقيًا للموارد الأساسية عبر `ResourceAsset` وprofiles للمركبات وأجهزة الاتصال والمعدات والأصول الثابتة.
- يفصل الملكية عن الموقع التشغيلي عبر `ResourcePlacement`، ويحفظ تاريخ الحالة عبر `ResourceStatusEvent`.
- يضيف أوامر صيانة وbaseline احتياج resource requirements، ويحسب الجاهزية والفجوات من سجلات مصدرية bounded.
- يدمج الموارد في `facility-operations` داخل قسم الموارد، النبض التشغيلي، قائمة التدخل، Action Center، السجل، وجودة البيانات.
- يضيف صفحة تشغيلية `/facilities/:facilityId/resources` لإدارة الموارد الأساسية دون CRUD عام.
- مكتملة محليًا مع Migration واحدة ولقطات Phase D.5 الفعلية؛ تبقى مراجعة PR وCI/SonarCloud بوابات القبول النهائية.
- لا ينفذ القوى البشرية أو الأسلحة/الذخائر أو العهد الحساسة أو المخزون والمستودعات العامة، ولا Region/HQ Workspaces.
- لا يغلق Issue #15 أو Issue #11؛ هذه دفعة أولى للموارد الأساسية فقط.

### Phase D.5.1 — Facility Workforce Readiness & Duty Coverage (Issue #133، جزئي من #15، استمرار #11)

- يضيف Domain قوى بشرية تشغيلية عبر `WorkforceMember` المستقل عن حساب الدخول `User`، مع `WorkforceRoleDefinition` منفصل عن RBAC.
- يغطي المؤهلات، التكليفات، `StaffingRequirement`، الورديات، جداول المناوبة (Draft/Published)، أحداث التوفر (بدون تشخيص طبي)، والمواقع الحرجة.
- يحسب التغطية والفجوات ومؤشرات الإجهاد/المخاطر بشكل حتمي عبر `WorkforceReadinessPolicy` و`WorkforceFatiguePolicy`، مع لقطات `WorkforceReadinessSnapshot` واستيراد Preview/Confirm محدود وكتالوجات ثابتة للتدخلات وجودة البيانات.
- يدمج القسم `القوى البشرية والتغطية` وwidget `facility.workforce` داخل `facility-operations`، مع صفحة `/facilities/:facilityId/workforce`.
- يفرض صلاحيات `Workforce.*` منفصلة عن `Workspaces.ViewFacility`.
- Migrations: `20260725180933_PhaseD51FacilityWorkforceReadiness` و`20260725203357_PhaseD51WorkforceReconciliationExport`، دون إعادة كتابة تاريخ قاعدة البيانات.
- لا ينفذ الأسلحة/الذخائر، ولا Region/HQ Workspaces، ولا الرواتب/البدلات/الترقيات، ولا الحضور البيومتري الخام، ولا تحسين المناوبات بالذكاء الاصطناعي.
- يغلق Issue #133؛ يبقى Issue #15 مفتوحًا (أسلحة وبقية الشرائح) وIssue #11 مفتوحًا لبقية مجالات مركز القرار.

## المرحلة E — المشاريع والاستراتيجية

مشاريع متعددة المواقع، مبادرات، أهداف، مؤشرات، مستهدفات، لوحة أداء.

## المرحلة F — الوحدات الحساسة والتكامل

متابعة نزلاء (قراءة مرجعية + متابعة محلية)، تسليح، تدقيق متقدم، جودة بيانات.

## المرحلة G — دعم القرار

ملفات قرارات، بدائل، آثار، تكليفات ناتجة، قياس أثر، تقارير قيادية.

## معايير قبول الوحدة

قاعدة بيانات حقيقية، CRUD مكتمل، صلاحيات ونطاق على الخادم، تدقيق، اختبارات، لا Mock إنتاجي، حالات تحميل/خطأ/فراغ في الواجهة، إمكانية تتبع المؤشرات، وثائق تشغيل.
