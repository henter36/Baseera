# Phase D.6 — نطاق المرحلة: سجل المخاطر المؤسسي ومركز معالجة مخاطر السجن

يرتبط هذا التنفيذ بـ:

* Issue #16 — Enterprise Risk Center (تنفيذ جزئي — Facility scope فقط).
* Issue #11 — Facility Workspace – Prison Decision Center (استمرار).
* Issue #18 — Unified Operational Timeline، في حدود أحداث المخاطر فقط (عبر التدقيق/Audit وسجل حالة الخطر append-only؛ لا يوجد Timeline widget موحّد مخصص جديد في هذه المرحلة — انظر الفجوات).
* Issue #19 — Alert and Incident Center، في حدود الربط بمصادر المخاطر فقط (`RiskSourceLink`).

## داخل النطاق (منفَّذ فعليًا)

* Domain مؤسسي (`Baseera.Domain.RiskManagement`) قابل للاستخدام مستقبلًا في Region/Headquarters دون إعادة بناء (حقول `RegionId`/`HeadquartersOrganizationId`/`ScopeLevel` موجودة، غير مُستخدمة إلا لـ Facility في هذه المرحلة).
* Facility scope منفَّذ فعليًا عبر جميع نقاط النهاية (`/api/v1/facilities/{facilityId}/risks/...`).
* سجل المخاطر، تصنيفات المخاطر (هرمية بسيطة).
* مصفوفات الاحتمالية والأثر، مُصدرة (Versioned)، بدورة حياة Draft → PendingApproval → Active → Retired.
* التقييم الأولي (Inherent) والمتبقي (Residual) والحالي (Current/PostIncident/PeriodicReview) — منفصلة بنيويًا.
* الضوابط الحالية (`RiskControl`) منفصلة عن خطط المعالجة.
* خطط المعالجة وإجراءاتها، مع اعتمادية DAG-by-construction ومتطلبات دليل الإكمال.
* المراجعات والاعتمادات (`RiskReview`) لقبول الخطر وإغلاقه، بفصل مهام (four-eyes).
* القبول والتصعيد والإغلاق وإعادة الفتح، عبر أوامر ومراجعات محددة.
* الروابط النمطية (Typed) بالأدلة/المصادر مع تحقق نطاق للأنواع المتوفرة فعليًا في الكود الحالي.
* Facility Workspace: ودجت مخاطر حقيقي (ملخص + تدخلات)، تكامل مع Priority Queue وData Quality.
* الاستيراد المنضبط (Preview/Confirm) بمفاتيح Idempotency، والمصالحة الأساسية للتكرارات.
* التدقيق (Audit) لكل تحول حساس.
* الاختبارات: 86 اختبار وحدة للمنطق الصرف، 12 اختبار تكامل حي على SQL Server حقيقي، 4 اختبارات واجهة أمامية إضافية.

## خارج النطاق (لم يُنفَّذ، بحسب توجيه المرحلة)

* Region Workspace UI وHeadquarters Workspace UI.
* تجميع المخاطر عبر المناطق داخل واجهة مستقلة.
* AI risk scoring أو Predictive Risk Engine أو Monte Carlo أو التحسين الرياضي.
* التقييم المالي المتقدم، التأمين والعقود، إدارة الامتثال المؤسسية الكاملة.
* إدارة الحوادث كـ Domain مستقل (لا يوجد Incident/Occurrence entity في الكود الحالي أصلًا).
* الصور أو Screenshots كشرط قبول — لم تُستخدم في أي مكان.

## فجوات موثقة (انظر Compliance Ledger للتفصيل الكامل)

* تصدير المخاطر (`Risks.Export`) — الصلاحية موجودة ومُسندة، لكن لا يوجد endpoint فعلي لتوليد ملف تصدير في هذه المرحلة.
* Timeline widget موحّد مخصص للمخاطر (عرض زمني تراكمي عبر جميع الأحداث) — الأحداث نفسها مسجَّلة (Audit + RiskStatusHistory) لكن لا يوجد عرض API/Frontend مخصص يجمعها في Timeline واحد بعد.
* التحقق من النطاق (Scope) عند الربط بمصادر من نطاقات غير مُطوَّرة بعد في الكود (Project، EmergencyPlan، Decision، Occurrence، DataQualityIssue، SensitiveCustodyDiscrepancy) — الربط مسموح لكن التحقق من مطابقة نطاق السجن غير منفَّذ لهذه الأنواع تحديدًا (موثق في `phase-d6-risk-source-linking.md`).
* صفحة سجل مخاطر مستقلة كاملة (إنشاء تقييم/خطة معالجة عبر نموذج واجهة كامل) — منفَّذة فقط جزئيًا داخل مساحة عمل السجن (قسم وقسم سياق للقراءة + أوامر محدودة)؛ لا توجد صفحة `/risks` مستقلة مخصصة بكل الاستمارات.
