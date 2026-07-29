# Phase 2A — مصفوفة الاختبارات

## Backend — Unit Tests

لا اختبارات وحدات جديدة مخصصة أُضيفت للإضافتين الجديدتين (`CreateFromExistingFormAsync`, `GetSchemaAsync`) لأنهما تُركِّبان خدمات موجودة مُختبَرة وحدويًا بالفعل (`FormCommandService.CreateDraftAsync`, `AllocateAndPersistVersionAsync`, قواعد رؤية القوالب) بدل منطق جديد مستقل يستحق اختبار وحدة منفصل؛ التغطية الفعلية لهما هي اختبارات التكامل أدناه (الأصح لأنهما يلمسان قاعدة بيانات وصلاحيات ونطاقًا حقيقيًا). مجموعة اختبارات الوحدات الحالية (990 اختبارًا في `Baseera.UnitTests`، تشمل `Schema/FormDependencyGraphTests.cs`, `Schema/FormFormulaEvaluatorTests.cs`, `Versions/FormVersionStateMachineTests.cs`, إلخ) لم تتأثر ولم تُعدَّل — كلها ما زالت تعمل دون تغيير في المنطق الذي تغطيه.

## Backend — Integration Tests (جديدة في هذه المرحلة)

في `FormsVersionIntegrationTests.cs`:

| الاختبار | يغطي |
|---|---|
| `Copy_from_existing_form_seeds_new_draft_and_records_audit_provenance` | نسخ نموذج موجود → نموذج+إصدار جديدان مستقلان، `DraftSchemaHash` مطابق للمصدر، إدخال AuditLog يحمل `sourceFormId`/`sourceVersionId`، ومحاولة نسخ من خارج النطاق التنظيمي تعيد `404` |
| توسيع `Template_list_respects_scope_visibility_and_ownership` | معاينة قالب (`GET .../schema`) تتبع **بالضبط** نفس قواعد رؤية القائمة: قالب عام يُعاين من مستخدم آخر، قالب خاص يُعاين فقط من مالكه، غير المصرَّح له يحصل على `404` لا `403` |

النتيجة الفعلية لتشغيل مجموعة `Baseera.IntegrationTests` الكاملة (263 اختبارًا) بعد هذه الإضافات موثَّقة بالأرقام الحقيقية في `phase2a-form-designer-completion-report.md`.

## Frontend — Unit/Component Tests (جديدة)

| الملف | يغطي |
|---|---|
| `fieldDependencies.test.ts` | جمع اعتماديات حقل، اكتشاف من يعتمد على حقل محدَّد، **اكتشاف الدورات** (مباشرة، ذاتية، متعدية عبر 3 حقول)، منع الاعتماد على الحقل نفسه |
| `versionDiff.test.ts` | تصنيف حقل مضاف/محذوف/معدّل/بلا تغيير، تغييرات الخيارات (إضافة/حذف/تعديل)، تغيير شرط الإلزام دون تغيير علم الإلزام نفسه |
| `ConditionBuilder.test.tsx` | حالة فارغة، تقييد المعاملات حسب نوع الحقل (نصي لا يعرض "أكبر من")، استبعاد الحقل نفسه من قائمة الاختيار (منع الاعتماد الذاتي)، مجموعات متداخلة، إزالة الشرط بالكامل |
| `FormulaBuilder.test.tsx` | حالة فارغة، عملية ثنائية افتراضية، **تحذير القسمة على صفر الثابتة**، تقييد معاملات Min/Max/Sum لحقول رقمية فقط، السماح بحقل نصي في Concat، استبعاد الحقل نفسه، إضافة/حذف معاملات لدالة متغيرة العدد |
| `ValidationPanel.test.tsx` | تصنيف أخطاء/تحذيرات، عدم عرض كود الخطأ الخام للمستخدم، تحديد موضع الخطأ (صفحة/قسم/حقل)، اقتراحات محلية (نموذج فارغ، خيار واحد فقط)، زر الانتقال للعنصر فعليًا يستدعي `onNavigateToElement` بالموضع الصحيح |
| `FormDesignerStudioPage.test.tsx` | إنشاء نموذج فارغ ووصوله فعليًا للاستوديو (تدفق بداية كامل)، حالات الحفظ التلقائي (`توجد تغييرات غير محفوظة` → `تم الحفظ`)، **تعارض 409 يعرض الخيارات الثلاثة** بالضبط، **وضع الموبايل يعرض رسالة الشاشة الأكبر ولا يعرض مقبض السحب** |
| `App.route-redirects.test.tsx` | Redirect فعلي من كلا المسارين القديمين (`/edit`, `/versions/new`) إلى الاستوديو مع الحفاظ على `formId`/`versionId` |

