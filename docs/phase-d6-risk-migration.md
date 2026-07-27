# Phase D.6 — الترحيل (Migration)

## ملف واحد منظَّم

`20260727052010_PhaseD6EnterpriseRiskRegister` — الأولى بعد `20260726122846_PhaseD52SensitiveCustodyReadiness`. لم تُعدَّل أي هجرة سابقة.

## الجداول المُنشأة (17 جدولًا)

`RiskCategories`, `RiskRecords`, `RiskStatusHistories`, `RiskAssessmentMatrices`, `RiskLikelihoodLevels`, `RiskImpactDimensions`, `RiskImpactLevels`, `RiskRatingBands`, `RiskAssessments`, `RiskAssessmentImpacts`, `RiskControls`, `RiskTreatmentPlans`, `RiskTreatmentActions`, `RiskSourceLinks`, `RiskReviews`, `RiskImportBatches`, `RiskReconciliationRecords`.

بالإضافة إلى تسلسل (Sequence) جديد: `RiskRecordReferenceSequence` لتوليد رموز `RSK-00000001` تصاعديًا (بنفس نمط `CorrectiveActionReferenceSequence`).

## القيود المُطبَّقة فعليًا

* `RiskRecords`: رمز فريد لكل منظمة (`OrganizationId, RiskCode`)، قيد CHECK لعدد إعادة الفتح غير السالب، قيد CHECK يفرض وجود `AcceptedUntilUtc` عند `Status=Accepted`، وقيد CHECK يفرض وجود `ClosedAtUtc`/`ClosedBy`/`ClosureReason` معًا عند `Status=Closed`.
* `RiskAssessmentMatrices`: فريد لكل (منظمة، رمز، إصدار)، وفهرس فريد مُصفَّى يضمن **مصفوفة افتراضية نشطة واحدة كحد أقصى لكل منظمة**، وقيد CHECK يفرض وجود أوزان عند اختيار صيغة الأثر الموزون.
* `RiskAssessments`: قيد CHECK للدرجة غير السالبة، فهرس مركّب (RiskRecordId, AssessmentType, Status, ApprovedAtUtc) لتسريع استعلام "آخر تقييم معتمد".
* `RiskTreatmentPlans`: قيد CHECK يفرض وجود `ApprovedBy` عند `ApprovalStatus=Approved`.
* `RiskTreatmentActions`: قيد CHECK يمنع اعتمادية ذاتية مباشرة (`DependencyActionId ≠ Id`).
* `RiskReviews`: قيد CHECK يفرض وجود `Decision` عند `Status=Completed`.
* `RiskImportBatches`: فريد لكل (منشأة، نوع استيراد، Hash الملف) — أساس الـ Idempotency.
* حماية Append-only لجدول `RiskStatusHistories` عبر `RiskStatusHistoryAppendOnlyGuard` مسجَّل في كل من `SaveChanges`/`SaveChangesAsync` و`AuditImmutabilityInterceptor`.
* فهارس تصفية Soft-delete (`[IsDeleted] = 0`) على كل فهرس فريد، وفلاتر استعلام عامة (`HasQueryFilter`) مضافة يدويًا في `BaseeraDbContext.OnModelCreating` لكل الجداول السبعة عشر.

## التحقق الفعلي (وليس افتراضًا)

* `dotnet ef migrations add` نجحت بلا أخطاء.
* `dotnet ef database update` طُبِّقت **فعليًا** على قاعدة بيانات SQL Server 2022 حقيقية (حاوية Docker `mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04`، نفس صورة CI) من الصفر عبر كامل سلسلة الهجرات التاريخية (23+ هجرة سابقة) وصولًا لهذه الهجرة، دون أي خطأ.
* 12 اختبار تكامل حي يمارسون فعليًا القراءة/الكتابة عبر هذا المخطط (إنشاء، تحديث، انتقال حالة، قيود CHECK، فهارس فريدة) — راجع `phase-d6-risk-test-matrix.md`.
