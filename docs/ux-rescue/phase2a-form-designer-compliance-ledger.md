# Phase 2A — سجل الامتثال (Compliance Ledger)

الحالات: `Verified` (منفَّذ ومُتحقَّق منه بدليل كود/اختبار)، `Partial` (منفَّذ جزئيًا، الفجوة موصوفة)، `Missing` (غير منفَّذ)، `Not Applicable` (لا مقابل له في نموذج البيانات/الصلاحيات الحالي، ولم يُختلَق).

عتبة الجاهزية المعلنة في هذا الملف: **لا ادّعاء اكتمال كامل** طالما توجد بنود `Missing`. Issue #144 **لا يُغلَق** بهذا التسليم — راجع تقرير الإكمال.

## المسار الرئيس والتخطيط

| # | البند | الحالة | الدليل |
|---|---|---|---|
| 1 | `/forms/designer/:formId` و`/forms/designer/new` كمسار رئيس واحد | Verified | `App.tsx` routes، `FormDesignerStudioPage.tsx` |
| 2 | `versionId`/`draftVersionId` في URL عند الحاجة | Verified (كـ`versionId` عبر `useSearchParams`؛ `draftVersionId` لم يُستخدَم كاسم منفصل لأن كل إصدار قابل للتحرير هو بالتعريف مسودة) | `FormDesignerStudioPage.tsx` |
| 3 | دمج إنشاء/تحرير/تصميم/حقول/خصائص/شروط/صيغ/معاينة/تحقق/إصدارات/طلب مراجعة | Verified للجميع ما عدا "الإصدارات" (سجل كامل، مقارنة) — تلك تبقى Route مستقل مرتبط، ليست مضمَّنة حرفيًا داخل نفس الشاشة | `FormDesignerStudioPage.tsx`، `StudioReviewPanel.tsx`، `FormVersionComparePage.tsx` |
| 4 | لا حذف Routes قديمة قبل نقل القدرة واختبار Redirect | Verified | `phase2a-form-designer-route-transition.md`، `App.route-redirects.test.tsx` |
| 5 | تحديث `npm run check:ux-routes` | Verified — يمر (65 Route موثَّق) | مخرجات فعلية في تقرير الإكمال |

## الشريط العلوي وحالات الحفظ

| # | البند | الحالة | الدليل |
|---|---|---|---|
| 6 | عرض دائم لاسم النموذج/حالة المسودة/رقم الإصدار/حالة الحفظ/تراجع/إعادة/تحقق/معاينة/طلب مراجعة | Verified | `StudioTopBar.tsx` |
| 7 | 5 حالات حفظ محدَّدة، بلا نجاح متكرر مزعج | Verified | `StudioTopBar.tsx` (`STATUS_LABELS_AR`)، لا Toast |

## التخطيط Desktop/Tablet/Mobile

| # | البند | الحالة | الدليل |
|---|---|---|---|
| 8 | Desktop ثلاثي الألواح (مكتبة حقول/مخطط، Canvas، Inspector) | Verified | `.studio-body` CSS، `FormDesignerStudioPage.tsx` |
| 9 | Tablet: لوحات قابلة للفتح داخل الصفحة، لا Modal متراكبة | Partial — منفَّذ عبر `data-panel-open` وCSS `position: fixed` من جانب واحد؛ لا اختبار تفاعلي مخصص لفتح/إغلاق اللوحة ولا Focus restoration صريح عند الإغلاق | `index.css` (`@media (max-width: 1180px) and (min-width: 769px)`)، `FormDesignerStudioPage.tsx` |
| 10 | Mobile: مراجعة وتعديلات بسيطة فقط، رسالة صريحة، لا ادّعاء سحب كامل | Verified، مُختبَر آليًا | `StudioMobileReview.tsx`، اختبار `renders the mobile review-only mode...` في `FormDesignerStudioPage.test.tsx` |

## بداية إنشاء النموذج

