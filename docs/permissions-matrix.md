# مصفوفة الصلاحيات والأدوار

## نموذج التحكم

| الطبقة | الوصف |
|--------|--------|
| RBAC | أدوار تمنح صلاحيات Action-Level |
| Scope | نطاق تنظيمي (وطني / منطقة / سجن / وحدة / متعدد) |
| Classification | درجة سرية السجل عند العرض/التصدير |
| SoD | فصل واجبات على العمليات الحساسة |

## الأدوار الأولية

| الرمز | الاسم |
|-------|--------|
| SystemAdministrator | مسؤول النظام |
| HeadquartersExecutive | تنفيذي المستوى الرئيسي |
| DecisionSupportDirector | مدير دعم القرار |
| DecisionAnalyst | محلل قرارات |
| RegionalDirector | مدير منطقة |
| RegionalCoordinator | منسق منطقة |
| FacilityDirector | مدير سجن |
| FacilityCoordinator | منسق سجن |
| SecurityOfficer | ضابط أمن |
| ArmamentOfficer | ضابط تسليح |
| FleetOfficer | ضابط أسطول |
| WorkforceOfficer | ضابط قوى عاملة |
| IncidentOfficer | ضابط وقائع |
| PrisonerCaseOfficer | ضابط حالات نزلاء |
| ProjectManager | مدير مشاريع |
| StrategyOfficer | ضابط استراتيجية |
| FormDesigner | مصمم نماذج |
| FormReviewer | مراجع نماذج |
| Auditor | مدقق |
| ReadOnlyUser | مستخدم قراءة فقط |

## صلاحيات المرحلة A (مفعّلة)

| الصلاحية | SystemAdmin | Auditor | باقي الأدوار (افتراضي) |
|----------|:-----------:|:-------:|:----------------------:|
| Organization.View | ✓ | ✓ | حسب النطاق إن مُنحت |
| Organization.Manage | ✓ | | |
| Users.View | ✓ | ✓ | |
| Users.Manage | ✓ | | |
| Roles.Manage | ✓ | | |
| Scopes.Manage | ✓ | | |
| Audit.View | ✓ | ✓ | |
| Attachments.Upload | ✓ | | حسب المنح |
| Attachments.Download | ✓ | ✓ | حسب المنح |
| Attachments.DownloadSensitive | ✓ | ✓ | صريح فقط |

## صلاحيات الملاحظات التشغيلية (مفعّلة في B.1)

النطاق المدعوم لـ `OperationalNote` في B.1: `Global`, `Headquarters`, `Region`, `Facility`, `FacilityUnit` فقط
(لا `MultipleRegions`/`MultipleFacilities`). أي طلب خارج نطاق المستخدم أو لكيان غير موجود يُعامَل كـ `404 Not Found`
(منع التعداد)، بدلاً من `403 Forbidden`.

| الصلاحية | الوصف | SystemAdmin | HQ Executive | Decision Support Director | Regional Director | Regional Coordinator | Facility Director | Facility Coordinator |
|----------|-------|:-----------:|:------------:|:--------------------------:|:------------------:|:---------------------:|:-------------------:|:----------------------:|
| Notes.View | عرض الملاحظات ضمن النطاق | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Notes.ViewSensitive | عرض محتوى Confidential/Secret دون حجب | ✓ | ✓ | | | | | |
| Notes.Create | إنشاء مسودة ملاحظة | ✓ | | ✓ | ✓ | ✓ | ✓ | ✓ |
| Notes.Update | تحديث/تقديم مسودة (Draft to Open) | ✓ | | ✓ | ✓ | ✓ | ✓ | ✓ |
| Notes.Assign | تكليف/إعادة تكليف | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | |
| Notes.StartWork | بدء المعالجة (Assigned/Reopened to InProgress) | ✓ | | | | ✓ | | ✓ |
| Notes.SubmitForVerification | إرسال للتحقق (InProgress to PendingVerification) | ✓ | | | | ✓ | | ✓ |
| Notes.ReturnForRework | إعادة للمعالجة (PendingVerification to InProgress) | ✓ | | ✓ | ✓ | | ✓ | |
| Notes.VerifyClosure | اعتماد الإغلاق (PendingVerification to Closed) | ✓ | ✓ | ✓ | ✓ | | ✓ | |
| Notes.Reopen | إعادة فتح ملاحظة مغلقة | ✓ | ✓ | ✓ | ✓ | | ✓ | |
| Notes.Cancel | إلغاء (Draft/Open to Cancelled) | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Notes.Archive | أرشفة (حذف ناعم) | ✓ | ✓ | | ✓ | | ✓ | |
| Notes.Restore | استعادة من الأرشفة | ✓ | ✓ | | ✓ | | ✓ | |

`Auditor` و `ReadOnlyUser` يحصلان فقط على `Notes.View`.

### فصل الواجبات على الملاحظات الحرجة (Critical SoD)

لملاحظة بمستوى خطورة Critical تحديدًا: مَن قام بالمعالجة الفعلية (آخر مَن نفّذ start-work أو
submit-for-verification، محفوظ في LastProcessedByUserId) لا يمكنه هو نفسه تنفيذ verify-closure
لنفس الملاحظة، حتى لو كان يحمل صلاحية Notes.VerifyClosure. **SystemAdministrator لا يُستثنى من هذا الفصل.**
الفحص منفصل تمامًا عن التحقق من الصلاحية (Notes.VerifyClosure مطلوبة أولاً، ثم فحص SoD).

### مصفوفة الحالات (State Machine)

- Draft to Open (submit) / Draft to Cancelled (cancel)
- Open to Assigned (assign) / Open to Cancelled (cancel)
- Assigned to InProgress (start-work) / Assigned to Assigned (reassign)
- InProgress to PendingVerification (submit-for-verification)
- PendingVerification to Closed (verify-closure) / PendingVerification to InProgress (return-for-rework)
- Closed to Reopened (reopen)
- Reopened to Assigned (assign) / Reopened to InProgress (start-work)

أي انتقال خارج هذه القائمة يُرفض بـ 409 Conflict. الأسباب (Reason) مطلوبة إلزاميًا لعمليات:
cancel, assign (تكليف وإعادة تكليف), return-for-rework, verify-closure, reopen.

## صلاحيات مسجّلة للوحدات اللاحقة (Seed فقط في A)

`Vehicles.*`, `Armament.*`, `Incidents.*`, `Forms.*`, `Projects.*`, `Strategy.*`, `Reports.ExportSensitive`, `Plans.*`, `Workforce.*`, `Decisions.*`, `PrisonerFollowUp.*`

## قواعد فصل الواجبات (SoD)

| العملية | القاعدة |
|---------|---------|
| حركة تسليح | المنشئ ≠ المعتمد لنفس الحركة |
| ملاحظة حرجة | المعالج ≠ المعتمد النهائي للإغلاق منفردًا (مُفعّلة ومُختبرة في B.1) |
| واقعة جسيمة | مدخل التقرير ≠ المعتمد النهائي |
| تصدير حساس | يتطلب `Reports.ExportSensitive` أو `Attachments.DownloadSensitive` + تسجيل تدقيق |

تُنفَّذ قواعد SoD في طبقة Application عند تفعيل الوحدات المعنية؛ في المرحلة A تُوثَّق وتُختبر بنية الصلاحيات والنطاق.
