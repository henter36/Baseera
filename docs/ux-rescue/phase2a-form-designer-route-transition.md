# Phase 2A — قرار الانتقال لكل Route في مجال النماذج

يفصّل هذا الملف القرار (Keep / Redirect / Merge / Advanced fallback / Remove after migration) لكل Route تحت `/forms*` و`/form-templates`، ويثبت أن كل قرار Redirect نُفِّذ فعليًا مع نقل القدرة واختبار الـRedirect، لا حذفًا مباشرًا. الجدول الكامل المُحدَّث (بما فيه Routes خارج هذا المجال) في `docs/ux-rescue/screen-and-route-inventory.md`.

## ملخص القرار

| Route | القرار | التفصيل |
|---|---|---|
| `/forms/designer/new` | **جديد — Keep** | المسار الرئيس لبدء نموذج |
| `/forms/designer/:formId` | **جديد — Keep** | المسار الرئيس للاستوديو الموحَّد |
| `/forms` | Keep | قائمة النماذج؛ زر الإنشاء يوجّه الآن إلى `/forms/designer/new` بدل `/forms/new` |
| `/forms/new` | **Advanced fallback** | لا رابط داخلي يستهدفه بعد الآن؛ يبقى مسارًا يعمل (نموذج الحقول الكامل: تصنيف أمني + إدارة مالكة) لمن يحتاج الإعداد الكامل بدل بداية الاستوديو المبسَّطة عمدًا |
| `/forms/:id` | Keep | صفحة تفاصيل النموذج، لم تتغيّر |
| `/forms/:id/edit` | Advanced fallback | تعديل بيانات وصفية (تصنيف/إدارة) — لا يوجد مكافئ داخل الاستوديو لتعمّد إبقاء بداية الاستوديو مبسَّطة |
| `/forms/:id/review` | Advanced fallback | مراجعة حالة **Form** (منفصلة عن الإصدار)؛ القرار المكافئ لمراجعة **الإصدار** متاح الآن أيضًا داخل `StudioReviewPanel` |
| `/forms/:id/access` | Advanced fallback (بلا تغيير) | إدارة صلاحيات الوصول — نطاق إداري منفصل عمدًا عن التأليف اليومي، كما أوصى Phase 0 |
| `/forms/:formId/versions` | Keep | قائمة الإصدارات؛ رابط "تصميم" يوجّه الآن مباشرة للاستوديو (`/forms/designer/:formId?versionId=`)، وأُضيف رابط "مقارنة الإصدارات" |
| `/forms/:formId/versions/new` | **Redirect (مسار ميت مؤكَّد)** | لم يكن له أي رابط داخلي أصلاً (مسار ميت موثَّق منذ Phase 0)؛ الآن يُحوَّل فورًا (`RedirectToStudioNew`) إلى `/forms/designer/:formId` بدل عرض قائمة الإصدارات نفسها |
| `/forms/:formId/versions/compare` | **جديد — Keep** | مقارنة إصدارين |
| `/forms/:formId/versions/:versionId` | Keep | تفاصيل الإصدار؛ رابط "فتح الاستوديو" يوجّه للمسار الجديد |
| `/forms/:formId/versions/:versionId/edit` | **Redirect** | كان المسار الأساسي للمصمم القديم (`FormDesignerPage`، أُزيل الملف بعد نقل كل قدراته إلى الاستوديو). كل الروابط الداخلية (قائمة الإصدارات، تفاصيل الإصدار) حُدِّثت لتستهدف `/forms/designer/:formId?versionId=` مباشرة؛ هذا المسار أصبح Redirect فقط (`RedirectToStudioEdit`) للروابط القديمة المحفوظة |
| `/forms/:formId/versions/:versionId/review` | Advanced fallback | نفس قرار مراجعة الإصدار متاح أيضًا داخل الاستوديو (`StudioReviewPanel`) دون مغادرته؛ الصفحة المستقلة بقيت بلا تعديل لمن يفضّلها |
| `/forms/:formId/versions/:versionId/snapshot` | Advanced fallback (بلا تغيير) | عرض JSON خام للقطة مقفلة — أداة تدقيق تقنية |
| `/form-templates` | Advanced fallback | القوالب متاحة الآن أيضًا كخطوة بداية داخل الاستوديو (`/forms/designer/new`)؛ هذه الصفحة تبقى للاستخدام الإداري (إنشاء/إدارة القوالب نفسها عبر `Forms.ManageTemplates`)، مع إصلاح خلل `window.location.assign` → SPA navigation |

## ما لم يُحذَف ولماذا

لم يُحذَف أي Route فعليًا من `App.tsx` في هذه المرحلة. المكوّنان اللذان أُزيلا هما ملفات كود غير مُوجَّهة إليها أي Route بعد الآن:

- `FormDesignerPage.tsx` + اختباره — استُبدل بالكامل؛ كل قدراته (Canvas، Palette، Properties، Toolbar، Autosave، Undo/Redo، Validate، Preview، Submit) موجودة في الاستوديو الجديد وتتجاوزها (شروط، صيغ، Validation Panel مصنَّف، مراجعة موحَّدة، استجابة للأجهزة).
- `DesignerCanvas.tsx`, `DesignerPalette.tsx`, `DesignerPropertiesPanel.tsx`, `DesignerToolbar.tsx` — كانت مُستخدَمة حصريًا من `FormDesignerPage.tsx`؛ تحقَّق عبر بحث نصي كامل في `src/` أنه لا يوجد أي استيراد آخر لها قبل الحذف.

المكوّنات المشتركة التي **بقيت ولم تُمَس** لأن الاستوديو يعتمد عليها مباشرة: `useFormDesignerAutosave.ts`, `historyStore.ts`, `previewLogic.ts`, `schemaTypes.ts`, `designerHelpers.ts`, `formPreviewWidths.ts`, `FormPreviewPanel.tsx`.

## اختبار الـRedirect وBrowser back/forward

- `src/App.route-redirects.test.tsx`: يُصدِّر `RedirectToStudioEdit`/`RedirectToStudioNew` من `App.tsx` (كانا محليين غير مُصدَّرين) ويختبر كليهما عبر `MemoryRouter` للتأكد من وصول رابط قديم (`/forms/f1/versions/v1/edit`, `/forms/f1/versions/new`) فعليًا للاستوديو مع الحفاظ على `formId`/`versionId`.
- Browser back/forward: الاستوديو يعكس `versionId` في `useSearchParams` (لا `useState` معزول)، لذلك التنقّل للخلف/للأمام بين إصدارات مختلفة يعمل عبر آلية React Router القياسية دون حاجة لمنطق تاريخ إضافي. لم يُضَف اختبار E2E مخصص لـback/forward الفعلي في متصفح (خارج نطاق Vitest/RTL)؛ موثَّق كـ Partial في سجل الامتثال — التحقق تم يدويًا فقط عبر قراءة الكود (تحديث `searchParams` عبر `setSearchParams` القياسي، وهو نفس الآلية التي يعتمد عليها المتصفح لبناء سجل التنقّل).

## تحديث `npm run check:ux-routes`

الفحص الآلي (`scripts/ux-route-inventory-check.mjs`) يقارن `<Route path>` في `App.tsx` بصفوف `docs/ux-rescue/screen-and-route-inventory.md`. بعد هذه المرحلة: **65 Route** (كانت 62)، كل صف مُحدَّث، والفحص **passed** (انظر `phase2a-form-designer-completion-report.md` للمخرجات الفعلية).