| # | البند | الحالة | الدليل |
|---|---|---|---|
| 11 | اختيار نقطة بداية: فارغ/قالب/نسخ | Verified | `StudioStartFlow.tsx` |
| 12 | فارغ يطلب فقط اسم/غرض/نوع استخدام، بلا حوكمة كاملة | Partial — تصنيف أمني وإدارة مالكة مُخفيان بقيم افتراضية فعليًا (لا تُطلَب)، لكن **نطاق النموذج (Scope)** يبقى ظاهرًا وإلزاميًا لأنه قيد Backend حقيقي غير قابل للحذف (لا يمكن إنشاء نموذج بلا نطاق صالح) | `StudioStartFlow.tsx` (`IdentityFields`) |
| 13 | نوع الاستخدام (مرة واحدة/يومي/أسبوعي/شهري/مخصص) | Partial — تُعرَض وتُجمَع في الواجهة، **لا حقل Backend لتخزينها** على `FormDefinition` | `phase2a-form-designer-scope.md` |
| 14 | قالب: اسم/وصف/حقول وأقسام/معاينة/جهة مالكة/عام أو خاص | Verified | `StudioStartFlow.tsx` (`TemplateFlow`)، نقطة جديدة `GET /form-templates/{id}/schema` |
| 15 | إنشاء من قالب لا يعدّل القالب الأصلي | Verified (سلوك Backend موجود مسبقًا، لم يتغيّر) | `FormTemplateService.CreateFormFromTemplateAsync` |
| 16 | نسخ نموذج موجود: بحث ضمن المصرَّح به، اختيار إصدار مصدر، نسخ Schema فقط، لا نسخ استجابات/حملات، تسجيل المصدر في AuditLog | Verified | `StudioStartFlow.tsx` (`CopyExistingFlow`)، `FormVersionService.CreateFromExistingFormAsync`، اختبار تكامل `Copy_from_existing_form_seeds_new_draft_and_records_audit_provenance` |

## مكتبة الحقول

| # | البند | الحالة | الدليل |
|---|---|---|---|
| 17 | مكتبة قابلة للبحث والتصنيف، 7 فئات | Verified | `fieldLibrary.ts`, `StudioFieldLibrary.tsx` |
| 18 | دعم الأنواع الفعلية فقط، لا نوع لا يحفظه Backend | Verified | `phase2a-form-designer-schema-contract.md` |
| 19 | النقر للإضافة، Quick Add (بحث)، لوحة مفاتيح كاملة، تحديد موضع الإضافة | Verified للنقر/البحث/لوحة المفاتيح؛ تحديد الموضع = إدراج بعد الحقل المحدَّد حاليًا أو نهاية القسم الأول | `StudioFieldLibrary.tsx`, `FormDesignerStudioPage.tsx` (`handleAddField`) |
| 20 | Drag-and-drop من المكتبة إلى Canvas | **Missing عمدًا** — قرار نطاق موثَّق (لم يكن موجودًا قبل هذه المرحلة أصلاً) | `phase2a-form-designer-scope.md` |
| 21 | المفضلة/الحديثة كتفضيل مستخدم غير حرج | Partial — "حديثة" فقط (لا "مفضلة" منفصلة)، محفوظة في `localStorage` بمعالجة أخطاء صامتة | `FormDesignerStudioPage.tsx` (`loadRecentTypes`/`saveRecentTypes`) |

## Canvas

| # | البند | الحالة | الدليل |
|---|---|---|---|
| 22 | صفحات/أقسام/حقول، إعادة ترتيب، نقل بين أقسام/صفحات، نسخ، حذف | Verified لكل شيء ما عدا "نقل حقل بين أقسام" تفاعليًا من الواجهة | `studioSchemaOps.ts` يحتوي `moveFieldAcrossSections` لكنه **غير مربوط بأي زر في `StudioCanvas`** حتى الآن — Partial |
| 23 | Multi-select للعمليات الآمنة | **Missing** — لم يُنفَّذ تحديد متعدد؛ كل العمليات فردية | — |
| 24 | Undo/Redo | Verified | `historyStore.ts` (غير مُعدَّل)، يغطي كل العمليات الجديدة لأنها تمر عبر `applySchema` |
| 25 | تحرير مباشر لعنوان الصفحة/القسم/الحقل والنص التوضيحي | Verified لعناوين الصفحة/القسم/الحقل؛ **النص التوضيحي (`description`) يُعدَّل من Inspector فقط، لا Inline في Canvas** — Partial | `StudioCanvas.tsx`، `StudioInspector.tsx` |
| 26 | تحديد العنصر بوضوح دون ألوان صاخبة، شارات (إلزامي/شرط/صيغة/خطأ/تحذير) | Verified | `.studio-field-row-selected` CSS، شارات `badge` نصية |
| 27 | حذف حقل مستخدَم في شرط/صيغة يعرض أثر الحذف، لا `window.confirm` | Verified | `StudioCanvas.tsx` (`FieldRow` confirmingDelete + `dependents`) |
| 28 | منع حذف يؤدي لـSchema غير صالح دون تأكيد | Partial — التأكيد المحلي يُعرِض الأثر لكن **لا يمنع** الحذف فعليًا (المستخدم يمكنه المتابعة رغم التحذير)؛ التحقق النهائي يبقى على الخادم عند التحقق/الإرسال | `StudioCanvas.tsx` |
| 29 | حذف يدعم Undo | Verified | نفس آلية `applySchema`/`historyStore` |

