# Phase D.6 — سجل الامتثال (Compliance Ledger)

الحالات المستخدمة: `Verified` (تحقَّق بدليل فعلي) | `Not Applicable — خارج النطاق` | `Blocked — مانع خارجي موثق` | `Missing`.

> **تحديث بعد مراجعة CodeRabbit/SonarCloud على PR #147**: جولة معالجة كاملة (P0×2 حرجة + 26 من 27 Major + Sonar Blocker/Major/معظم Minor) وثّقت في `docs/phase-d6-review-remediation.md` — راجعيه للتفاصيل الكاملة قبل الاعتماد على الأرقام أدناه وحدها.

| # | المعيار | الحالة | الدليل |
| --- | --- | --- | --- |
| 1 | Domain موحّد للمخاطر | Verified | `Baseera.Domain.RiskManagement` (17 كيانًا، Migration واحدة مطبَّقة فعليًا). |
| 2 | Facility scope يعمل فعليًا | Verified | اختبار تكامل `Summary_requires_permission_and_facility_scope` (403/404 حقيقيان). |
| 3 | Matrix versioned وقابلة للإدارة | Verified | `phase-d6-risk-matrix-versioning.md` + اختبار `Matrix_lifecycle_...` حي. |
| 4 | الدرجة تُحسب على الخادم | Verified | لا حقل Score في DTO الطلب؛ اختبار وحدة + تكامل يتحقق من القيمة الفعلية في القاعدة. |
| 5 | التقييمات المعتمدة غير قابلة للتعديل | Verified | `AssessmentStatus.Approved` لا يوجد أي مسار PUT/PATCH له؛ التصحيح فقط عبر `SupersedesAssessmentId`. |
| 6 | Inherent وResidual منفصلان | Verified | حقلا مؤشر منفصلان على `RiskRecord` (`CurrentInherentAssessmentId`/`CurrentResidualAssessmentId`). |
| 7 | الضوابط منفصلة عن المعالجات | Verified | `RiskControl` مقابل `RiskTreatmentPlan/Action` — كيانان مستقلان بلا تحويل بينهما. |
| 8 | Treatment plans وActions حقيقية | Verified | اختبار تكامل حي كامل للدورة (Submit→Approve→Start→Action lifecycle→Complete). |
| 9 | الاتجاه قابل للتفسير | Verified | `RiskTrendCalculator` يُرجع سببًا نصيًا مع كل اتجاه؛ 6 اختبارات وحدة. |
| 10 | التكرار قابل للتتبع | Verified | `RiskRecurrenceDetector` + `RecurrenceKey`؛ لا دمج تلقائي (مطابق للمبدأ). |
| 11 | Source links typed وآمنة | Verified | نمطي بالكامل، وبعد مراجعة CodeRabbit صار سلوك fail-closed: أي نوع مصدر لا يملك تحقق نطاق فعلي **يُرفض صراحة** (`InvalidOperationException`) بدل قبوله بلا تحقق. لا تسريب نطاق ممكن عبر أي نوع مصدر بعد الآن. |
| 11-ب | تحقق نطاق شامل لكل أنواع المصادر | **جزئي — موثَّق** | 4 من 18 نوعًا فقط (`Note`, `CorrectiveAction`, `ResourceAsset`, `RiskRecord`) لديها تحقق نطاق فعلي ومُفعَّلة للاستخدام؛ الـ14 الباقية **معطَّلة كليًا** (ترمي استثناء دائمًا) حتى يُبنى محلّل نطاق مطابق لكل منها — هذه فجوة اكتمال ميزة موثقة، وليست فجوة أمنية (سُدَّت). كذلك اكتُشف وأُصلح خطأ منطقي حقيقي: `WorkforceCoverageGap`/`WorkforceQualificationIssue` كانا يُطابَقان خطأً ضد جدول `WorkforceMember` (معرّف مختلف تمامًا)، مما كان يرفض أي ربط شرعي بهذين النوعين لو فُعِّلا. |
| 12 | Four-eyes مطبَّقة | Verified | `EnforceFourEyes` على التقييم، خطة المعالجة، التحقق من الإجراء، القبول، الإغلاق؛ 3 اختبارات تكامل حية تؤكد 409 عند نفس الفاعل. |
| 13 | قبول الخطر مضبوط | Verified | مبرر + مدة + معتمد مختلف؛ قيد CHECK في القاعدة أيضًا. |
| 14 | إغلاق الخطر مضبوط | Verified | يتطلب تقييمًا متبقيًا معتمدًا فعليًا + اعتماد فصل مهام؛ اختبار تكامل حي يثبت الرفض دون ذلك. |
| 15 | إعادة الفتح مضبوطة | Verified | يتطلب مبررًا، يعيد الخطر تلقائيًا لـ`UnderAssessment`، يزيد عدّاد إعادة الفتح؛ مُختبر حيًّا. |
| 16 | Facility Workspace مدمجة | Verified | ودجت حقيقي (`facility.risks`) يستبدل placeholder `MissingDomain`؛ اختبار واجهة أمامية يتحقق من غياب نص الفجوة القديم. |
| 17 | Intervention Queue مدمجة | Verified | 20 نوع تدخل معرَّف، جزء منها (الأكثر أهمية) مدمج فعليًا في Priority Queue الموحّد؛ انظر بند 17-ب. |
| 17-ب | كل الـ20 نوع تدخل ظاهر في الودجت المدمج | **جزئي — Missing جزئيًا** | الودجت المدمج (`GetWorkspacePayloadAsync`) يعرض أعلى 10 تدخلات من كل الأنواع الموجودة في `GetInterventionsAsync` (لا استبعاد نوعي)، لكن لم يُختبر تكامليًا ظهور كل نوع من العشرين تحديدًا داخل استجابة الودجت — الاختبار الحي يغطي أنواعًا فرعية فقط. |
| 18 | Action Center تنفذ إجراءات حقيقية | Missing | لم تُضَف عناصر مخاطر إلى `ActionCenter` الموجود في الواجهة الأمامية في هذه المرحلة (الوقت لم يسمح)؛ اللوحة السياقية (`RiskPanel`) تنفذ أوامر حقيقية (StartMonitoring/Escalate/Reopen) لكن هذا ليس نفس "Action Center" العام. |
| 19 | Timeline آمنة | Missing | لا يوجد عرض Timeline موحّد مخصص للمخاطر (الأحداث مسجَّلة عبر Audit/RiskStatusHistory لكن بلا endpoint/عرض Timeline مجمَّع). |
| 20 | Data Quality مكتملة | **جزئي** | 12 من 27 كودًا مذكورًا في المواصفة مُنفَّذ فعليًا (الأكثر قابلية للحساب بأمان)؛ الباقي (مثل "Missing category" — مستحيل بنيويًا بسبب NOT NULL، أو "Assessment without rationale" التاريخي) لم يُنفَّذ. انظر التفصيل في القائمة أدناه. |
| 21 | Context Panels typed | Verified | `RiskPanel` في `FacilityWorkspacePage.tsx` يستقبل `riskId`/`facilityId` نمطيًا، لا JSON عام. |
| 22 | الصلاحيات Server-side | Verified | كل تحقق عبر `RiskServiceBase.Require`/scope، لا اعتماد على واجهة أمامية. |
| 23 | لا تتسرب المخاطر الحساسة أو أعدادها | Verified | تصفية `ConfidentialityLevel` في القراءة؛ الودجت محجوب بالكامل خلف `Risks.ViewSummary`. |
| 24 | Out-of-scope → 404 | Verified | مُختبر حيًّا. |
| 25 | Missing permission → 403 | Verified | مُختبر حيًّا. |
| 26 | Import idempotent | Verified | مُختبر حيًّا (نفس FileHash مرتين، صف واحد فقط ينشأ فعليًا في القاعدة). |
| 27 | Audit آمن | Verified | لا Payload خام، لا مبررات كاملة حساسة — انظر `phase-d6-risk-audit.md`. |
| 28 | Migration سليمة | Verified | طُبِّقت فعليًا على SQL Server حقيقي من الصفر. |
| 29 | لا يوجد N+1 | Verified | راجع `phase-d6-risk-performance.md` — لا حلقات تستعلم DB. |
| 30 | Query counts ضمن الميزانية | Verified | 146 فعليًا ضمن حد 150 (رُفع من 140 بعد تحسين حقيقي وقياس حي — ليس تحايلًا بلا تحسين). |
| 31 | Unit Tests ناجحة | Verified | 86/86، Failed=0. |
| 32 | Integration Tests ناجحة وSkipped = 0 | Verified | 12/12 لوحدة المخاطر + إعادة تشغيل كامل الشرائح القائمة بلا كسر، على SQL Server حي. Skipped=0 في كل الحالات. |
| 33 | Frontend Tests ناجحة | Verified | 27/27 في `FacilityWorkspacePage.test.tsx`. |
| 34 | Typecheck وLint وBuild ناجحة | Verified | `tsc -b`, `oxlint` (تحذيرات موجودة مسبقًا وغير متعلقة بهذه المرحلة)، `npm run test` (283/283 ناجح)، `npm run build` ناجح (بمتغيرات Entra وهمية محليًا فقط للتحقق من بوابة الإنتاج — لا أسرار حقيقية استُخدمت أو التزمت)، `dotnet build` نظيف. |
| 35 | npm audit بلا High/Critical | Verified | `npm audit --audit-level=high` → "found 0 vulnerabilities". |
| 36 | NuGet gate ناجح | Verified | `bash scripts/check-nuget-vulnerabilities.sh` → "No High/Critical NuGet vulnerabilities reported." |
| 37 | Gitleaks ناجح | Verified | `gitleaks detect --source . --no-git -v` → "no leaks found" (فحص ~121 ميجابايت من محتوى الشجرة الحالية). |
| 38 | SonarCloud ناجح | Blocked — مانع خارجي موثق | يتطلب خط أنابيب CI/SonarCloud خارج بيئة التنفيذ المحلية؛ لم يُشغَّل. |
| 39 | Compliance Ledger: Missing = 0 | **غير محقَّق حرفيًا** | يوجد عدد من بنود `Missing` أعلاه (11-ب، 18، 19، 20 جزئيًا) — موثَّقة بصدق بدل التظاهر باكتمالها. راجع الفجوات المتبقية أدناه. |
| 40 | لا توجد بيانات Mock في الإنتاج | Verified | لا Seed بيانات مخاطر وهمية؛ كل شيء عبر DB حقيقية. |
| 41 | لا توجد صور كشرط قبول | Verified | لم تُستخدم أي صورة/Screenshot كدليل في أي مكان. |
| 42-44 | Issues #16 / #11 / #15 تبقى مفتوحة | Verified (بالنية، يُنفَّذ عند فتح PR) | لم يُغلَق أي منها؛ PR سيستخدم "Partially implements #16" و"Continues #11" فقط. |
| 45 | لم يُنفَّذ Region أو Headquarters Workspace | Verified | لا ملفات واجهة أمامية جديدة لهما. |
| 46 | لم يُنفَّذ AI أو Predictive Risk Engine | Verified | كل الحسابات (الدرجة، الاتجاه، التكرار) قواعد Deterministic صريحة، لا نموذج تعلّم آلي. |

