# Phase D.6 — نموذج المجال: `Baseera.Domain.RiskManagement`

## القاعدة

جميع الكيانات القابلة للتعديل ترث `SoftDeletableEntity` (Id, RowVersion, CreatedAtUtc/By, UpdatedAtUtc/By, IsDeleted/DeletedAtUtc/DeletedBy) وتطبّق `IScopedEntity` عند الحاجة، تمامًا كبقية الوحدات (`SensitiveCustody`, `Resources`, `Workforce`). السجل الوحيد append-only بالكامل هو `RiskStatusHistory` (صنف بسيط بلا `RowVersion`، يتبع نمط `CorrectiveActionStatusHistory` تمامًا، ومحمي بحارس `RiskStatusHistoryAppendOnlyGuard` مسجَّل في `BaseeraDbContext` وفي `AuditImmutabilityInterceptor`).

## RiskRecord

الحقول الأساسية كما في التصميم المطلوب، بالإضافة إلى حقول قراءة مخزَّنة مؤقتًا (denormalized read cache) لأداء القوائم:

* `CurrentInherentAssessmentId` / `CurrentAssessmentId` / `CurrentResidualAssessmentId` — تُحدَّث **فقط** عند اعتماد تقييم (`IRiskAssessmentService.ApproveAsync`)، وليست مصدر الحقيقة (RiskAssessment نفسه هو المصدر).
* `CurrentScore`, `CurrentRatingBandId`, `CurrentTrend`, `CurrentTrendReasonAr` — تُحدَّث بنفس اللحظة، لتفادي استعلام تقييمات متعددة عند عرض قائمة السجل.
* `RecurrenceKey` — يُحسب عند الإنشاء من (رمز التصنيف + معرّف السجن + العنوان المُطبَّع)، يُستخدم للكشف عن التكرار (`RiskRecurrenceDetector`) دون أي دمج تلقائي.
* `ScopeLevel` (من `Baseera.Domain.Common.ScopeType`، معاد استخدامه بدل enum مواز) — يُخزَّن `Facility` دائمًا في هذه المرحلة، مع حقول `RegionId`/`HeadquartersOrganizationId` جاهزة لمراحل لاحقة دون تعديل المخطط.

`IScopedEntity.RegionId` مُشتق من `Facility.RegionId` إن وُجدت منشأة، وإلا من الحقل المخزَّن `RegionId` مباشرة (تحضيرًا لمخاطر مستوى المنطقة مستقبلًا بلا منشأة).

## المصفوفة (Versioned)

`RiskAssessmentMatrix` + `LikelihoodLevel` + `ImpactDimension` (كتالوج على مستوى المنظمة، قابل لإعادة الاستخدام عبر إصدارات المصفوفة) + `ImpactLevel` (خاص بإصدار مصفوفة محدد) + `RiskRatingBand`. راجع `phase-d6-risk-matrix-versioning.md` و`phase-d6-risk-scoring.md`.

## التقييم

`RiskAssessment` + `RiskAssessmentImpact` (تفصيل لكل بُعد أثر). `SupersedesAssessmentId` يشكّل سلسلة إصدارات مثل `FormVersion.BasedOnVersionId`. راجع `phase-d6-risk-lifecycle.md`.

## الضوابط والمعالجة

`RiskControl` (منفصل تمامًا عن `RiskTreatmentPlan`/`RiskTreatmentAction`). راجع `phase-d6-risk-controls.md` و`phase-d6-risk-treatment-workflow.md`.

## الروابط والمراجعات

`RiskSourceLink` (نمطي، Soft-delete فقط، `RemovalReason` إلزامي عند الإزالة) و`RiskReview` (تحمل `RequestedAcceptedUntilUtc`/`RequestedReviewFrequencyDays` فقط عند `ReviewType.RiskAcceptance`، و`SubjectReferenceType`/`SubjectReferenceId` كمؤشر نمطي للكيان محل المراجعة بدل عمود FK منفصل لكل نوع).

## الاستيراد والمصالحة

`RiskImportBatch` و`RiskReconciliationRecord` يطابقان بنية `SensitiveCustodyImportBatch`/`SensitiveCustodyReconciliationResolution` حرفيًا.

## قرار تصميمي: عدم توسيع محرك Escalations العام

راجع Domain موجود في `Escalations` (يستهدف حاليًا `OperationalNote`/`CorrectiveAction` فقط). تقرر **عدم** إضافة `RiskItem` كنوع هدف جديد في `EscalationTargetType` هذه المرحلة، لتفادي توسيع الأثر على وحدة قائمة تعمل بإنتاج. تصعيد الخطر (`RiskCommandTypes.Escalate`) يُنفَّذ كأمر مباشر مسجَّل في التدقيق فقط. هذا قرار موثَّق قابل لإعادة النظر في مرحلة تجميع المنطقة/الجهاز الرئيسي.