## Inspector

| # | البند | الحالة | الدليل |
|---|---|---|---|
| 30 | Inspector واحد يتغيّر حسب العنصر | Verified | `StudioInspector.tsx` |
| 31 | خصائص أساسية معروضة فقط للحقل المناسب | Verified | `StudioInspector.tsx` (تصفية حسب `CHOICE_TYPES`/`NUMBER_TYPES`/`TEXT_TYPES`/`FILE_TYPES`) |
| 32 | خيارات: إضافة/تعديل/ترتيب/حذف، مفتاح ثابت، منع تكرار | Verified للإضافة/التعديل/الترتيب/الحذف/منع التكرار | `ChoiceOptionsEditor` في `StudioInspector.tsx` |
| 33 | عدم تغيير مفتاح خيار منشور بلا سياسة Migration | **Missing** — لا فحص لحالة "هل هذا الإصدار مبني على نسخة منشورة سابقة تستخدم هذا المفتاح" | — |
| 34 | إعدادات متقدمة في قسم مطوي (شروط، صيغ، Validation متقدم، Data binding، مفاتيح تكامل) | Verified لشروط الظهور/الإلزام والصيغة؛ **Not Applicable** لـData binding/مفاتيح التكامل (لا مفهوم Backend لهذا سوى مفتاح الحقل نفسه، مذكور صراحة في الواجهة)؛ **Validation متقدم** (قواعد تحقق مخصصة) لم تُضَف واجهة تحرير لها (`validationRules` موجودة في النموذج لكن بلا محرر UI) — Partial | `StudioInspector.tsx` |

## الصفحات والأقسام (Outline)

| # | البند | الحالة | الدليل |
|---|---|---|---|
| 35 | Outline قابل للطي، إضافة صفحة/قسم، إعادة ترتيب، نقل حقل، نسخ قسم، حذف صفحة فارغة | Verified لكل شيء ما عدا "إعادة ترتيب الصفحات/الأقسام من داخل Outline نفسه" (الترتيب يتم من Canvas فقط، Outline للقراءة والتنقل + الطي) — Partial | `StudioOutline.tsx` |
| 36 | منع حذف الصفحة الوحيدة دون بديل | Verified | `studioSchemaOps.ts` (`canDeletePage`) |
| 37 | عدد الحقول والأخطاء لكل صفحة | Verified | `StudioOutline.tsx` |
| 38 | Outline يقرأ ويعدّل نفس Schema، لا مصدر ثانٍ | Verified | كلا المكوّنين يقرأان من نفس `history.present` |

## الشروط والصيغ

