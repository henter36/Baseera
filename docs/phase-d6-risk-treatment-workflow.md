# Phase D.6 — خطط المعالجة وإجراءاتها

## دورة حياة الخطة (`RiskTreatmentPlanStateMachine`)

```
Draft -> PendingApproval -> Approved -> InProgress -> Completed
Draft/PendingApproval/Approved/InProgress -> Cancelled
PendingApproval -> Rejected
PendingApproval -> Draft (إعادة للتعديل)
InProgress <-> Blocked
```

`Overdue` **ليست** حالة قابلة للوصول عبر أي أمر — هي حالة مُشتقة فقط تُحسب وقت العرض (`RiskTreatmentPlanStateMachine.IsOverdue(status, dueAtUtc, now)`)، مُختبرة بوحدة صريحة تتأكد أن `CanTransition(any, Overdue)` تُعيد `false` دائمًا.

## اعتماد الخطة = فصل مهام، ليس صلاحية منفصلة

لا توجد صلاحية `Risks.ApproveTreatments` منفصلة في القائمة المطلوبة أصلًا؛ الاعتماد يستخدم نفس `Risks.ManageTreatments`، والفصل الفعلي يتحقق عبر فصل المهام (`EnforceFourEyes` مقابل `plan.CreatedBy`) — منشئ الخطة لا يمكنه اعتمادها حتى لو ملك الصلاحية شكليًا. مُختبر تكامليًا صراحة.

## دورة حياة الإجراء (`RiskTreatmentActionStateMachine`)

```
Draft -> Assigned -> InProgress -> PendingVerification -> Completed
InProgress <-> Blocked
PendingVerification -> InProgress (إعادة للعمل)
أي حالة غير نهائية -> Cancelled
```

`Verify` يتطلب `Risks.VerifyTreatmentActions` (مُسندة لدور المعتمد وليس المنفّذ)، وفصل مهام إضافي مقابل مُنفّذ `SubmitForVerification` (`action.UpdatedBy`).

## الاعتمادية (Dependency) بلا دورات

`RiskTreatmentAction.DependencyActionId` يمكن أن يشير فقط إلى إجراء **موجود مسبقًا** في نفس الخطة (يُتحقق عند الإنشاء عبر استعلام وجود). بما أن الإجراء الجديد لا يملك معرّفًا يمكن لإجراء آخر أن يشير إليه بعد (لا يوجد مسار "تعديل الاعتمادية لاحقًا"), فإن رسم الاعتمادية **DAG بالبناء** (Directed Acyclic by construction) — لا حاجة لخوارزمية كشف دورات وقت التشغيل. `Start` على إجراء يملك `DependencyActionId` يُرفض (409) ما لم يكن الإجراء المعتمَد عليه `Completed`.

## دليل الإكمال

`CompletionEvidenceRequired` يُحدَّد عند إنشاء الإجراء. `SubmitForVerification` يرفض (409) إن كان الدليل مطلوبًا و`CompletionSummary` فارغًا.

## إغلاق الخطة لا يعني إغلاق الخطر، والعكس

* إكمال **كل** إجراءات الخطة لا يُكمل الخطة تلقائيًا — أمر `Complete` صريح مطلوب دائمًا، ويُرفض (409) إن بقي إجراء واحد غير `Completed`/`Cancelled`.
* إكمال/إلغاء خطة المعالجة لا يغلق الخطر تلقائيًا — الإغلاق يمر حصرًا عبر `IRiskReviewService` (`ClosureApproval`) مع تقييم متبقٍ معتمد.

## فجوة موثقة

لا يوجد نموذج/API لتعديل `DueAtUtc`/`Priority` لخطة أو إجراء بعد الإنشاء (لا PUT/PATCH منفصل) — فقط أوامر انتقال الحالة. أي تعديل على البيانات الوصفية يتطلب إلغاء وإعادة إنشاء في هذه المرحلة.
