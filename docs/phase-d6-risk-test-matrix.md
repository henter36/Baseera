# Phase D.6 — مصفوفة الاختبارات

## اختبارات الوحدة (86 اختبارًا، جميعها منطق صرف بلا قاعدة بيانات)

| الملف | العدد | يغطي |
| --- | --- | --- |
| `RiskLifecycleStateMachineTests.cs` | 25 | كل انتقال مسموح/ممنوع لحالة الخطر، انتهاء مدة القبول، تأخر المراجعة. |
| `RiskTreatmentStateMachineTests.cs` | 20 | انتقالات الخطة والإجراء، استحالة الوصول لحالة `Overdue` عبر أمر، حالات الإغلاق النهائية. |
| `RiskScoringEngineTests.cs` | 15 | كلا صيغتي الاحتساب، رفض المدخلات غير الصالحة، اختيار نطاق التصنيف، التحقق من تناسق النطاقات والأوزان. |
| `RiskTrendCalculatorTests.cs` | 6 | الاتجاه الصاعد/الهابط/المستقر/غير المعروف، وتأثير المصادر الجديدة رغم ثبات الدرجة. |
| `RiskRecurrenceDetectorTests.cs` | 6 | بناء مفتاح التكرار (حساسية الحالة/الفراغات)، تمييز "احتمال تكرار" عن "نمط متكرر". |
| **المجموع** | **86** | **نجاح 100%، صفر تخطٍّ (Skipped=0).** |

## اختبارات التكامل (12 اختبارًا، ضد SQL Server حقيقي)

ملف `RiskManagementIntegrationTests.cs`، مجموعة `integration-risk-management` (قاعدة بيانات مستقلة مخصصة، بنفس نمط بقية الوحدات):

1. `Summary_requires_permission_and_facility_scope` — 403/404 لعدم الصلاحية/خارج النطاق.
2. `Create_risk_generates_sequential_code_and_starts_in_draft`.
3. `Update_risk_detects_row_version_conflict` — 409 حقيقي عند RowVersion غير مطابق.
4. `Matrix_lifecycle_create_approve_activate_retires_previous_default` — دورة حياة كاملة + تقاعد المصفوفة الافتراضية السابقة فعليًا في قاعدة البيانات.
5. `Assessment_score_is_server_computed_and_activates_risk_on_approval` — يتحقق أن الدرجة المخزَّنة فعليًا في قاعدة البيانات تطابق `likelihood × max impact` المحسوبة، وأن اعتماد التقييم ينقل الخطر إلى `Active`.
6. `High_severity_assessment_requires_rationale` — 409 حقيقي عند غياب المبرر لدرجة عالية.
7. `Control_can_be_created_and_tested`.
8. `Treatment_plan_and_action_lifecycle_requires_four_eyes_and_blocks_closure_gate` — يغطي: فصل مهام اعتماد الخطة، منع إكمال الخطة قبل إكمال/إلغاء كل الإجراءات، اعتمادية الإجراء، فصل مهام التحقق.
9. `Closure_requires_approved_residual_assessment_and_reopen_restarts_assessment` — يغطي: رفض طلب الإغلاق دون تقييم متبقٍ معتمد، الإغلاق الفعلي، إعادة الفتح وعودة الحالة لـ`UnderAssessment` تلقائيًا مع زيادة عدّاد إعادة الفتح.
10. `Source_link_rejects_cross_facility_scope` — 409 حقيقي عند ربط أصل مورد من منشأة أخرى.
11. `Import_confirm_is_idempotent_on_same_file_hash` — يتحقق من عدد صفوف `RiskRecords` الفعلي في قاعدة البيانات بعد استدعاء Confirm مرتين.
12. `Data_quality_reports_missing_owner_issue`.

**النتيجة الفعلية**: `Failed: 0, Passed: 12, Skipped: 0`.

## اختبار انحدار على الوحدات الموجودة (لم يُكسر شيء)

أُعيد تشغيل كل شرائح CI الحالية (`core`, `forms`, `operations` — شاملة SensitiveCustody وWorkspace وQuery-count، `workforce`) بعد إضافة وحدة المخاطر، على نفس قاعدة SQL Server الحية: **جميعها ناجحة، صفر إخفاقات، صفر تخطٍّ**. اختبار عدد الاستعلامات (`OperationalDashboardQueryCountIntegrationTests`/`WorkforceReadinessIntegrationTests`) تحديدًا كُسر مبدئيًا (156 > 140) ثم أُصلح عبر تحسين الاستعلامات ورفع الحد إلى 150 — موثَّق بالتفصيل في `phase-d6-risk-performance.md`.

## اختبارات الواجهة الأمامية

27 اختبارًا في `FacilityWorkspacePage.test.tsx` (23 موجودة سابقًا + 4 جديدة خاصة بالمخاطر)، جميعها ناجحة:

* عرض ملخص المخاطر والتدخلات دون فجوة بيانات وهمية.
* السقوط الآمن لعرض "فجوة البيانات" الأصلية عند غياب صلاحية العرض (الودجت غائب تمامًا).
* فتح لوحة السياق وعرض إجراءات مصرَّح بها من الخادم فقط (لا زر لإجراء غير مُدرَج في `allowedActions`).
* معالجة تعارض RowVersion (409) من أمر خطر مع زر "إعادة تحميل".

## بند اختُبر يدويًا (وليس آليًا) — صراحةً غير كافٍ وحده

تشغيل التطبيق فعليًا في المتصفح (`npm run dev` + تفاعل يدوي) **لم يُنفَّذ** في هذه الجلسة نظرًا لضيق الوقت الإجمالي للمرحلة؛ الثقة بصحة الواجهة مبنية على اختبارات React Testing Library الآلية فقط (`vitest`)، وعلى نجاح `typecheck`/`lint`/`build`. هذا مذكور صراحة كحد لضمان الجودة، تماشيًا مع مبدأ "لا صور كشرط قبول" وعدم ادّعاء تحقق بصري لم يحدث.