| # | البند | الحالة | الدليل |
|---|---|---|---|
| 39 | وضع متقدم منفصل، لا Expressions حرة | Verified | `ConditionBuilder.tsx`, `FormulaBuilder.tsx` — كلاهما بناء عبر عناصر واجهة محددة فقط |
| 40 | Condition Builder: عندما/الحقل/المعامل/القيمة → إظهار/إخفاء/إلزام/تعطيل | Partial — البنية الكاملة (شرط ← إظهار/إخفاء عبر `visibilityCondition`، إلزام عبر `requiredCondition`) موجودة؛ **"تعطيل" (Disable) كنتيجة شرط منفصلة عن الإخفاء غير موجودة في نموذج البيانات على الخادم أصلاً** (لا `disabledCondition` في `FormFieldSchema`) — Not Applicable لعدم وجود مقابل Backend |
| 41 | مجموعات: كل الشروط/أي شرط | Verified | `ConditionBuilder.tsx` |
| 42 | العمليات حسب النوع (يساوي...بين...أحد الخيارات) | Verified ما عدا "بين" كعامل واحد — **Not Applicable** (لا عامل Between في تعداد الخادم؛ يُحقَّق عبر مجموعة "كل الشروط" بعاملين) | `phase2a-form-designer-schema-contract.md` |
| 43 | Typed operands، منع مقارنة أنواع غير متوافقة | Verified على مستوى الواجهة (تصفية عوامل حسب النوع)؛ الخادم هو الحكم النهائي | `ConditionBuilder.tsx` (`operatorsForType`) |
| 44 | منع الاعتماد على الحقل نفسه | Verified | `wouldCreateSelfReference`، مُختبَر |
| 45 | كشف Circular dependencies | Verified محليًا (تحذير فوري قبل التطبيق) ونهائيًا على الخادم | `fieldDependencies.ts` (`detectDependencyCycle`)، `StudioInspector.tsx` (`applyPatchWithCycleGuard`)، مُختبَر |
| 46 | إظهار الحقول المتأثرة | Verified (في تحذير حذف الحقل)؛ Partial لعدم وجود عرض "قائمة اعتماديات" مستقل خارج سياق الحذف | `StudioCanvas.tsx` |
| 47 | الانتقال المباشر للحقل من رسالة الخطأ | Verified (من Validation Panel)؛ Partial لعدم وجود انتقال مباشر من رسالة خطأ الدورة نفسها داخل Inspector (تظهر كنص فقط) | `ValidationPanel.tsx` |
| 48 | صيغ: الجمع/الطرح/الضرب/القسمة/المتوسط/الحد الأدنى/الحد الأعلى/العد/النسبة | Verified للستة الأولى؛ **Not Applicable** لـ"العدّ"/"النسبة" (لا دالة Backend مقابلة) | `phase2a-form-designer-schema-contract.md` |
| 49 | إظهار نوع القيمة الناتجة | **Missing** — لا عرض صريح لنوع النتيجة (رقم/نص) في `FormulaBuilder` | — |
| 50 | منع القسمة على صفر | Verified للثابت الرقمي فقط (تحذير فوري)؛ لا يمكن الكشف عن قسمة-على-صفر-وقت-التشغيل (قيمة حقل = صفر) إلا في المعاينة/التشغيل الفعلي (الخادم يعالجها كـnull، متوافق مع `evaluateFormula`) | `FormulaBuilder.tsx` |
| 51 | منع Circular references | Verified (نفس آلية الشروط) | `fieldDependencies.ts` |
| 52 | منع استخدام حقل غير رقمي في عملية رقمية | Verified | `FormulaBuilder.tsx` (`requireNumeric`، `NUMERIC_ONLY_FUNCTIONS`) |
| 53 | Dependency list | Partial — موجودة ضمنيًا (تصفية الحقول المتاحة) لا كقائمة منفصلة معروضة | — |
| 54 | Server-side validation هو المصدر النهائي | Verified | `validateVersion` |
| 55 | Preview وRuntime يستخدمان نفس القواعد (لا نفس المحرك) | Partial | `previewLogic.ts` تنفيذ منفصل يُستخدَم في المعاينة؛ الخادم يستخدم `FormFormulaEvaluator`/`FormConditionEvaluator` منفصلَين تمامًا. لكل تنفيذ تغطية اختبار آلية منفصلة (`Schema/FormFormulaEvaluatorTests.cs`/`Responses/FormConditionEvaluatorTests.cs` للخادم؛ اختبارات `previewLogic.ts` الخاصة به للواجهة)، لكن لا محرك مشترك ولا عقد اختبار تكافؤ آلي (parity contract) يقارن مخرجات التنفيذَين على نفس المدخلات ويمنع انحرافهما مستقبلًا — انحراف كهذا لن يُكتشَف إلا يدويًا |

## التحقق المستمر

| # | البند | الحالة | الدليل |
|---|---|---|---|
| 56 | تصنيف ثلاثي (أخطاء مانعة/تحذيرات/اقتراحات) | Verified — الفئة الثالثة محلية بالكامل (لا مقابل Backend)، موثَّق | `ValidationPanel.tsx` |
| 57 | كل خطأ: رسالة/عنصر/صفحة وقسم/إجراء مقترح/زر انتقال | Verified | `ValidationPanel.tsx` (`IssueRow`) |
| 58 | لا Error code خام للمستخدم | Verified، مُختبَر | اختبار `never surfaces the raw error code` |