## Frontend — بنود مطلوبة في نص التكليف لم تُختبَر آليًا بعد (Missing/Partial)

| البند | الحالة |
|---|---|
| Quick Add وBحث في مكتبة الحقول | منفَّذ في `StudioFieldLibrary`، **بلا اختبار مخصص** له وحده (مغطى بشكل غير مباشر فقط عبر أن `handleAddField` يُستدعى في مسار إنشاء النموذج الفارغ) — Partial |
| Inline editing (تحرير مباشر) | منفَّذ في `StudioCanvas`، **بلا اختبار تفاعل مخصص** (نقر زر ✎ → كتابة → Blur → تحقق من `onRenameFieldLabel`) — Partial |
| Move/duplicate/delete field عبر الواجهة (تفاعليًا) | منطق `studioSchemaOps.ts` (نسخ/حذف/نقل) **غير مختبَر بوحدات مخصصة** رغم أنه دوال نقية بسيطة قابلة للاختبار بسهولة — Missing |
| Keyboard-only workflow (تسلسل Tab كامل) | لا اختبار مؤتمت لتسلسل Tab الفعلي عبر الاستوديو — Missing (انظر أيضًا ملاحظة الوصول) |
| Preview Desktop/Tablet/Mobile (تبديل الأوضاع فعليًا وعرض المحتوى الصحيح) | `FormPreviewPanel` نفسه (غير مُعدَّل) له اختبارات موجودة مسبقًا ضمن `previewLogic.test.ts` وما شابه؛ لا اختبار جديد يتحقق أن الاستوديو **يستدعيه** بشكل صحيح عبر أزرار "المعاينة" — Partial |
| Version state / Request review (تفاعليًا حتى نهاية القبول/الرفض) | مغطى جزئيًا عبر بناء `StudioReviewPanel` واستخدام `allowedActions` الحقيقية، **بلا اختبار تفاعل كامل للسيناريو** (طلب مراجعة → قرار → قفل) على مستوى الواجهة — الخادم مغطى تكامليًا في `FormsVersionIntegrationTests` الموجودة مسبقًا (`Version_create_save_submit_approve_lock_and_reject_locked_update`) — Partial |
| عدم استخدام Modal كبيرة | **تحقُّق بالقراءة فقط**، لا اختبار مؤتمت يبحث عن غياب عنصر Modal — Missing كاختبار، Verified كتصميم فعلي |
| Loading/empty/error states لكل مكوّن جديد | مغطاة جزئيًا عبر أنماط JSX الموجودة (`<div className="loading">`, `<div className="empty">`) المطابقة لبقية التطبيق، **بلا اختبار صريح لكل حالة على حدة** لكل مكوّن جديد — Partial |

## الحصيلة الرقمية

الأرقام الفعلية النهائية (بعد كل الإضافات، بما فيها هذا الملف) موثَّقة في `phase2a-form-designer-completion-report.md`، مأخوذة من تشغيل فعلي لـ`dotnet test` (Unit + Integration) و`npm run test` (Frontend) — لا تقدير.
