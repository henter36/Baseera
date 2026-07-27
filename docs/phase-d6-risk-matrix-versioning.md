# Phase D.6 — إصدار مصفوفات التقييم

## دورة الحياة

```
Draft --(ApproveAsync، فصل مهام عن المنشئ)--> PendingApproval --(ActivateAsync)--> Active --(تلقائي عند تفعيل بديل افتراضي)--> Retired
```

* **Draft**: تُنشأ بكل صفوفها (Likelihood/Impact/RatingBand) دفعة واحدة عبر `IRiskMatrixService.CreateAsync`. لا يوجد تعديل جزئي بعد الإنشاء داخل هذه المرحلة (كل تصحيح = مصفوفة Draft جديدة عبر `PreviousVersionMatrixId`).
* **PendingApproval**: `ApproveAsync` يتحقق من صلاحية `Risks.ApproveMatrices`، ويطبّق فصل المهام (`EnforceFourEyes` مقابل `CreatedBy`)، ويعيد التحقق من `RiskMatrixValidation.ValidateRatingBands` قبل السماح بالانتقال.
* **Active**: `ActivateAsync` (نفس الصلاحية) يتحقق أن الحالة `PendingApproval`، وإن كانت `IsDefault=true` يقوم بتقاعد (`Retired` + `EffectiveToUtc=الآن`) أي مصفوفة افتراضية نشطة سابقة لنفس المنظمة **في نفس المعاملة**.
* **Retired**: نهائية، لا رجوع.

قيد قاعدة بيانات (`RiskAssessmentMatriceConfiguration`) يفرض **مصفوفة افتراضية نشطة واحدة كحد أقصى لكل منظمة**:

```sql
HasIndex(m => m.OrganizationId).IsUnique().HasFilter("[IsDefault] = 1 AND [IsDeleted] = 0 AND [Status] = 2")
```

## عدم إعادة كتابة التقييمات التاريخية

كل `RiskAssessment` يحتفظ بـ `MatrixId` **و** `MatrixVersion` (نسخة رقمية مأخوذة وقت التقييم). حتى لو تغيّرت المصفوفة لاحقًا (نسخة جديدة، أو حتى تقاعد المصفوفة الأصلية)، يبقى التقييم القديم مرتبطًا بالسياق (الاحتمالية/الأثر/الدرجة) الذي أُنشئ به. لا يوجد أي مسار كودي يعيد حساب تقييم قديم عند تغيّر المصفوفة.

## الاحتمالية والأثر

* `LikelihoodLevel` خاص بكل إصدار مصفوفة (لا يُعاد استخدامه بين الإصدارات).
* `ImpactDimension` كتالوج على مستوى المنظمة (Security, Safety, ...) قابل لإعادة الاستخدام عبر الإصدارات.
* `ImpactLevel` خاص بكل إصدار مصفوفة **لكل بُعد**، حتى لو كان البُعد نفسه معاد استخدامه.

## التحقق قبل الاعتماد/التفعيل

`RiskMatrixValidation.ValidateRatingBands` (مُختبر بـ 6 حالات وحدة) يفرض:

* وجود نطاق تصنيف واحد على الأقل.
* لكل نطاق: الحد الأدنى ≤ الحد الأعلى.
* النطاقات متلامسة تمامًا بلا فجوة ولا تداخل: `min(التالي) = max(السابق)` (وليس `+1`). هذا مقصود: صيغة `LikelihoodTimesWeightedImpact` تُنتج درجات كسرية (مثلاً 6.99)، فلا يصح افتراض حدود صحيحة (integer) بين النطاقات. `RiskScoringEngine.SelectRatingBand` يتعامل مع الحد الأعلى لكل نطاق (عدا الأخير) على أنه **غير شامل** (exclusive) — أي أن نقطة التماس تنتمي دائمًا للنطاق الأعلى. **الحد الأعلى للنطاق الأخير غير مُطبَّق إطلاقًا في الاختيار الفعلي للنطاق** (`MaximumScore` هناك للتحقق البنيوي فقط عبر `ValidateRatingBands`) — أي درجة تساوي أو تتجاوز الحد الأدنى للنطاق الأخير تنتمي إليه، مهما بلغت. هذا مقصود (النطاق الأعلى يمثّل "وما فوق")، وليس خطأً في التوثيق أو الكود.

`RiskMatrixValidation.ValidateWeights` يفرض وزنًا موجبًا لكل بُعد أثر مستخدم عند اختيار صيغة `LikelihoodTimesWeightedImpact` (انظر `phase-d6-risk-scoring.md`).

## فجوة موثقة

لا يوجد endpoint منفصل لـ "تعديل مصفوفة Draft قائمة" (إضافة/حذف صفوف بعد الإنشاء الأولي) — أي تصحيح يتطلب إنشاء Draft جديد عبر `PreviousVersionMatrixId`. هذا قرار متعمد لتبسيط النطاق، وليس نقصًا غير مقصود.