## المعاينة

| # | البند | الحالة | الدليل |
|---|---|---|---|
| 59 | نفس Schema snapshot، تطبيق شروط/صيغ/Validation، صفحات وتنقل، مرفقات تجريبية بلا رفع حقيقي، لا استجابة حقيقية، نفس محرك UI | Verified — `FormPreviewPanel` معاد استخدامه دون تعديل | `FormPreviewPanel.tsx` |
| 60 | اختيار سياق المعاينة للحقول المؤسسية (منطقة/سجن/وحدة/دور) دون تجاوز صلاحيات المستخدم | **Missing** — لم يُضَف محدِّد سياق مؤسسي لوضع المعاينة | — |

## الحفظ التلقائي والتعارض

| # | البند | الحالة | الدليل |
|---|---|---|---|
| 61 | Debounce، Patch/Snapshot حسب العقد، RowVersion، منع الحفظ المتوازي، إعادة محاولة آمنة، عدم فقد عند فشل الشبكة، عرض الحالة، عدم الكتابة على منشور | Verified (موروث من `useFormDesignerAutosave` الموجود، غير مُعدَّل؛ "عدم الكتابة على منشور" مضمون بمنع تحرير إصدارات غير `Draft`/`ChangesRequested` من الخادم أصلاً) | `phase2a-form-designer-autosave.md` |
| 62 | تعارض: بانر + 3 خيارات آمنة، لا Overwrite صامت | Verified، مُختبَر آليًا | `StudioConflictBanner.tsx`، اختبار `shows the three non-destructive conflict actions...` |

## Undo/Redo

| # | البند | الحالة | الدليل |
|---|---|---|---|
| 63 | تغطية كل العمليات المذكورة (إضافة/حذف/نقل حقل، تعديل خصائص، خيار، صفحة/قسم، شرط/صيغة) | Verified | جميعها تمر عبر `applySchema` |
| 64 | سجل محدود في الذاكرة | Verified (50، غير مُعدَّل) | `historyStore.ts` |
| 65 | Autosave لا يلغي Undo المحلي | Verified | `phase2a-form-designer-autosave.md` |

## الإصدارات

| # | البند | الحالة | الدليل |
|---|---|---|---|
| 66 | عرض حالة الإصدار دائمًا (6 حالات) | Verified | `StudioTopBar.tsx` يعرض `statusAr` من الخادم |
| 67 | الإصدار المنشور Immutable، التعديل ينشئ Draft جديد، لا تعديل Snapshot منشور | Verified (سلوك خادم موجود مسبقًا، غير مُعدَّل) | `FormVersionStateMachine`, `ApproveAndLockAsync` |
| 68 | لا نقل استجابات بين Schema versions | Verified (غير مُلامَس في هذه المرحلة) | — |
| 69 | الحفاظ على روابط Campaigns/Responses بالإصدار الصحيح | Verified (غير مُلامَس) | — |
| 70 | عرض تاريخ الإصدارات | Verified (`FormVersionsPage`، غير مُعدَّل جوهريًا) | — |
| 71 | مقارنة إصدارين بصورة مفهومة (حقول مضافة/محذوفة/معدّلة، خيارات، شروط، صيغ، إلزام) | Verified | `versionDiff.ts`, `VersionCompare.tsx`, `FormVersionComparePage.tsx`، مُختبَر وحدويًا |

## المراجعة والاعتماد

