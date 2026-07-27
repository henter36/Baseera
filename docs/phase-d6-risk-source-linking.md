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
| `RiskRecord` | ✅ عبر `RiskRecord.FacilityId` | لربط مخاطر ببعضها (مثل "سبَّب هذا الخطر ذاك"). |
| كل نوع آخر (`Escalation`, `Occurrence`, `OccupancyWarning`, `ResourceGap`, `MaintenanceWorkOrder`, `WorkforceCoverageGap`, `WorkforceQualificationIssue`, `SensitiveCustodyDiscrepancy`, `Project`, `EmergencyPlan`, `FormResponse`, `DataQualityIssue`, `Decision`, `Other`) | ❌ **مرفوض صراحة** (`InvalidOperationException`) | لا يوجد محلّل نطاق مبني لهذه الأنواع بعد؛ **fail-closed** — يُرفض الربط حتى يُبنى تحقق مطابق، بدل قبوله دون تحقق. |

> ملاحظة تصحيحية: نسخة سابقة من هذا الجدول ذكرت أن `WorkforceCoverageGap`/`WorkforceQualificationIssue` محقَّقان عبر `WorkforceMember`. هذا كان خطأ فعليًا في الكود — معرّف "فجوة تغطية" أو "مشكلة تأهيل" **ليس** معرّف عضو قوى بشرية، فكانت المطابقة ضد الجدول الخطأ ترفض أي ربط شرعي بهذين النوعين. عولج بإزالتهما من المجموعة المحقَّقة وتطبيق الرفض الصريح عليهما مع بقية الأنواع غير المدعومة.

## القاعدة العملية

`RiskSourceLinkService.EnsureSourceInScopeAsync` تتحقق فقط للأنواع الأربعة المذكورة أعلاه بعلامة ✅ (مجموعة `ScopeCheckedTypes`)، وترمي استثناءً لأي نوع آخر بدل قبوله. هذا يعني أن **مبدأ "لا تسمح بربط كيان خارج Facility scope" مُطبَّق بالكامل الآن** لكل الأنواع المدعومة فعليًا اليوم؛ الأنواع الأخرى غير مرفوضة أمنيًا فحسب بل غير قابلة للاستخدام إطلاقًا حتى تُبنى مجالاتها ويُضاف محلّل نطاق مطابق لكل منها.