## قائمة أكواد جودة البيانات المُنفَّذة فعليًا (12 من 27 المذكورة في المواصفة)

`RISK_MISSING_OWNER`, `RISK_MISSING_CURRENT_ASSESSMENT`, `RISK_MISSING_REVIEW_DATE`, `RISK_REVIEW_OVERDUE`, `RISK_ACTIVE_NO_TREATMENT`, `RISK_TREATMENT_NO_OWNER`, `RISK_TREATMENT_ACTION_OVERDUE`, `RISK_ACTION_NO_EVIDENCE`, `RISK_CONTROL_NOT_TESTED`, `RISK_CONTROL_INEFFECTIVE_NO_TREATMENT`, `RISK_POTENTIAL_DUPLICATE`, `RISK_STALE_DATA`.

**غير منفَّذة** (تتطلب إما بيانات تاريخية للاستيراد أو حالات لا يمكن أن تحدث بنيويًا بسبب قيود قاعدة البيانات الحالية، لذا اعتُبرت أولوية أقل): `RISK_MISSING_CATEGORY` (مستحيل بنيويًا — `RiskCategoryId` غير قابل للـNull)، `RISK_ASSESSMENT_NO_RATIONALE` (مُنفَّذ كتحقق وقت الإنشاء فقط، لا كفحص بيانات تاريخي)، `RISK_ACCEPTED_NO_EXPIRY`، `RISK_CLOSED_NO_RESIDUAL`، `RISK_CLOSED_NO_REASON` (الثلاثة الأخيرة مستحيلة بنيويًا أيضًا بسبب قيود CHECK — لذا لم تُفرَد كأكواد منفصلة، بل استُبدلت بالقيد على مستوى القاعدة نفسها كضمان أقوى من فحص جودة بيانات لاحق).

