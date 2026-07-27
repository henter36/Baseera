# Phase D.6 — الضوابط الحالية (RiskControl)

## الفصل عن المعالجة

`RiskControl` كيان منفصل تمامًا عن `RiskTreatmentPlan`/`RiskTreatmentAction`: الضابط يمثّل ما هو **قائم اليوم فعليًا** (وقائي/كاشف/تصحيحي/رادع/تعافٍ/تعويضي)، بينما خطة المعالجة تمثّل ما **سيُنفَّذ مستقبلًا** لتخفيف الخطر. لا يوجد أي مسار كودي يحوّل ضابطًا إلى إجراء معالجة أو العكس.

## عدم افتراض الفعالية

كل ضابط جديد يُنشأ بـ `ControlStatus = Proposed` و`ControlEffectiveness = NotTested` دائمًا (`IRiskControlService.CreateAsync`). لا يوجد أي منطق يفترض الفعالية من مجرد الإنشاء. الانتقال إلى `Implemented` يحدث فقط تلقائيًا عند أول تسجيل اختبار (`RecordTestAsync`)، والفعالية نفسها (`Effective/PartiallyEffective/Ineffective/NotTested/Unknown`) تُحدَّث فقط عبر نفس الاستدعاء الصريح — لا قيمة افتراضية متفائلة.

## أنواع الضوابط (`RiskControlType`)

`Preventive`, `Detective`, `Corrective`, `Deterrent`, `Recovery`, `Compensating` — سُمّيت الكيانات `RiskControl`/`RiskControlType` صراحة (وليس `Control` المجرد) لتفادي التعارض اللفظي مع `WorkforceRoleCategory.Control` الموجود مسبقًا في وحدة القوى البشرية (غرفة تحكم)، بحسب ما أظهرته مراجعة الكود الحالي.

## تكامل جودة البيانات والتدخلات

* `RiskDataQualityCodes.ControlNotTested`: ضابط `Implemented` لم يُختبر بعد.
* `RiskDataQualityCodes.IneffectiveControlWithoutTreatment`: ضابط `Ineffective` بلا خطة معالجة مرتبطة (لا شيء يعوّضه).
* `RiskInterventionTypes.ControlNotTested` / `ControlIneffective`: نفس الإشارتين تظهران في Intervention Queue.

## فجوة موثقة

لا يوجد جدولة تلقائية لتذكير باختبار الضابط عند اقتراب `NextTestDueAtUtc` (لا وظيفة خلفية/Background Job مضافة في هذه المرحلة) — الحقل مخزَّن ومعروض، لكن التنبيه الاستباقي (قبل الفوات) غير مطبَّق؛ المطبَّق فقط هو الإشارة عند **عدم الاختبار على الإطلاق** لضابط مطبَّق.