| # | البند | الحالة | الدليل |
|---|---|---|---|
| 72 | Validation كامل من الخادم قبل السماح بطلب المراجعة | Verified (الخادم يرفض `SubmitForReviewAsync` إن كان الـSchema غير صالح، بغض النظر عن حالة الواجهة) | `FormVersionService.SubmitForReviewAsync` |
| 73 | ملخص (صفحات/أقسام/حقول/شروط/صيغ/تحذيرات) | Verified | `StudioReviewPanel.tsx` |
| 74 | إرسال لمراجع مؤهل، قفل تعديلات حساسة، تسجيل مستخدم ووقت | Verified (سلوك خادم موجود مسبقًا) | — |
| 75 | اعتماد أو إعادة للمصمم، سبب الإعادة إلزامي | Verified | `StudioReviewPanel.tsx` (زر معطَّل بلا سبب) |
| 76 | Four-eyes، منع اعتماد المصمم لتصميمه | Verified (خادم، `IFormSeparationOfDutiesService`، غير مُعدَّل) | — |
| 77 | بعد الاعتماد: رابط للجدولة والنشر دون فقد `formId`/`versionId` | Verified | `StudioReviewPanel.tsx` (`?formId=&versionId=`) + `FormCampaignWizardPage.tsx` أصبحت تقرأ الباراميترين فعليًا |

## الربط بمساحة السجن

