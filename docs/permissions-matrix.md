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

## صلاحيات الإجراءات التصحيحية (مفعّلة في B.2.1)

النطاق مشتق من `OperationalNote` الأصلية. السجل غير الموجود أو خارج النطاق يعود `404 Not Found` لمنع التعداد، ونقص الصلاحية داخل النطاق يعود `403 Forbidden`.

| الصلاحية | الوصف | SystemAdmin | HQ Executive | Decision Support Director | Regional Director | Regional Coordinator | Facility Director | Facility Coordinator | Auditor | ReadOnlyUser |
|----------|-------|:-----------:|:------------:|:--------------------------:|:------------------:|:---------------------:|:-------------------:|:----------------------:|:-------:|:------------:|
| CorrectiveActions.View | عرض الإجراءات ضمن النطاق | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| CorrectiveActions.ViewSensitive | عرض محتوى حساس دون حجب | ✓ | ✓ | صريح | | | | | صريح | صريح |
| CorrectiveActions.Create | إنشاء إجراء مرتبط بملاحظة | ✓ | | ✓ | ✓ | ✓ | ✓ | ✓ | | |
| CorrectiveActions.Update | تحديث الحقول القابلة للتحرير | ✓ | | ✓ | ✓ | ✓ | ✓ | ✓ | | |
| CorrectiveActions.Assign | تكليف/إعادة تكليف | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | | | |
| CorrectiveActions.StartWork | بدء المعالجة | ✓ | | | | ✓ | | ✓ | | |
| CorrectiveActions.SubmitForVerification | إرسال للتحقق | ✓ | | | | ✓ | | ✓ | | |
| CorrectiveActions.VerifyCompletion | اعتماد الإنجاز | ✓ | ✓ | ✓ | ✓ | | ✓ | | | |
| CorrectiveActions.ReturnForRework | إعادة للمعالجة | ✓ | ✓ | ✓ | ✓ | | ✓ | | | |
| CorrectiveActions.Reopen | إعادة فتح إجراء مكتمل | ✓ | ✓ | ✓ | ✓ | | ✓ | | | |
| CorrectiveActions.Cancel | إلغاء إجراء | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | | |
| CorrectiveActions.Archive | أرشفة حذف ناعم | ✓ | ✓ | | ✓ | | ✓ | | | |
| CorrectiveActions.Restore | استعادة من الأرشفة | ✓ | ✓ | | ✓ | | ✓ | | | |

## صلاحيات التصعيد والإشعارات (مفعّلة في B.2.2)

| الصلاحية | الوصف | SystemAdmin | HQ Executive | Decision Support Director | Regional Director | Regional Coordinator | Facility Director | Facility Coordinator | Auditor | ReadOnlyUser |
|----------|-------|:-----------:|:------------:|:--------------------------:|:------------------:|:---------------------:|:-------------------:|:----------------------:|:-------:|:------------:|
| Escalations.View | عرض سياسات التصعيد | ✓ | ✓ | ✓ | ✓ | | ✓ | | | |
| Escalations.Manage | إنشاء وتعديل السياسات والقواعد | ✓ | | ✓ | | | | | | |
| Escalations.Activate | تفعيل وتعطيل السياسات | ✓ | | ✓ | | | | | | |
| Escalations.Run | تشغيل يدوي | ✓ | | ✓ | | | | | | |
| Escalations.ViewOccurrences | عرض حوادث التصعيد | ✓ | ✓ | ✓ | ✓ | | ✓ | | | |
| Escalations.RetryFailed | إعادة محاولة الفشل | ✓ | | ✓ | | | | | | |
| Notifications.ViewOwn | عرض إشعارات المستخدم نفسه | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Notifications.MarkRead | تعليم إشعارات المستخدم نفسه كمقروءة | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Notifications.ArchiveOwn | أرشفة إشعارات المستخدم نفسه | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

## صلاحيات أنواع الملاحظات وإدخالها (مفعّلة في B.2.3.1)

هذه الصلاحيات لا تكفي وحدها لتنفيذ العمليات على الملاحظات؛ يجب تحقق RBAC العملي ونطاق المستخدم الجغرافي والتصنيف الأمني وCapability نوع الملاحظة.