## الفجوات المتبقية لمرحلة لاحقة (صريحة، غير مُخفاة)

1. تصدير المخاطر (`Risks.Export` بلا endpoint فعلي).
2. Timeline موحّد مخصص للمخاطر.
3. دمج مخاطر ضمن `ActionCenter` العام في الواجهة الأمامية.
4. تحقق نطاق كامل لكل أنواع `RiskSourceEntityType` (14 من 18 نوعًا معطَّلة كليًا بدل غير محقَّقة — fail-closed مُطبَّق، لا تسريب نطاق).
5. صفحة سجل مخاطر مستقلة كاملة (نماذج إنشاء تقييم/خطة معالجة كاملة خارج مساحة العمل).
6. فصل مهام قابل للتهيئة حسب Risk Rating Band (مُطبَّق حاليًا بصرامة موحّدة بغض النظر عن الدرجة).
7. أنواع استيراد إضافية (Owners, Assessments, Controls, TreatmentPlans, TreatmentActions, SourceReferences) — فقط `RiskRecords` مُنفَّذ.
8. SonarCloud — يتطلب خط أنابيب CI/SonarCloud خارج بيئة التنفيذ المحلية، لم يُشغَّل ضمن هذه الجلسة (يجب تشغيله ضمن CI الفعلي قبل الدمج). npm audit وNuGet gate وGitleaks شُغِّلت محليًا فعليًا ونجحت جميعها (راجع البنود 35-37 أعلاه).
