# Phase 2A — تقرير الإكمال: استوديو تصميم النماذج الموحَّد

- **Branch**: `ux-rescue-phase2a-unified-form-designer-studio`
- **Continues**: #144 (لا يُغلَق بهذا التسليم)، #45، #12، #13
- **PR**: يُحدَّث بعد فتحه (انظر نهاية هذا الملف)
- **Commit SHA**: `6fe8622d16b51bff2339541a811509302bb0bd47`

## ملخص تنفيذي

أُعيد بناء منشئ النماذج ليصبح استوديو تأليف موحَّدًا واحدًا على `/forms/designer/new` و`/forms/designer/:formId`، يدمج ما كان يتطلب سابقًا 7 Routes منفصلة (إنشاء نموذج، إنشاء أول إصدار، تصميم الحقول، التحقق، المعاينة، طلب المراجعة، مراجعة الإصدار) في مكان واحد. الفجوة الحرجة الوحيدة المؤكَّدة مسبقًا في `form-designer-gap-analysis.md` — غياب أي واجهة لبناء شروط الظهور/الإلزام وصيغ الحساب رغم اكتمال نموذج البيانات ومحرك التقييم على الخادم — أُغلقت عبر `ConditionBuilder` و`FormulaBuilder` جديدين بالكامل، آمنين ومحددي الأنواع، بلا Expressions حرة.

لم يُبنَ أي محرك موازٍ: كل منطق الحفظ التلقائي (`useFormDesignerAutosave`)، التراجع/الإعادة (`historyStore`)، تقييم الشروط/الصيغ للمعاينة (`previewLogic`)، والتحقق النهائي (`FormSchemaValidator` على الخادم) أُعيد استخدامه كما هو دون تعديل. أُضيفت نقطتا Backend محدودتان فقط: نسخ نموذج موجود عبر النماذج (Copy)، ومعاينة قالب قبل الاستخدام (Preview).

سجل الامتثال الكامل (95 بندًا) في `phase2a-form-designer-compliance-ledger.md` يوثّق **8 بنود Missing صريحة** (لا خطر بيانات/أمان في أي منها — كلها فجوات تجربة/تغطية اختبار/قياس أداء) و**~18 بندًا Partial**. **لذلك لا يُغلَق Issue #144 بهذا التسليم.**

## Routes: دُمجت أو حُوِّلت

راجع `phase2a-form-designer-route-transition.md` للتفصيل الكامل. ملخص:
- **جديد (Keep)**: `/forms/designer/new`, `/forms/designer/:formId`, `/forms/:formId/versions/compare`.
- **Redirect حقيقي (لا حذف)**: `/forms/:formId/versions/:versionId/edit` و`/forms/:formId/versions/new` → الاستوديو، مع اختبار مؤتمت (`App.route-redirects.test.tsx`).
- **Advanced fallback (بلا حذف)**: `/forms/new`, `/forms/:id/edit`, `/forms/:id/review`, `/forms/:formId/versions/:versionId/review`, `/form-templates`.
- إجمالي Routes في `App.tsx`: **65** (كان 62)، `npm run check:ux-routes` **passed**.

## Frontend implementation

- 8 مكوّنات استوديو جديدة تحت `src/frontend/src/pages/forms/studio/`: `FormDesignerStudioPage`, `StudioStartFlow`, `StudioTopBar`, `StudioCanvas`, `StudioInspector`, `StudioOutline`, `StudioFieldLibrary`, `StudioReviewPanel`, `StudioMobileReview`, `StudioConflictBanner`.
- 7 وحدات منطق/مكوّنات مشتركة جديدة تحت `src/frontend/src/forms/designer/`: `ConditionBuilder`, `FormulaBuilder`, `ValidationPanel`, `VersionCompare`, `fieldDependencies`, `versionDiff`, `studioSchemaOps`, `fieldLibrary`, `useUnsavedChangesGuard`, `useResponsiveStudioLayout`.
- صفحة جديدة: `FormVersionComparePage`.
- أُزيلت 5 ملفات كود أصبحت غير موجَّه إليها أي Route: `FormDesignerPage.tsx` (+اختباره)، `DesignerCanvas.tsx`, `DesignerPalette.tsx`, `DesignerPropertiesPanel.tsx`, `DesignerToolbar.tsx` — تحقَّق عبر بحث نصي كامل أنه لا استيراد آخر لها.
- إصلاحان جانبيان: `FormTemplatesPage` كانت تستخدم `window.location.assign` (إعادة تحميل كاملة) → SPA navigation؛ `FormCampaignWizardPage` أصبحت تقرأ `?formId=&versionId=` من رابط "الانتقال للجدولة والنشر" بدل تجاهلهما.

