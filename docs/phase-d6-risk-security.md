# Phase D.6 — الأمن والنطاق

## فرض النطاق من الخادم دائمًا

كل خدمة تطبيقية ترث `RiskServiceBase` وتستدعي `EnsureFacilityVisibleAsync`/`EnsureRiskVisibleAsync` قبل أي عملية قراءة أو كتابة، بنفس نمط `SensitiveCustodyServices`:

1. صلاحية مفقودة → `UnauthorizedAccessException` → **403**.
2. منشأة/خطر خارج نطاق المستخدم → `KeyNotFoundException` → **404** (يُخفي الوجود، لا يكشف أن السجل موجود في منشأة أخرى).

لا يوجد أي فحص نطاق على مستوى الواجهة الأمامية فقط — كل الفحوصات مكرَّرة/أصلية على الخادم.

## عدم تسرب المخاطر الحساسة

* `RiskRecord.ConfidentialityLevel` (بإعادة استخدام `Attachments.ClassificationLevel` الموجود، بلا enum مواز) يُصفّى في `ListAsync`/`GetAsync` دون `Risks.ViewSensitive`.
* Data Quality وIntervention Queue وWorkspace Summary تعرض **عدادات وأنماطًا** فقط، لا عناوين مخاطر فردية لمستخدم يفتقر لـ`Risks.View`/`Risks.ViewSummary` (الودجت نفسه محجوب بالكامل خلف فحص الصلاحية في `FacilityWorkspaceReadService`).

## التدقيق الآمن

راجع `phase-d6-risk-audit.md`. القاعدة العامة: لا تُسجَّل نصوص المبررات الكاملة أو الأدلة الحساسة في `AuditLog`، فقط معرّفات وأنواع وأسماء حقول متغيّرة (مثل نوع الأمر، الحالة السابقة/التالية، رمز نطاق التصنيف).

## لا بيانات وهمية (Mock) في مسار الإنتاج

جميع نقاط النهاية تقرأ/تكتب من قاعدة بيانات حقيقية عبر `IBaseeraDbContext`. اختُبر التطبيق فعليًا (وليس افتراضًا) ضد SQL Server حقيقي (حاوية Docker محلية تحاكي بيئة CI) — راجع `phase-d6-risk-test-matrix.md`. لا توجد بيانات Seed وهمية للمخاطر في `DatabaseInitializer.cs` — فقط الصلاحيات والأدوار والحزم.

## التواريخ

كل الطوابع الزمنية في النموذج من نوع `DateTimeOffset` وتُخزَّن UTC (بلا استثناء) عبر EF Core. عرضها بتوقيت `Asia/Riyadh` مسؤولية الواجهة الأمامية (`Intl.DateTimeFormat` بـ `timeZone: 'Asia/Riyadh'`)، بنفس النمط المستخدم في بقية صفحات مساحة العمل — لم تُضَف منطقة زمنية جديدة أو منطق تحويل مواز.

## فجوة أمنية موثقة

كما ذُكر في `phase-d6-risk-source-linking.md`، التحقق من نطاق المصدر عند الربط **غير مكتمل** لعدة أنواع كيانات (Escalation، MaintenanceWorkOrder، FormResponse، وغيرها) — هذه نقطة يجب معالجتها قبل الاعتماد الكامل على الربط النمطي كضمان أمني شامل.
