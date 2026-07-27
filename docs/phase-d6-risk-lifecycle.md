# Phase D.6 — دورة حياة الخطر

المنطق في `Baseera.Application.RiskManagement.RiskLifecycleStateMachine` (منطق صرف، 12 اختبار وحدة)، مطبَّق من قِبل `RiskCommandService`, `RiskAssessmentService`, `RiskReviewService`.

## جدول الانتقالات المسموحة

```
Draft            -> UnderAssessment
UnderAssessment  -> PendingReview
PendingReview    -> Active
PendingReview    -> UnderAssessment      (مراجعة تقييم مرفوضة/معادة)
Active           -> UnderTreatment
Active           -> Monitoring
Active           -> PendingAcceptance
UnderTreatment   -> Monitoring
PendingAcceptance-> Accepted
PendingAcceptance-> Active               (طلب قبول مرفوض)
Accepted         -> PendingReview        (انتهاء مدة القبول / مراجعة دورية)
Monitoring       -> PendingClosure
Monitoring       -> UnderTreatment       (دليل جديد أثناء المتابعة)
PendingClosure   -> Closed
PendingClosure   -> Monitoring           (طلب إغلاق مرفوض/معاد)
Closed           -> Reopened
Reopened         -> UnderAssessment      (إعادة الفتح تستوجب تقييمًا جديدًا دائمًا)
Closed           -> Archived
```

الانتقالات المذكورة صراحة في متطلبات المرحلة كلها موجودة. الحواف الإضافية (الرجوع عند الرفض، `Accepted → PendingReview`، `Reopened → UnderAssessment`) أُضيفت لأن قرارات المراجعة (Returned/Rejected) يجب أن "تذهب إلى مكان ما" منطقيًا — موثَّقة هنا صراحة وليست إضافة صامتة. الأرشفة مسموحة فقط من `Closed` (خطر يجب أن يُحل بالكامل قبل أرشفته).

## من يُشغّل كل انتقال؟

| الانتقال | المُشغِّل |
| --- | --- |
| `Draft → UnderAssessment` | إنشاء أول تقييم (`IRiskAssessmentService.CreateAsync`) |
| `UnderAssessment → PendingReview` | إرسال تقييم Inherent/Current للمراجعة (`SubmitAsync`) |
| `PendingReview → Active` / `→ UnderAssessment` | اعتماد/رفض مراجعة التقييم (`ApproveAsync`/`ReviewAsync`) |
| `Active → UnderTreatment` | اعتماد خطة معالجة (`IRiskTreatmentService.ExecutePlanCommandAsync("Approve")`) |
| `Active/UnderTreatment → Monitoring` | أمر مباشر `StartMonitoring` (`Risks.Update`) |
| `Active → PendingAcceptance` / `PendingAcceptance → Accepted/Active` | طلب/قرار قبول (`IRiskReviewService`، نوع `RiskAcceptance`) |
| `Accepted → PendingReview` | طلب مراجعة دورية عند اقتراب/انتهاء مدة القبول |
| `Monitoring → PendingClosure` / `PendingClosure → Closed/Monitoring` | طلب/قرار إغلاق (`IRiskReviewService`، نوع `ClosureApproval`) |
| `Closed → Reopened → UnderAssessment` | أمر مباشر `Reopen` (بمبرر/دليل إلزامي) |
| `Closed → Archived` | أمر مباشر `Archive` |

## الضوابط الحاكمة (مطبَّقة، ليست وثائق فقط)

1. **Activation تحتاج Assessment معتمد**: `PendingReview → Active` لا يحدث إلا داخل `ApproveAsync` بعد اعتماد تقييم Inherent/Current فعليًا.
2. **UnderTreatment تحتاج Treatment Plan معتمدة**: الانتقال يحدث فقط داخل `ExecutePlanCommandAsync("Approve")` بعد نجاح فصل المهام.
3. **Acceptance تحتاج**: مبرر (`Comments` غير فارغ)، معتمد مختلف عن الطالب (four-eyes)، تاريخ `AcceptedUntilUtc` مستقبلي، ومدة مراجعة دورية (`ReviewFrequencyDays > 0`) — تُرفض جميعها بـ 409 عند غيابها (`RiskReviewService.RequestAsync`).
4. **PendingClosure تحتاج**: تقييمًا متبقيًا (`Residual`) **معتمدًا** موجودًا فعليًا (`risk.CurrentResidualAssessmentId` + `AssessmentStatus.Approved`) قبل قبول طلب الإغلاق، وإلا 409. مُختبر تكامليًا (`Closure_requires_approved_residual_assessment...`).
5. **Closure تحتاج اعتمادًا**: لا يتحول الخطر إلى `Closed` إلا داخل `RiskReviewService.DecideAsync` بقرار `Approved`/`ApprovedWithConditions` من طرف مختلف عن طالب الإغلاق.
6. **Reopen يحتاج مبررًا/دليلًا**: `RiskCommandTypes.Reopen` يرفض الطلب بلا `Reason`.
7. **لا يمكن حذف خطر Active أو Closed**: لا يوجد أي endpoint حذف (DELETE) لسجل الخطر أصلًا في هذه المرحلة — القيد محقَّق ببساطة بغياب القدرة، وموثَّق كقرار تصميمي وليس سهوًا (`RiskLifecycleStateMachine.IsDeletable` موجودة للاستخدام المستقبلي إن أُضيف حذف للمسودات فقط).

## فصل المهام (Four-eyes)

مطبَّق عبر `RiskServiceBase.EnforceFourEyes(submittedBy)` الذي يقارن `ActorReference()` الحالي بمن قام بالتقديم/الإنشاء، ويرمي 409 عند التطابق. يُطبَّق على: اعتماد التقييم (مراجعة + اعتماد)، اعتماد خطة المعالجة، التحقق من إجراء المعالجة، وقرارات القبول/الإغلاق. **لا يوجد** تفريق حسب Risk Rating Band في هذه المرحلة (كل الحالات تُطبَّق بنفس الصرامة بغض النظر عن الدرجة) — هذا تبسيط متعمد موثَّق، وليس المطلوب الكامل في الطلب الأصلي ("اجعل السياسة قابلة للتهيئة حسب Risk Rating Band") الذي بقي **غير منفَّذ** (انظر Compliance Ledger).