## Backend implementation

- `IFormVersionService.CreateFromExistingFormAsync` (نسخ نموذج موجود عبر النماذج) — يعيد استخدام `IFormCommandService.CreateDraftAsync` و`AllocateAndPersistVersionAsync` الموجودين، ضمن معاملة واحدة، مع فحص صلاحية عرض على النموذج المصدر وتسجيل تدقيق يحمل هوية المصدر.
- `IFormTemplateService.GetSchemaAsync` (معاينة قالب) — يعيد استخدام قواعد رؤية القوالب المستخرجة من `ListAsync` سابقًا في دالة مشتركة `BuildVisibleTemplatesQueryAsync`.
- نقطتا API جديدتان: `POST /api/v1/forms/copy-from/{sourceFormId}/{sourceVersionId}`، `GET /api/v1/form-templates/{templateId}/schema`.
- لا خدمة Backend جديدة مستقلة، لا محرك موازٍ، لا Migration قاعدة بيانات.

## عقد Schema والإصدارات

لم يتغيّر. مطابقة رقمية كاملة تحقَّقت مباشرة بين `schemaTypes.ts` وتعدادات `FormSchemaModels.cs`. راجع `phase2a-form-designer-schema-contract.md` للتفصيل، بما فيه البنود المطلوبة في نص التكليف بلا مقابل Backend (عامل "بين"، دالتا "العدّ"/"النسبة"، أنواع حقول وهمية) — لم تُختلَق، مُوثَّقة كـNot Applicable.

## سلوك الحفظ التلقائي

راجع `phase2a-form-designer-autosave.md`. أُعيد استخدام `useFormDesignerAutosave` كما هو. أُضيف `StudioConflictBanner` بثلاثة خيارات آمنة عند 409 (تحميل الأحدث / مقارنة التغييرات / حفظ نسخة جديدة)، مُختبَر آليًا. حارس مغادرة (`beforeunload`) جديد، مع فجوة موثَّقة صراحةً: لا `useBlocker` لتنقّل SPA عشوائي داخل التطبيق (يتطلب Data Router، خارج نطاق هذه المرحلة).

## محرك التحقق

راجع `phase2a-form-designer-validation.md`. `ValidationPanel` جديد، مصنَّف (أخطاء مانعة/تحذيرات/اقتراحات محلية)، بلا Error code خام للمستخدم، مع انتقال مباشر للعنصر. الخادم هو المصدر النهائي دائمًا.

## الشروط والصيغ

`ConditionBuilder` و`FormulaBuilder` جديدان بالكامل، مبنيان فوق `schemaTypes.ts`/الخادم الموجودين. كشف الدورات ومنع الاعتماد الذاتي على مستويين (فوري في الواجهة + نهائي على الخادم)، مُختبَران وحدويًا (فحوصات دورة مباشرة/ذاتية/متعدية عبر 3 حقول).

## الوصول (Accessibility)

راجع `phase2a-form-designer-accessibility.md`. أُصلح خلل تصميم اكتُشف أثناء البناء (زر متداخل داخل `role="button"` في صف الحقل) قبل التسليم. **Missing صراحةً**: تدقيق آلي (axe) واختبار قارئ شاشة فعلي — لم يُنفَّذا في هذه الجلسة.

## الأداء وعدد الاستعلامات

راجع `phase2a-form-designer-performance.md`. لا N+1 بالبناء (تحميل الاستوديو = استعلامان فقط). **Missing صراحةً**: ميزانية موثَّقة لنموذج 200 حقل، واختبار Query-count budget آلي — لم يُقاسا فعليًا.

## Unit tests

```text
dotnet test src/backend/tests/Baseera.UnitTests/Baseera.UnitTests.csproj -c Release --no-build --logger "console;verbosity=minimal"
Passed!  - Failed: 0, Passed: 990, Skipped: 0, Total: 990
```

لم تُضَف اختبارات وحدة Backend جديدة (الإضافتان الجديدتان مُغطَّيتان تكامليًا فقط — أصح لأنهما تلمسان قاعدة بيانات ونطاقًا حقيقيًا)؛ الرقم أعلاه يؤكد أن كل الاختبارات الحالية (990) ما زالت تعمل دون كسر شيء.

## Integration tests