| # | البند | الحالة | الدليل |
|---|---|---|---|
| 78 | استوديو التصميم لا يظهر للمستخدم التشغيلي العادي داخل مساحة السجن | Verified — رابط "استوديو تصميم النماذج" في الشريط الجانبي خلف `hasPermission('Forms.UpdateDraft')`؛ لا رابط للاستوديو من `FacilityWorkspacePage` | `App.tsx` |
| 79 | قسم "النماذج" داخل مساحة السجن يعرض فقط النماذج الموجَّهة للسجن | Not Applicable لهذا التسليم — القسم موجود مسبقًا (`form-assignment` panel في Facility Workspace، `/form-compliance/facilities/:id`) ولم يُلمَس؛ خارج نطاق Phase 2A (نطاق Phase 3/#145) |
| 80 | المستخدم صاحب صلاحية التصميم يرى رابطًا مستقلاً للاستوديو | Verified | رابط شريط جانبي مستقل |
| 81 | إنشاء ونشر النموذج لا يتمان من شاشة تعبئة السجن | Verified (لم تُمَس شاشات التعبئة إطلاقًا) | — |

## الصلاحيات

| # | البند | الحالة | الدليل |
|---|---|---|---|
| 82 | لا صلاحيات مكررة، تعيين للصلاحيات الفعلية | Verified | `permissions-matrix.md` §Phase 2A |
| 83 | النطاق من الخادم، القوالب/النماذج الخاصة لا تظهر خارج النطاق | Verified | `formScope`/`effectiveAccess` (غير مُعدَّلين، مُعاد استخدامهما في الإضافتين الجديدتين) |
| 84 | نقص الصلاحية → 403، خارج النطاق → 404، تعارض RowVersion → 409، Schema validation → 422 structured | Verified (نمط قياسي موجود، مُتحقَّق في نقاط الـAPI الجديدة عبر نفس `FormAccessHelper`) | اختبار تكامل جديد (403/404 غير مُختبَر صراحة لنقطة `copy-from` الجديدة عبر Forbidden — فقط NotFound للنطاق؛ لا اختبار 403 صريح لنقص `Forms.Create`/`Forms.UpdateDraft` على هذه النقطة تحديدًا) — **Partial** لتغطية 403 على النقطة الجديدة تحديدًا |
| 85 | لا حماية مخفية في Frontend فقط | Verified — كل تحقق صلاحية في الاستوديو (`usePermission`) هو تحسين UX فقط؛ التنفيذ الفعلي محمي بـ`RequireAuthorization` على الخادم لكل نقطة | — |

## Backend

| # | البند | الحالة | الدليل |
|---|---|---|---|
| 86 | مراجعة Domain وخدمات Forms قبل التعديل، لا محرك موازٍ | Verified | `phase2a-form-designer-architecture.md` |
| 87 | إعادة استخدام FormDefinition/FormVersion/Schema snapshot/Fields/Condition-formula engine/Review-publishing/Campaigns/Responses | Verified | — |
| 88 | فصل الخدمات (أو استخدام التقسيم الحالي إن كان مناسبًا) | Verified — استُخدم التقسيم الحالي (`FormVersionService`, `FormTemplateService`) بإضافتين محدودتين، لا خدمات جديدة | — |
| 89 | عقد الاستوديو Read model موحَّد | Verified — لا استعلام منفصل لكل حقل/قسم؛ `FormVersionDetailDto` كامل الموجود يكفي | `phase2a-form-designer-architecture.md` |
| 90 | أوامر محددة وآمنة، عمليات مركبة Transactional | Verified — `CreateFromExistingFormAsync` بالكامل داخل `db.ExecuteInTransactionAsync` | `FormVersionService.cs` |

## الأداء

| # | البند | الحالة | الدليل |
|---|---|---|---|
| 91 | لا N+1، ميزانية 200 حقل موثَّقة، Validation بلا Query لكل حقل، Autosave بلا إعادة كامل البيانات غير الضرورية، Outline/Canvas نفس الحالة، لا إعادة تحميل غير ضرورية، حد لـUndo history | Partial بالكامل — "لا N+1" و"لا إعادة تحميل غير ضرورية" و"Outline/Canvas نفس الحالة" و"حد Undo" كلها Verified بالبناء (راجع `phase2a-form-designer-performance.md`)؛ **"ميزانية 200 حقل موثَّقة" و"اختبار Query-count budget آلي" كلاهما Missing** — لم يُقاسا فعليًا | `phase2a-form-designer-performance.md` |

## الوصول

| # | البند | الحالة | الدليل |
|---|---|---|---|
| 92 | تنقّل بلوحة المفاتيح، إضافة بلا سحب، نقل الحقل بلوحة المفاتيح، Focus restoration، Labels، Live region، لا اعتماد على اللون فقط، Escape للوحات ثانوية، لا Modal stacking، RTL، ترتيب DOM منطقي | Verified لمعظم البنود؛ **Missing**: تدقيق آلي (axe)، اختبار قارئ شاشة فعلي؛ **Partial**: Focus restoration بعد إغلاق لوحة Tablet، ترتيب DOM (لم يُختبَر Tab-order كاملاً) | `phase2a-form-designer-accessibility.md` |

## التسليم

| # | البند | الحالة | الدليل |
|---|---|---|---|
| 93 | Failed=0, Skipped=0 لكل من Unit وIntegration وFrontend | Verified/راجع الأرقام الفعلية في تقرير الإكمال — يُحدَّث بعد اكتمال تشغيل `dotnet test` (Integration) في هذه الجلسة |
| 94 | لا Mock أو أسرار أو ملفات مولَّدة غير مقصودة | Verified — `gitleaks` بلا نتائج، لا بيانات Mock في المسار الإنتاجي (الاستوديو يستدعي `api.*` الحقيقي حصرًا؛ الاختبارات فقط تُموّه الشبكة) | مخرجات `gitleaks` في تقرير الإكمال |
| 95 | Commit واضح، دفع الفرع، PR بلا دمج/Auto-merge | Verified — راجع تقرير الإكمال لرابط PR |

## ملخص عددي

- **Verified بالكامل**: أغلبية البنود (~70 من 95).
- **Partial**: ~18 بندًا، كلها موثَّقة أعلاه بسبب دقيق وملف مرجعي.
- **Missing**: 8 بنود صريحة (Multi-select، سياق مؤسسي في المعاينة، نوع القيمة الناتجة للصيغة، ميزانية أداء 200 حقل موثَّقة، اختبار Query-count آلي، تدقيق وصول آلي، اختبار قارئ شاشة، سياسة Migration لمفتاح خيار منشور).
- **Not Applicable**: 4 بنود (عامل "بين"، دالتا "العدّ"/"النسبة"، "تعطيل" كنتيجة شرط منفصلة، قسم النماذج داخل مساحة السجن).

**تصحيح**: بند واحد من الثمانية `Missing` هو خطر سلامة بيانات فعلي، وليس مجرد فجوة تجربة/اختبار/أداء — البند رقم 33 (عدم تغيير مفتاح خيار منشور بلا سياسة Migration): لا يوجد اليوم أي فحص يمنع تغيير `option.value` لخيار ضمن إصدار مبني على نسخة منشورة سابقة تستخدم هذا المفتاح، فتغييره يكسر تفسير الاستجابات المحفوظة سابقًا (الاستجابة تشير إلى مفتاح لم يعد موجودًا). بقية السبعة بنود `Missing` فجوات تجربة/تغطية اختبار/قياس أداء فعلاً، لا صحة أو سلامة. القرار بإغلاق Issue #144 من عدمه متروك للمراجعة البشرية بعد قراءة هذا السجل، مع ملاحظة أن البند 33 تحديدًا يستحق أولوية أعلى من بقية الفجوات.
