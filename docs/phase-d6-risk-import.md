# Phase D.6 — الاستيراد المنضبط

## التدفق المنفَّذ

```
Preview (تحقق + عد صفوف صالحة/مرفوضة/مكررة، يحفظ RiskImportBatch بحالة Previewed)
   -> Confirm (يعيد نفس التحقق، يطبّق الصفوف الصالحة وغير المكررة فقط، يحفظ Status=Confirmed)
   -> Confirm لاحق بنفس (facility, ImportKind.RiskRecords, FileHash) => إعادة تشغيل آمنة (Idempotent) دون تكرار
```

مطابق تمامًا لبنية `ISensitiveCustodyImportService`/`IResourceImportService` الموجودة مسبقًا: لا رفع ملفات خام، الصفوف JSON منظم من العميل، مفتاح Idempotency = (facilityId, ImportKind, SourceSystem, SourceReference, FileHash).

## ما يُنشأ فعليًا عند Confirm

لكل صف صالح غير مكرر: `RiskRecord` جديد (`Status = UnderAssessment`, `SourceType = Import`) + `RiskAssessment` نوع `Inherent` بحالة **Draft** (وليس معتمدًا)، بدرجة محسوبة على الخادم عبر نفس `RiskScoringEngine` المستخدم للإنشاء اليدوي — **لا يُقبل أي Score من ملف الاستيراد**. هذا التقييم يمر لاحقًا بنفس دورة Submit→Review→Approve العادية قبل أن يصبح معتمَدًا.

## قواعد الرفض عند التحقق

* رمز تصنيف غير موجود.
* مصفوفة غير موجودة أو ليست `Active`.
* رمز مستوى احتمالية/أثر غير موجود ضمن المصفوفة المحددة.
* عدم وجود أي بُعد أثر مقيَّم.
* عنوان فارغ.
* تكرار: نفس (التصنيف + العنوان المُطبَّع) موجود مسبقًا في المنشأة، أو مكرر داخل نفس الدفعة.

## خطأ حقيقي عُثر عليه وأُصلح أثناء اختبار هذه المرحلة (يستحق التوثيق)

النسخة الأولى من `PreviewAsync`/`ConfirmAsync` استدعت `Db.Update(batch)` مباشرة بعد أن يُنشئ `FindOrCreateBatchAsync` الدفعة عبر `Db.Add(batch)` **ضمن نفس دورة الحفظ**. استدعاء `Update()` على كيان لا يزال بحالة `Added` يجبر Entity Framework Core على تحويل حالته إلى `Modified`، فيُصدر أمر `UPDATE` بدل `INSERT` لصف غير موجود بعد في قاعدة البيانات — يفشل بـ `DbUpdateConcurrencyException` ("expected to affect 1 row(s), but actually affected 0"). اكتُشف الخطأ عبر اختبار تكامل حي (`Import_confirm_is_idempotent_on_same_file_hash`) ضد SQL Server حقيقي، وليس عبر تحليل ثابت. الإصلاح: إزالة استدعاء `Update()` غير الضروري (الكيان متعقَّب بالفعل، وEF يكتشف التغييرات تلقائيًا). **الدرس**: لا تستدعِ `Update()` صراحة على كيان تم `Add()`ه للتو ضمن نفس الطلب.

## المصالحة (`IRiskReconciliationService`)

مصالحة أساسية فقط: يكتشف مجموعات مخاطر تتشارك نفس `RecurrenceKey` ضمن نفس المنشأة (احتمال تكرار عبر مصادر مختلفة)، ويسجّل قرار المصالحة (`RiskReconciliationRecord`) بمبرر إلزامي. لا يوجد دمج تلقائي — فقط تسجيل قرار بشري.

## قيود مفروضة على الاستيراد (مطبَّقة)

* لا يُقبل Score محسوب يدويًا — يُعاد احتسابه دائمًا.
* لا يُقبل Matrix version غير موجودة أو غير نشطة.
* لا يوجد استيراد لمخاطر مغلقة عبر هذا المسار (نوع `RiskImportKind.RiskRecords` فقط مُنفَّذ من بين الأنواع الستة المعرَّفة في enum؛ الأنواع الأخرى — Owners, Assessments, Controls, TreatmentPlans, TreatmentActions, SourceReferences — **معرَّفة في enum لكن غير مُنفَّذة كمسارات استيراد فعلية في هذه المرحلة**، فجوة موثقة).
