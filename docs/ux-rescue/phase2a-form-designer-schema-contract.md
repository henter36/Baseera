# Phase 2A — عقد Schema المستخدَم في الاستوديو

لم يتغيّر عقد Schema بين الواجهة والخادم في هذه المرحلة. هذا الملف يوثّق العقد الموجود (لضمان أن الاستوديو لا ينحرف عنه) والإضافتين الوحيدتين على مستوى الـAPI.

## أنواع الحقول المدعومة (مطابقة 1:1 لـ `Baseera.Domain.Forms.Schema.FormFieldType`)

| القيمة | النوع | الفئة في مكتبة الحقول |
|---|---|---|
| 0 | ShortText | نص |
| 1 | LongText | نص |
| 2 | Number | أرقام |
| 3 | Percentage | أرقام |
| 4 | Date | تاريخ ووقت |
| 5 | Time | تاريخ ووقت |
| 6 | DateTime | تاريخ ووقت |
| 7 | SingleChoice | اختيارات |
| 8 | MultipleChoice | اختيارات |
| 9 | YesNo | اختيارات |
| 10 | File | مرفقات |
| 11 | Image | مرفقات |
| 12 | Signature | مرفقات |
| 13 | Location | هيكلة |
| 14 | RepeatingTable | هيكلة |
| 15 | OrganizationalReference | حقول مؤسسية |
| 16 | CalculatedNumber | أرقام |
| 17 | CalculatedText | نص |

`src/frontend/src/forms/designer/fieldLibrary.ts` هو المصدر الوحيد لتصنيف هذه الأنواع في مكتبة الحقول. لم يُضَف أي نوع حقل وهمي؛ كل الفئات السبع (نص/اختيارات/أرقام/تاريخ ووقت/مرفقات/هيكلة/حقول مؤسسية) مطلوبة في التكليف مُغطاة بأنواع حقيقية.

## أنواع مطلوبة في نص التكليف لا مقابل Backend لها (Not Applicable، لا اختلاق)

| الاسم في التكليف | القرار |
|---|---|
| قائمة منسدلة (منفصلة عن اختيار واحد) | Not Applicable — `SingleChoice` هو النوع الوحيد؛ الفرق بين قائمة منسدلة وأزرار راديو هو خيار عرض (Rendering) لا نوع Domain منفصل |
| عدد صحيح (منفصل عن رقم) | Not Applicable — `Number` مع `decimalPlaces = 0` يغطي الحالة؛ لا نوع `Integer` منفصل في `FormFieldType` |
| المستخدم (كحقل مؤسسي) | Not Applicable — `FormOrganizationalReferenceKind` يحتوي فقط `Region/Facility/FacilityUnit/Department`، لا `User` |
| عنوان قسم / نص توضيحي (كنوع حقل) | Not Applicable كنوع حقل منفصل — العنوان خاصية أصلية للقسم (`titleAr`) والنص التوضيحي خاصية للحقل (`description`/`instructions`)، لا حاجة لحقل Placeholder وهمي |

## الشروط (`FormConditionGroup`, `FormConditionOperator`)

مطابقة رقمية كاملة بين `src/frontend/src/forms/designer/schemaTypes.ts` و`Baseera.Domain.Forms.Schema.FormSchemaModels.cs` (Equals=0 … After=15). عامل **"بين" (Between)** المطلوب في نص التكليف **غير موجود** كعامل واحد في تعداد الخادم؛ يُحقَّق عبر مجموعة شروط "كل الشروط" تضم `أكبر من أو يساوي` + `أقل من أو يساوي` — وهو ما يدعمه `ConditionBuilder` أصلاً عبر المجموعات المتداخلة، بلا حاجة لعامل خاص.

## الصيغ (`FormFormulaNode`)

`FormulaBuilder` يعرض فقط العمليات المدعومة فعليًا في `FormFormulaBinaryOperator` (Add/Subtract/Multiply/Divide/Modulo) و`FormFormulaFunction` (Min/Max/Sum/Average/Round/Floor/Ceiling/Abs/Coalesce/Concat). **"العدّ" و"النسبة"** المذكوران في نص التكليف كدالتي صيغة **لا مقابل لهما في تعداد الخادم** ولم تُضافا (لا اختلاق دالة لا يستطيع الخادم تنفيذها أو التحقق منها).

## التحقق من التوافق البرمجي (Numeric parity check)

`docs/ux-rescue/phase2a-form-designer-completion-report.md` يوثّق أن هذا التطابق الرقمي تحقَّق منه مباشرة بقراءة الملفين معًا أثناء التنفيذ (لا اختبار آلي يقارنهما تلقائيًا اليوم). فجوة موثَّقة: لا يوجد اختبار CI يمنع انحراف مستقبلي بين تعداد C# وTypeScript لو أُضيف عامل/دالة جديدة لأحدهما فقط — موصى به كتحسين لاحق (خارج نطاق هذه المرحلة).

## إضافتان على عقد الـAPI (موثَّقتان بالكامل في الهندسة المعمارية)

1. `POST /api/v1/forms/copy-from/{sourceFormId}/{sourceVersionId}` — طلب: نفس `CreateFormRequest` الموجود، استجابة: `FormVersionDetailDto` الموجود (بلا حقول جديدة).
2. `GET /api/v1/form-templates/{templateId}/schema` — استجابة: `FormTemplateSchemaDto` جديد (`Id`, `NameAr`, `CanonicalSchemaJson`, `PageCount`, `SectionCount`, `FieldCount`).

لا Migration قاعدة بيانات لهذه المرحلة — كلا الإضافتين تقرأ/تكتب كيانات موجودة فعليًا (`FormDefinition`, `FormVersion`, `FormTemplate`) دون تعديل مخططها.
