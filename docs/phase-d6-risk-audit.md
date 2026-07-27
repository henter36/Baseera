# Phase D.6 — التدقيق

## الاصطلاح

`Baseera.Application.RiskManagement.RiskAuditActions` — ثوابت نصية `PascalCase` بصيغة `{كيان}{فعل ماضٍ}`، بنفس اصطلاح `"WeaponRegistered"`/`"CorrectiveActionAssigned"` الموجود مسبقًا (لا enum، لا نمط جديد).

## القائمة الكاملة (مطبَّقة فعليًا في الكود، وليست تصميمًا نظريًا)

`RiskCreated`, `RiskUpdated`, `RiskOwnerAssigned`, `RiskStatusChanged`, `RiskEscalated`, `RiskSourceLinked`, `RiskSourceUnlinked`, `RiskCategoryCreated`, `RiskAssessmentCreated`, `RiskAssessmentSubmitted`, `RiskAssessmentReviewed`, `RiskAssessmentApproved`, `RiskControlCreated`, `RiskControlTested`, `RiskTreatmentCreated`, `RiskTreatmentApproved`, `RiskTreatmentActionChanged`, `RiskAcceptanceRequested`, `RiskAccepted`, `RiskClosureRequested`, `RiskClosed`, `RiskReopened`, `RiskReviewCompleted`, `RiskMatrixCreated`, `RiskMatrixApproved`, `RiskMatrixActivated`, `RiskImportPreviewed`, `RiskImportConfirmed`, `RiskReconciled`.

كل استدعاء يمر عبر `RiskServiceBase.AuditAsync` → `IAuditService.WriteAsync` (نفس الخدمة العامة `AuditService`، بلا جدول تدقيق موازٍ).

## ما لا يُسجَّل أبدًا

* لا ملفات خام أو مرفقات كاملة.
* لا نص المبرر الكامل (`Rationale`) للتقييمات الحساسة — تُسجَّل فقط حقول بنيوية (مثل الدرجة المحسوبة، نوع التقييم، من→إلى للحالة، اسم الأمر المنفَّذ).
* لا بيانات شخصية غير لازمة (فقط `ActorReference()` وهو نفس `ExternalSubject`/`DisplayName` المستخدم في كل الوحدات الأخرى للتدقيق).

## فجوة موثقة: `RiskExported` مُعرَّف لكن غير مُستخدَم

الثابت `RiskAuditActions.RiskExported` معرَّف (تحضيرًا) لكن **لا يُستدعى من أي مكان** في هذه المرحلة، لأنه لا يوجد أي مسار تصدير فعلي بعد (`Risks.Export` صلاحية مُسندة بلا endpoint مقابل — انظر `phase-d6-risk-api-contract.md`). سيُفعَّل عند بناء ميزة التصدير الفعلية.