| الصلاحية | الوصف | SystemAdmin | Decision Support Director | Regional Director | Facility Director |
|----------|-------|:-----------:|:--------------------------:|:------------------:|:-----------------:|
| Notes.ManageTypes | إدارة أنواع الملاحظات | ✓ | ✓ | | |
| Notes.ManageRoleTypeAccess | إدارة Grants الأدوار لأنواع الملاحظات | ✓ | ✓ | | |
| Notes.ManageUserTypeOverrides | إدارة منح/منع المستخدمين المباشرة | ✓ | ✓ | ضمن النطاق | ضمن السجن |
| Notes.ManageIntakeProfiles | إدارة تثبيت إدخال الملاحظات | ✓ | ✓ | ضمن النطاق | ضمن السجن |

## صلاحيات توجيه الملاحظات (مفعّلة في B.2.3.2)

| الصلاحية | الوصف | SystemAdmin | HQ Executive | Decision Support Director | Regional Director | Facility Director |
|----------|-------|:-----------:|:------------:|:--------------------------:|:------------------:|:-----------------:|
| Notes.ViewRouting | عرض قواعد ونتائج التوجيه | ✓ | ✓ | ✓ | ضمن النطاق | ضمن السجن |
| Notes.ManageRoutingRules | إنشاء وتعديل قواعد التوجيه | ✓ | | ✓ | ضمن النطاق | ضمن السجن |
| Notes.ActivateRoutingRules | تفعيل وتعطيل قواعد التوجيه | ✓ | | ✓ | ضمن النطاق | |
| Notes.RunRouting | تشغيل التوجيه يدويًا | ✓ | | ✓ | ضمن النطاق | ضمن السجن |
| Notes.ViewRoutingDiagnostics | عرض تشخيصات ومؤشرات التوجيه | ✓ | ✓ | ✓ | ضمن النطاق | |

Capabilities النوع:

- CanView
- CanCreate
- CanAssign
- CanProcess
- CanSubmitForVerification
- CanReview
- CanCancel
- CanReopen
- CanArchive
- CanRestore

### فصل الواجبات على الإجراءات الحرجة

للإجراء التصحيحي ذي أولوية `Critical`: أي مستخدم شارك في المعالجة الفعلية لا يعتمد الإنجاز النهائي، حتى إذا كان `SystemAdministrator`. المشاركة تُستنتج من `CorrectiveActionStatusHistory` عبر:

- `Assigned → InProgress`
- `Reopened → InProgress`
- `InProgress → PendingVerification`

### فصل الواجبات على الملاحظات الحرجة (Critical SoD)

لملاحظة بمستوى خطورة Critical تحديدًا: **لا يجوز لأي مستخدم شارك في المعالجة الفعلية للملاحظة الحرجة
اعتماد إغلاقها النهائي، حتى لو كان مسؤول النظام.**

المشاركة الفعلية تُستنتج من سجل `NoteStatusHistory` (وليس من `LastProcessedByUserId` وحده) عبر الانتقالات:

- `Assigned → InProgress` أو `Reopened → InProgress` (start-work)
- `InProgress → PendingVerification` (submit-for-verification)

لا يُعتبر `PendingVerification → InProgress` (return-for-rework) معالجة فعلية للمعتمد.
لا تُعدّ عمليات الإنشاء / التقديم / التكليف / الإلغاء / إعادة الفتح / العرض مشاركة معالجة تلقائية.

الفحص منفصل تمامًا عن التحقق من الصلاحية (`Notes.VerifyClosure` مطلوبة أولاً، ثم فحص SoD)، ويحدث قبل أي Mutation.

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
| ملاحظة حرجة | أي مشارك في المعالجة الفعلية ≠ المعتمد النهائي (مُفعّلة عبر NoteStatusHistory؛ SystemAdministrator لا يتجاوز) |
| إجراء تصحيحي حرج | أي مشارك في المعالجة الفعلية ≠ معتمد الإنجاز (مُفعّلة عبر CorrectiveActionStatusHistory؛ SystemAdministrator لا يتجاوز) |
| واقعة جسيمة | مدخل التقرير ≠ المعتمد النهائي |
| تصدير حساس | يتطلب `Reports.ExportSensitive` أو `Attachments.DownloadSensitive` + تسجيل تدقيق |

تُنفَّذ قواعد SoD في طبقة Application عند تفعيل الوحدات المعنية؛ في المرحلة A تُوثَّق وتُختبر بنية الصلاحيات والنطاق.
