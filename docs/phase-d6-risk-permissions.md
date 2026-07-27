# Phase D.6 — الصلاحيات

## قائمة الصلاحيات (`PermissionCodes` في `Baseera.Domain.Identity.IdentityEntities`)

| الصلاحية | الاستخدام |
| --- | --- |
| `Risks.ViewSummary` | رؤية الملخص والودجت في مساحة العمل دون تفاصيل الخطر. |
| `Risks.View` | رؤية سجل المخاطر وتفاصيله. |
| `Risks.ViewSensitive` | رؤية المخاطر ذات `ConfidentialityLevel` أعلى من `Internal`. |
| `Risks.Create` / `Risks.Update` | تسجيل/تحديث بيانات الخطر الأساسية. |
| `Risks.AssignOwner` | تعيين مالك الخطر. |
| `Risks.ManageCategories` | إدارة تصنيفات المخاطر. |
| `Risks.Assess` | إنشاء/إرسال تقييم. |
| `Risks.ReviewAssessment` / `Risks.ApproveAssessment` | مراجعة/اعتماد التقييم (منفصلتان). |
| `Risks.ManageControls` | إدارة الضوابط الحالية. |
| `Risks.ManageTreatments` | إنشاء/تعديل خطط وإجراءات المعالجة، **وأيضًا اعتماد الخطة** (الفصل الفعلي عبر فصل المهام لا الصلاحية — انظر أدناه). |
| `Risks.CompleteTreatmentActions` | تنفيذ/تقديم إجراء معالجة للتحقق. |
| `Risks.VerifyTreatmentActions` | التحقق من اكتمال إجراء المعالجة. |
| `Risks.RequestAcceptance` / `Risks.ApproveAcceptance` | طلب/اعتماد قبول الخطر. |
| `Risks.RequestClosure` / `Risks.ApproveClosure` | طلب/اعتماد إغلاق الخطر. |
| `Risks.Reopen` | إعادة فتح خطر مغلق. |
| `Risks.Escalate` | تصعيد الخطر (**صلاحية أُضيفت** خارج القائمة الحرفية للمواصفة لأن "تصعيد الخطر" مذكور كقدرة مطلوبة في الهدف دون صلاحية مقابلة في القائمة المقترحة — إضافة ضرورية موثقة). |
| `Risks.LinkSources` | ربط/فك ربط مصادر وأدلة. |
| `Risks.Export` | تصدير بيانات المخاطر (الصلاحية مُسندة، لا يوجد endpoint تصدير فعلي بعد — انظر Compliance Ledger). |
| `Risks.Import` | الاستيراد المنضبط والمصالحة. |
| `Risks.ManageMatrices` / `Risks.ApproveMatrices` | إدارة/اعتماد مصفوفات التقييم (منفصلتان تمامًا عن صلاحيات إدارة الخطر). |

## قرار: فصل المهام بدل صلاحية منفصلة للاعتماد

القائمة الأصلية المقترحة في المواصفة **لا تتضمن** صلاحية منفصلة لـ"اعتماد خطة المعالجة" أو "التحقق من إجراء معالجة تنفذه أنت". طُبِّق المبدأ التالي حرفيًا بدل اختراع صلاحيات غير مطلوبة:

* اعتماد خطة المعالجة يستخدم **نفس** `Risks.ManageTreatments`، والفصل الفعلي (منشئ الخطة ≠ معتمدها) يتحقق عبر فصل المهام وقت التنفيذ (`EnforceFourEyes`).
* التحقق من إجراء معالجة يستخدم صلاحية مستقلة (`Risks.VerifyTreatmentActions`) **مسندة فقط لدور المعتمد** (`FacilityDirector`)، وليس لدور المنفّذ (`RiskOfficer`) — هنا الفصل صلاحية + فصل مهام معًا، لأن "التحقق" فعل مختلف جوهريًا عن "التنفيذ".

## الأدوار (بذور `DatabaseInitializer.cs`)

* **دور جديد**: `RiskOfficer` (ضابط مخاطر) — حزمة إدارة كاملة على مستوى السجن (إنشاء، تقييم، ضوابط، معالجة، طلب قبول/إغلاق، إعادة فتح، تصعيد، ربط مصادر، استيراد، إدارة مصفوفات).
* **موسَّعة**: `FacilityDirector` (مدير سجن) — حزمة اعتماد كاملة (مراجعة/اعتماد تقييم، اعتماد خطط المعالجة عبر فصل المهام، التحقق من الإجراءات، اعتماد القبول/الإغلاق/المصفوفات، التصدير).
* **ملخص فقط**: `Auditor`, `HeadquartersExecutive`, `RegionalDirector` — يرون العدادات الإجمالية دون تفاصيل حساسة (`Risks.ViewSummary` فقط)، اتساقًا مع مبدأ عدم تسرب الأعداد لغير المصرح بالتفاصيل مع السماح بمؤشر عام للمستويات الإشرافية.
* **كامل التفاصيل بلا اعتماد**: `DecisionSupportDirector` — يرى ويشارك في الاعتماد (حزمة `riskViewer` + `riskApprover`) بما يماثل معاملته لوحدة العهد الحساسة.

## القواعد المطبَّقة فعليًا (وليست وثائق فقط)

* `Workspaces.ViewFacility` وحدها لا تُظهر قسم المخاطر — يتطلب `Risks.ViewSummary` تحديدًا (مُتحقَّق في `FacilityWorkspaceReadService.GetMetricsAsync`/`GetDataQualityAsync`/`GetPriorityQueueAsync`، وكل واحدة منها تتحقق بشكل مستقل قبل استدعاء ودجت المخاطر).
* `Risks.View` لا تعني `Risks.ViewSensitive` — `RiskRegisterQueryService.ListAsync`/`GetAsync` يُصفّيان/يرفضان المخاطر ذات التصنيف الحساس دون الصلاحية الإضافية.
* خارج النطاق (Facility أخرى) → 404 دائمًا (`KeyNotFoundException`)، صلاحية مفقودة → 403 دائمًا (`UnauthorizedAccessException`) — هذا نمط `Middleware.cs` العام المطبَّق على كل endpoints المخاطر دون استثناء.
