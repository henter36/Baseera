# Phase D.6 — روابط المصادر والأدلة (RiskSourceLink)

## نمط مُنضبط، لا JSON عام

`RiskSourceLink` يحمل `SourceEntityType` (enum مغلق `RiskSourceEntityType`) + `SourceEntityId` (Guid) + `RelationshipType` (enum مغلق `RiskSourceRelationshipType`: IdentifiedFrom, Evidence, ContributingFactor, Consequence, Control, TreatmentDependency, Trigger, Related). لا يوجد أي حقل JSON حر أو نوع كيان نصي غير مقيَّد.

## عدم الحذف التاريخي

الإزالة (`RemoveAsync`) هي دائمًا Soft-delete (`IsDeleted=true` + `RemovalReason` إلزامي)، أبدًا حذفًا فعليًا. سجل الروابط التاريخي يبقى قابلًا للاستعلام دائمًا (`IgnoreQueryFilters` عند الحاجة لتدقيق لاحق).

## التحقق من النطاق — الحالة الفعلية

الجدول التالي يعكس **ما هو منفَّذ فعليًا اليوم**، وليس افتراضًا:

| نوع المصدر | التحقق من النطاق | السبب |
| --- | --- | --- |
| `Note` | ✅ عبر `OperationalNote.FacilityId` | الكيان موجود ويحمل نطاقًا مباشرًا. |
| `CorrectiveAction` | ✅ عبر ربط بـ`OperationalNote.FacilityId` (الإجراء يرث نطاقه من الملاحظة) | يطابق قرار الكود الحالي بعدم تخزين نطاق مستقل على `CorrectiveAction`. |
| `ResourceAsset` | ✅ عبر `ResourceAsset.OperationalFacilityId` | |
| `WorkforceCoverageGap` / `WorkforceQualificationIssue` | ✅ عبر `WorkforceMember.CurrentOperationalFacilityId`/`HomeFacilityId` | لا يوجد كيان "فجوة تغطية" مستقل — الربط يستهدف عضو القوى البشرية نفسه. |
| `RiskRecord` | ✅ عبر `RiskRecord.FacilityId` | لربط مخاطر ببعضها (مثل "سبَّب هذا الخطر ذاك"). |
| `Escalation`, `Occurrence`, `OccupancyWarning`, `ResourceGap`, `MaintenanceWorkOrder`, `SensitiveCustodyDiscrepancy`, `Project`, `EmergencyPlan`, `FormResponse`, `DataQualityIssue`, `Decision`, `Other` | ❌ **غير محقَّق** | لا يوجد كيان Domain مستقل لبعضها في الكود الحالي أصلًا (Project/EmergencyPlan/Decision/Occurrence خارج النطاق حسب توجيه المرحلة)، أو لم يُبنَ التحقق لبعضها الآخر (Escalation, MaintenanceWorkOrder, FormResponse) لضيق الوقت. الربط بهذه الأنواع **مسموح دون رفض** حاليًا — وهذا مذكور صراحة كفجوة أمنية محتملة يجب معالجتها قبل تفعيل هذه الأنواع في الإنتاج. |

## القاعدة العملية

`RiskSourceLinkService.EnsureSourceInScopeAsync` تتحقق فقط للأنواع المذكورة أعلاه بعلامة ✅ (مجموعة `ScopeCheckedTypes`)، وتُرجع `true` (تسمح) لأي نوع آخر دون تحقق. هذا يعني أن **مبدأ "لا تسمح بربط كيان خارج Facility scope" مُطبَّق جزئيًا فقط** في هذه المرحلة، وليس بالكامل. أي توسعة مستقبلية لأنواع مصادر جديدة يجب أن تضيف تحققًا مطابقًا قبل اعتبارها آمنة.
