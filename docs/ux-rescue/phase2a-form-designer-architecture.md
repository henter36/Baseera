# Phase 2A — معمارية استوديو تصميم النماذج

## طبقة الفصل

```text
FormDesignerStudioPage (orchestrator: routing, queries, mutations, undo/redo state)
  ├── StudioStartFlow          (بداية نموذج جديد: فارغ / قالب / نسخ)
  ├── StudioTopBar             (هوية + حالة الحفظ + إجراءات علوية)
  ├── StudioConflictBanner     (تعارض 409: تحميل / مقارنة / حفظ نسخة جديدة)
  ├── StudioFieldLibrary       (بحث + تصنيف + Quick Add)
  ├── StudioOutline            (شجرة صفحات/أقسام/حقول للقراءة والتنقل)
  ├── StudioCanvas             (Canvas فعلي: سحب/إفلات، تحرير مباشر، حذف بأثر، نسخ)
  ├── StudioInspector          (خصائص الحقل حسب النوع + قسم متقدم قابل للطي)
  │     ├── ConditionBuilder   (شرط الظهور / شرط الإلزام)
  │     └── FormulaBuilder     (صيغة الحساب)
  ├── ValidationPanel          (أخطاء/تحذيرات/اقتراحات + انتقال للعنصر)
  ├── FormPreviewPanel         (معاد استخدام كما هو — نفس محرك التقييم)
  ├── StudioReviewPanel        (حالة Form+Version موحَّدة، طلب مراجعة، قرار مراجعة)
  └── StudioMobileReview       (وضع الموبايل: مراجعة/تعديل بسيط فقط)
```

## قرار تصميمي محوري: لا محرك جديد

الاستوديو **لا يعيد بناء** أي منطق تقييم أو تحقق. كل مكوّن جديد يُعيد استخدام ما هو موجود فعليًا:

| الاحتياج | المصدر الموجود المُعاد استخدامه |
|---|---|
| نموذج بيانات الشرط/الصيغة | `schemaTypes.ts` (`FormConditionGroup`, `FormFormulaNode`) — يطابق تعداد الخادم رقميًا بدقة (تحقَّق مباشرة من `Baseera.Domain.Forms.Schema.FormSchemaModels.cs`) |
| تقييم الشرط/الصيغة في المعاينة | `previewLogic.ts` (`evaluateCondition`, `evaluateFormula`) — لم يُعدَّل |
| الحفظ التلقائي | `useFormDesignerAutosave.ts` — لم يُعدَّل؛ استُخدم كما هو (debounce 800ms، إلغاء الطلبات المتجاوَزة، حالة تعارض) |
| Undo/Redo | `historyStore.ts` — لم يُعدَّل؛ سقف 50 عملية |
| سحب/إفلات وإعادة الترتيب داخل Canvas | نفس نمط `@dnd-kit` (Pointer + Keyboard sensors) المستخدَم سابقًا في `DesignerCanvas.tsx` (المكوّن القديم نفسه أُزيل بعد أن أصبح Studio Canvas يغطي كل قدراته) |
| التحقق النهائي | `POST /forms/{id}/versions/{id}/validate` (`FormSchemaValidator`, `FormDependencyGraph`) — المصدر الوحيد للحقيقة؛ أي تحقق واجهي (كشف دورة في Builder) هو **تحقق مساعد فوري**، لا بديل |

## فجوة موثَّقة: كشف الدورات في الواجهة مقابل الخادم

`forms/designer/fieldDependencies.ts` يبني رسمًا بيانيًا محليًا بسيطًا (DFS) لاكتشاف الدورات فور تعديل شرط أو صيغة في Inspector، لمنع المستخدم من إدخال دورة قبل حتى محاولة الحفظ. هذا **لا يغني** عن `FormDependencyGraph` على الخادم — أي محاولة Autosave/Validate/SubmitForReview تمر عبر تحقق الخادم الكامل بغض النظر عن نتيجة الفحص المحلي. الفحصان مستقلان عمدًا؛ لا مشاركة كود بينهما لأن أحدهما TypeScript والآخر C#.

## Backend: إضافة واحدة مبررة، لا خدمة جديدة موازية

طُلب صراحة تقسيم Backend إلى خدمات محددة (`FormDesignerQueryService`, `FormDraftCommandService`, ...) أو **استخدام التقسيم الحالي إذا كان مناسبًا**. التقسيم الحالي (`FormQueryService`, `FormCommandService`, `FormVersionService`, `FormSchemaValidator`, `FormTemplateService`) يغطي بالفعل هذه الأدوار بدقة كافية؛ إنشاء طبقة تسمية جديدة فوقها كان سيُنتج تكرارًا بلا قيمة. الإضافتان الوحيدتان:

1. `IFormVersionService.CreateFromExistingFormAsync` — يُنشئ نموذجًا جديدًا (عبر `IFormCommandService.CreateDraftAsync` الموجود) ثم يبذر إصداره الأول بـSchema نموذج مصدر آخر (عبر `AllocateAndPersistVersionAsync` الموجود)، ضمن معاملة واحدة، مع فحص صلاحية عرض على النموذج المصدر وتسجيل تدقيق إضافي يحمل هوية المصدر.
2. `IFormTemplateService.GetSchemaAsync` — استعلام قراءة بسيط لمعاينة قالب، يعيد استخدام قواعد رؤية القوالب نفسها المستخرجة من `ListAsync` سابقًا (دالة مشتركة `BuildVisibleTemplatesQueryAsync` لمنع الانحراف).

لا عقد Backend آخر تغيَّر. `FormVersionDetailDto`, `FormVersionValidateResultDto`, `FormSchemaValidationIssue` كلها كما هي.

## عقد الاستوديو في الواجهة (Read model محلي، لا استعلام جديد لكل حقل)

الاستوديو **لا** يستدعي endpoint منفصل لكل صفحة/قسم/حقل. يُحمَّل الإصدار كاملاً مرة واحدة (`GET /forms/{formId}/versions/{versionId}` — DTO موجود مسبقًا يحمل `draftSchemaJson` كاملاً + `allowedActions`)، ويُحمَّل النموذج مرة واحدة (`GET /forms/{formId}`). كل تعديل لاحق (إضافة حقل، تغيير خاصية، شرط، صيغة) يُطبَّق على النسخة المحلية في الذاكرة (`historyStore`) ويُبَثّ للخادم عبر Autosave المُدبَّس (Debounced) لا فوريًا لكل ضغطة، مطابقًا لقيد "لا تُعِد تحميل Schema كاملة بعد كل تعديل بسيط".

## الأجزاء التي بقيت خارج الاستوديو عمدًا

`FormAccessPage`, `/settings/forms-governance`, `FormVersionReviewPage` المستقلة (نفس القرار مُتاح أيضًا داخل `StudioReviewPanel` دون الحاجة لمغادرة الاستوديو، لكن الصفحة المستقلة بقيت Advanced fallback بلا تعديل).