```text
dotnet test src/backend/tests/Baseera.IntegrationTests/Baseera.IntegrationTests.csproj -c Release --no-build --logger "console;verbosity=minimal"
(مع BASEERA_TEST_CONNECTION موجَّه لحاوية SQL Server المحلية baseera-sql)
Passed!  - Failed: 0, Passed: 263, Skipped: 0, Total: 263, Duration: 30 m 9 s
```

اختباران جديدان في `FormsVersionIntegrationTests.cs`: `Copy_from_existing_form_seeds_new_draft_and_records_audit_provenance` (يغطي النسخ عبر النماذج، تدقيق المصدر، ورفض النسخ من خارج النطاق بـ404)، وتوسيع `Template_list_respects_scope_visibility_and_ownership` (معاينة قالب تتبع نفس قواعد رؤية القائمة تمامًا).

## Frontend tests

```text
npm run test -- --run
Test Files  63 passed (63)
     Tests  343 passed (343)
```

7 ملفات اختبار جديدة (35+ حالة اختبار جديدة): `fieldDependencies.test.ts`, `versionDiff.test.ts`, `ConditionBuilder.test.tsx`, `FormulaBuilder.test.tsx`, `ValidationPanel.test.tsx`, `FormDesignerStudioPage.test.tsx`, `App.route-redirects.test.tsx`. راجع `phase2a-form-designer-test-matrix.md` للتفصيل الكامل، بما فيه البنود المطلوبة في نص التكليف التي لم تُختبَر آليًا بعد (موثَّقة صراحةً).

## Build

- Backend: `dotnet build src/backend/Baseera.slnx -c Release` — **Build succeeded, 0 Errors** (تحذيرات موجودة مسبقًا فقط، لا تحذير جديد من كود هذه المرحلة).
- Frontend: `npm run typecheck` (0 أخطاء)، `npm run lint` (0 أخطاء، تحذيرات `only-export-components` موجودة مسبقًا فقط بنفس النمط)، `npm run build` (نجح، مع تحذير حجم Chunk موجود مسبقًا، لا خطأ).

## Security checks

- `npm audit --audit-level=high`: **0 vulnerabilities**.
- `bash scripts/check-nuget-vulnerabilities.sh src/backend/Baseera.slnx`: **لا ثغرات High/Critical**.
- `gitleaks detect --source . --config .gitleaks.toml --no-banner`: **لا تسريبات** (276 Commit، ~15MB مُفحوصة).
- `git diff --check`: **بلا أخطاء مسافات بيضاء**.

## SonarCloud / CodeRabbit

يُطلب مراجعة CodeRabbit بعد فتح الـPR (راجع قسم PR أدناه)؛ نتائج SonarCloud تُضاف إن كانت مُفعَّلة على المستودع (لم يُتحقَّق من تفعيلها ضمن هذه الجلسة).

## Verified / Partial / Missing — الخلاصة

راجع `phase2a-form-designer-compliance-ledger.md` للسجل الكامل (95 بندًا). **8 بنود Missing صريحة** لا تشكّل خطر بيانات/أمان: Multi-select في Canvas، سياق مؤسسي في المعاينة، نوع القيمة الناتجة في Formula Builder، ميزانية أداء 200 حقل موثَّقة، اختبار Query-count آلي، تدقيق وصول آلي (axe)، اختبار قارئ شاشة فعلي، سياسة Migration لمفتاح خيار منشور.

## نطاق Phase 2B المتبقي (مقترح)

- إغلاق البنود الـ8 الـMissing أعلاه.
- Multi-select في Canvas للعمليات الآمنة (نسخ/حذف/نقل جماعي).
- نقل حقل بين أقسام/صفحات فعليًا من الواجهة (المنطق موجود في `moveFieldAcrossSections`، غير مربوط بزر بعد).
- محرر واجهة لقواعد Validation المخصصة (`validationRules`) — موجودة في نموذج البيانات، بلا محرر UI.
- تدقيق وصول آلي (axe-core) مضاف لمجموعة الاختبارات.
- اختبار أداء فعلي لنموذج 200 حقل مع ميزانية زمنية موثَّقة.
- التفكير في ترحيل `main.tsx` إلى React Router Data Router لدعم `useBlocker` عبر تنقّل SPA عشوائي — قرار معماري أكبر يستحق تصميمًا منفصلاً.

---

**PR URL**: يُحدَّث بعد الفتح (راجع رابط الـPR في رسالة التسليم النهائية)
**Branch**: `ux-rescue-phase2a-unified-form-designer-studio`
**Commit SHA**: `6fe8622d16b51bff2339541a811509302bb0bd47`
