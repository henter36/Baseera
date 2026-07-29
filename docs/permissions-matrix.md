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
| FormApprover | معتمد نماذج |
| FormPublisher | ناشر نماذج (C.2+) |
| FormRespondent | مستجيب نماذج (C.2+) |
| FormRegionalMonitor | مراقب نماذج إقليمي |
| FormHeadquartersMonitor | مراقب نماذج المقر |
| FormAnalyst | محلل نماذج |
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

### UX Rescue Phase 1A — لا صلاحيات جديدة

مساحة عمل الملاحظات (`/notes/workspace`) وإنشاء ملاحظة من `workspaces/facilities/:facilityId` يعيدان استخدام هذا الجدول حرفيًا — لم تُضَف أي صلاحية `Notes.*` جديدة. المرجع المصدري الوحيد لتحديد الإجراءات الظاهرة داخل الـWorkspace هو `NoteWorkspaceQueryService.ComputeAllowedActions` (يقرأ `NoteStateMachine.CanTransition` + صلاحية المستخدم فعليًا)، وليس أي منطق واجهة يخمّن الإجراء التالي من الحالة وحدها. الفجوة الوحيدة التي أُصلحت هنا: `Notes.VerifyClosure` كان له Endpoint ونقطة API فعليتان لكنه لم يكن يظهر إطلاقًا ضمن `AllowedActions` — تمت إضافته لقائمة الحساب فقط، دون أي تغيير في مصفوفة الأدوار أعلاه. صلاحية عرض/إنشاء الملاحظة من Facility Workspace هي نفسها `Notes.View`/`Notes.Create` أعلاه؛ لا صلاحية "Facility Workspace" توسّع صلاحيات الملاحظات (السجن لا يمنح وصولاً إضافيًا).

### UX Rescue Phase 1B — 7 صلاحيات جديدة (بوابة الفرز، اعتماد القرار الموحَّد، تجميد SLA)

| الصلاحية | الوصف | SystemAdmin | HQ Executive | Decision Support Director | Regional Director | Regional Coordinator | Facility Director |
|----------|-------|:-----------:|:------------:|:--------------------------:|:------------------:|:---------------------:|:-------------------:|
| Notes.ProposeInvalid | اقتراح اعتبار ملاحظة غير صحيحة | ✓ | | ✓ | ✓ | ✓ | ✓ |
| Notes.ApproveInvalid | اعتماد قرار غير صحيحة (مراجع مستقل عن المقترح) | ✓ | ✓ | ✓ | ✓ | | ✓ |
| Notes.ProposeDuplicate | اقتراح اعتبار ملاحظة مكررة | ✓ | | ✓ | ✓ | ✓ | ✓ |
| Notes.ApproveDuplicate | اعتماد قرار التكرار | ✓ | ✓ | ✓ | ✓ | | ✓ |
| Notes.ProposeNoAction | اقتراح عدم الحاجة إلى إجراء | ✓ | | ✓ | ✓ | ✓ | ✓ |
| Notes.ApproveNoAction | اعتماد عدم الحاجة إلى إجراء | ✓ | ✓ | ✓ | ✓ | | ✓ |
| Notes.ApproveSlaPause | اعتماد تجميد SLA أثناء انتظار القطع | ✓ | ✓ | ✓ | ✓ | | ✓ |

**فصل الواجبات هنا شخصي لا دوري**: عمدًا لم تُفصَل الأدوار بين Propose/Approve (`RegionalDirector`/`FacilityDirector`/`DecisionSupportDirector` يملكون الاثنين معًا) — المنع الفعلي هو أن مُقترِح قرار بعينه لا يستطيع اعتماد ذلك القرار تحديدًا (`ProposedByUserId != ReviewedByUserId` على مستوى السجل)، وليس منع الدور من الفعلَين معًا. `RegionalCoordinator` استثناء متعمَّد: يملك Propose* فقط (لا يملك `Notes.VerifyClosure` أصلًا، فهو طبقة معالجة لا اعتماد). لا تغيير على مصفوفة صلاحيات B.1 أعلاه. تفصيل كامل: `phase1b-observation-permissions.md`.

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

## صلاحيات لوحة المتابعة (مفعّلة في B.3.1)

| الصلاحية | الوصف | SystemAdmin | HQ Executive | Decision Support Director | Regional Director | Facility Director | Auditor | ReadOnly |
|----------|-------|:-----------:|:------------:|:--------------------------:|:------------------:|:-----------------:|:-------:|:--------:|
| Dashboard.ViewOperational | عرض العبء والاتجاهات والتقسيمات | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Dashboard.ViewRisk | عرض مؤشرات المخاطر والتأخر | ✓ | ✓ | ✓ | ✓ | ✓ | | |
| Dashboard.ViewRouting | عرض مؤشرات التوجيه في اللوحة | ✓ | ✓ | ✓ | | | | |
| Dashboard.ViewCorrectiveActions | عرض مؤشرات الإجراءات التصحيحية | ✓ | ✓ | ✓ | ✓ | ✓ | | |

## صلاحيات الإشغال وحركة النزلاء (Phase D.4)

| الصلاحية | الوصف | SystemAdmin | HQ Executive | Decision Support Director | Regional Director | Regional Coordinator | Facility Director | Facility Coordinator | Prisoner Case Officer |
|----------|-------|:-----------:|:------------:|:--------------------------:|:------------------:|:---------------------:|:-----------------:|:--------------------:|:---------------------:|
| Occupancy.ViewSummary | عرض ملخص الإشغال غير التعريفي | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Occupancy.ViewUnitBreakdown | عرض إشغال الوحدات | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Occupancy.ViewMovements | عرض مؤشرات الحركة المجمعة | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Occupancy.ViewSensitiveMovements | عرض تفاصيل حركة حساسة مستقبلًا | ✓ | | ✓ | | | | | ✓ |
| Occupancy.ManageCapacity | تسجيل طاقة معتمدة | ✓ | | ✓ | ✓ | | ✓ | | ✓ |
| Occupancy.RecordSnapshot | تسجيل Snapshot إحصائي | ✓ | | ✓ | ✓ | | ✓ | | ✓ |
| Occupancy.Import | استيراد حركات مموهة | ✓ | | ✓ | ✓ | | ✓ | | ✓ |
| Occupancy.Export | تصدير بيانات إشغال مستقبلًا | ✓ | | ✓ | | | | | ✓ |
| Occupancy.Reconcile | تنفيذ reconciliation مستقبلًا | ✓ | | ✓ | ✓ | | ✓ | | ✓ |

`Workspaces.ViewFacility` لا يمنح أي صلاحية إشغال بمفرده. Missing permission للـendpoint المباشر تعود `403`، وخارج النطاق يعود `404`.

## صلاحيات جاهزية الموارد الأساسية (Phase D.5)

تنطبق على المركبات وأجهزة الاتصال والمعدات التشغيلية/الأمنية غير المصنفة كأسلحة والأصول الثابتة فقط. لا تشمل القوى البشرية، الأسلحة، العهد الحساسة، المخزون العام، أو المشتريات المالية.

| الصلاحية | الوصف | SystemAdmin | HQ Executive | Decision Support Director | Regional Director | Regional Coordinator | Facility Director | Facility Coordinator | Fleet Officer |
|----------|-------|:-----------:|:------------:|:--------------------------:|:------------------:|:---------------------:|:-----------------:|:--------------------:|:-------------:|
| Resources.ViewSummary | عرض ملخص الجاهزية والفجوات | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Resources.ViewAssets | عرض سجلات الموارد غير الحساسة (مع صلاحية النوع) | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Resources.ViewVehicles | عرض تفاصيل المركبات (يشمل لوحة المركبة) | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Resources.ViewCommunicationDevices | عرض أجهزة الاتصال | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Resources.ViewEquipment | عرض المعدات التشغيلية والأمنية غير المصنفة كأسلحة | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Resources.ViewFacilityAssets | عرض المرافق والأصول الثابتة | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Resources.ManageAssets | إنشاء وتحديث تعريف المورد | ✓ | | ✓ | ✓ | | ✓ | | ✓ |
| Resources.ManagePlacements | إدارة الموقع التشغيلي والموضع | ✓ | | ✓ | ✓ | | ✓ | | ✓ |
| Resources.ManageStatus | تغيير حالة المورد | ✓ | | ✓ | ✓ | | ✓ | | ✓ |
| Resources.ViewMaintenance | عرض الصيانة والسجل المرتبط | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Resources.ManageMaintenance | إنشاء أوامر الصيانة وإدارتها | ✓ | | ✓ | ✓ | | ✓ | | ✓ |
| Resources.ViewRequirements | عرض baseline الاحتياج والفجوة | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Resources.ManageRequirements | إدارة احتياجات الموارد | ✓ | | ✓ | ✓ | | ✓ | | ✓ |
| Resources.Import | استيراد الموارد مع preview/confirm | ✓ | | ✓ | ✓ | | ✓ | | ✓ |
| Resources.Export | تصدير بيانات الموارد | ✓ | | ✓ | | | | | |
| Resources.Reconcile | مصالحة بيانات الموارد مستقبلًا | ✓ | | ✓ | ✓ | | ✓ | | ✓ |

`Workspaces.ViewFacility` لا يمنح أي صلاحية موارد بمفرده. المستخدم غير المخول لا يرى قسم الموارد أو أعداده داخل مساحة السجن، والـendpoint المباشر يعيد `403` عند نقص الصلاحية و`404` عند الخروج من النطاق.

## صلاحيات جاهزية القوى البشرية (Phase D.5.1)

تنطبق على أعضاء القوى البشرية التشغيلية والتغطية والمناوبات فقط. لا تشمل الأسلحة، الرواتب، أو تجميع Region/HQ.

| الصلاحية | الوصف | SystemAdmin | HQ Executive | Decision Support Director | Regional Director | Regional Coordinator | Facility Director | Facility Coordinator | Workforce Officer |
|----------|-------|:-----------:|:------------:|:--------------------------:|:------------------:|:---------------------:|:-----------------:|:--------------------:|:-----------------:|
| Workforce.ViewSummary | عرض ملخص التغطية وجودة البيانات وwidget المساحة | ✓ | ✓ | ✓ | | | ✓ | | ✓ |
| Workforce.ViewCoverage | عرض التغطية والوحدات والمتطلبات والجداول | ✓ | ✓ | ✓ | | | ✓ | | ✓ |
| Workforce.ViewMembers | عرض الأعضاء والأدوار التشغيلية | ✓ | ✓ | ✓ | | | ✓ | | ✓ |
| Workforce.ViewSensitiveRestrictions | عرض أكواد القيود التشغيلية الحساسة | ✓ | | ✓ | | | ✓ | | ✓ |
| Workforce.ManageMembers | إنشاء أعضاء القوى البشرية | ✓ | | ✓ | | | ✓ | | ✓ |
| Workforce.ManageAssignments | إدارة التكليفات التشغيلية | ✓ | | ✓ | | | ✓ | | ✓ |
| Workforce.ManageQualifications | إدارة المؤهلات والاعتمادات | ✓ | | ✓ | | | ✓ | | ✓ |
| Workforce.ManageRequirements | إدارة احتياجات التغطية | ✓ | | ✓ | | | ✓ | | ✓ |
| Workforce.ManageRosters | إنشاء ونشر جداول المناوبات | ✓ | | ✓ | | | ✓ | | ✓ |
| Workforce.RecordAvailability | تسجيل التوفر والغياب والقيود | ✓ | | ✓ | | | ✓ | | ✓ |
| Workforce.Import | استيراد القوى البشرية مع preview/confirm | ✓ | | ✓ | | | ✓ | | ✓ |
| Workforce.Export | تصدير بيانات القوى البشرية (صلاحية مُعدّة؛ واجهة التصدير غير مشحونة في هذه الشريحة) | ✓ | | ✓ | | | ✓ | | ✓ |
| Workforce.Reconcile | عرض ومعالجة فروقات مصالحة بيانات القوى البشرية | ✓ | | ✓ | | | ✓ | | ✓ |

`Workspaces.ViewFacility` لا يمنح أي صلاحية قوى بشرية بمفرده. المستخدم غير المخول لا يرى قسم `القوى البشرية والتغطية` أو أعداده، والـendpoint المباشر يعيد `403` عند نقص الصلاحية و`404` عند الخروج من النطاق. بذرة التطوير الحالية تمنح مجموعة الملخص لـ HQ Executive ومجموعة الإدارة لـ Decision Support Director وFacility Director وWorkforce Officer؛ Regional/Facility Coordinator لا يحصلان على `Workforce.*` في هذه البذرة.

## صلاحيات الأسلحة والذخائر والعهد الحساسة (Phase D.5.2)

تنطبق على الأسلحة، الذخائر، مواقع العهد الحساسة، سلسلة التسليم والاستلام، الجرد، الفحص، والاستيراد/المطابقة. لا تمتد من `Resources.*` ولا من `Workspaces.ViewFacility`.

| الصلاحية | الوصف | SystemAdmin | HQ Executive | Decision Support Director | Regional Director | Facility Director | Armament Officer |
|----------|-------|:-----------:|:------------:|:--------------------------:|:------------------:|:-----------------:|:----------------:|
| SensitiveCustody.ViewSummary | عرض الملخص الآمن وقسم Workspace دون serials أو مواقع تفصيلية | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| SensitiveCustody.ViewWeapons | عرض قائمة الأسلحة مع serial masked | ✓ | | ✓ | | ✓ | ✓ |
| SensitiveCustody.ViewSerialNumbers | عرض serial المحمي عند الحاجة التشغيلية | ✓ | | ✓ | | | ✓ |
| SensitiveCustody.ViewArmoryLocations | عرض مواقع العهد التفصيلية | ✓ | | ✓ | | | ✓ |
| SensitiveCustody.ViewAmmunition | عرض الذخيرة والرصيد الآمن | ✓ | | ✓ | | ✓ | ✓ |
| SensitiveCustody.ViewCustodyTransactions | عرض سلسلة العهد والتسليم | ✓ | | ✓ | | ✓ | ✓ |
| SensitiveCustody.ManageWeapons | إنشاء وتحديث تعريف السلاح | ✓ | | ✓ | | | ✓ |
| SensitiveCustody.IssueWeapons | إنشاء عمليات صرف/نقل عهدة | ✓ | | ✓ | | | ✓ |
| SensitiveCustody.ReceiveWeapons | تأكيد التسليم والاستلام والإرجاع | ✓ | | ✓ | | | ✓ |
| SensitiveCustody.ApproveTransactions | اعتماد عمليات العهد الحساسة مع four-eyes | ✓ | | ✓ | | ✓ | |
| SensitiveCustody.ManageAmmunition | تسجيل حركات الذخيرة | ✓ | | ✓ | | | ✓ |
| SensitiveCustody.ConductInventory | بدء الجرد وإضافة قيوده | ✓ | | ✓ | | | ✓ |
| SensitiveCustody.ApproveInventory | اعتماد الجرد مع four-eyes | ✓ | | ✓ | | ✓ | |
| SensitiveCustody.ManageInspections | تسجيل الفحص | ✓ | | ✓ | | | ✓ |
| SensitiveCustody.ManageMaintenance | إدارة صيانة السلاح | ✓ | | ✓ | | | ✓ |
| SensitiveCustody.ViewDiscrepancies | عرض فروقات الجرد | ✓ | | ✓ | | ✓ | ✓ |
| SensitiveCustody.Export | تصدير منضبط ومفصول الصلاحية | ✓ | | ✓ | | | |
| SensitiveCustody.Import | preview/confirm لاستيراد العهد الحساسة | ✓ | | ✓ | | | ✓ |
| SensitiveCustody.Reconcile | مصالحة مصادر العهد الحساسة | ✓ | | ✓ | | | ✓ |

المسار خارج نطاق المستخدم يعيد `404` لمنع التعداد، ونقص الصلاحية داخل النطاق يعيد `403`. `ViewWeapons` لا تمنح `ViewSerialNumbers`، و`IssueWeapons` لا تمنح `ApproveTransactions`.

## صلاحيات سجل المخاطر ومركز المعالجة (Phase D.6)

تنطبق على سجل المخاطر، المصفوفات، التقييمات، الضوابط، خطط ومعالجة المخاطر، المراجعات، الروابط النمطية، والاستيراد/المصالحة. لا تمتد من `Workspaces.ViewFacility`. دور جديد `RiskOfficer` (ضابط مخاطر) يحمل حزمة `riskManager` (18 صلاحية من 25 — تحقَّق مطابقتها مع seed الفعلي في `DatabaseInitializer.cs` وليس افتراضًا) — **وليست** "الحزمة الكاملة": يستثني الدور 7 صلاحيات مقيَّدة: 6 صلاحيات اعتماد (`ReviewAssessment`, `ApproveAssessment`, `VerifyTreatmentActions`, `ApproveAcceptance`, `ApproveClosure`, `ApproveMatrices`) بالإضافة إلى صلاحية التصدير (`Risks.Export`).

يجب التمييز بين ثلاث طبقات منفصلة هنا لتجنّب الخلط:

1. **منح الصلاحية للدور** (`Grant(role, permission)` في `DatabaseInitializer`): مجرد كون صلاحية ما ضمن حزمة دور لا يعني وحده أي شيء عن فصل المهام — إنه فقط تفويض بتنفيذ عملية معيّنة.
2. **فرض فصل المهام (`EnforceFourEyes`) على العملية نفسها**: الصلاحية وحدها **لا تكفي** لمنع منشئ العملية من اعتمادها بنفسه؛ التطبيق يفرض ذلك صراحة (خادم-جانب) عبر فحص `RiskServiceBase.EnforceFourEyes(submittedBy)` عند نقاط الاعتماد الخمس (مراجعة/اعتماد التقييم، اعتماد خطة المعالجة، التحقق من الإجراء، القبول، الإغلاق) وعند اعتماد المصفوفة. الاستثناءات الست أعلاه (عدا `Export`) هي الصلاحيات المرتبطة بنقاط `EnforceFourEyes` هذه تحديدًا.
3. **`Risks.Export` لا علاقة لها بفصل المهام إطلاقًا** — إنها صلاحية تصدير/تقارير منفصلة تمامًا، مُسندة فقط للأدوار الإشرافية/الرقابية (`FacilityDirector`, `DecisionSupportDirector`) ضمن حزمة `riskApprover`، ولا يوجد أي فحص `EnforceFourEyes` مرتبط بها.

`Auditor` غير مذكور في الجدول أدناه لأنه لا يحصل إلا على `Risks.ViewSummary` — نفس حزمة `RegionalDirector`/`HeadquartersExecutive` (انظر السطر الأخير من هذا القسم).

| الصلاحية | الوصف | SystemAdmin | HQ Executive | Decision Support Director | Regional Director | Facility Director | Risk Officer |
|----------|-------|:-----------:|:------------:|:--------------------------:|:------------------:|:-----------------:|:------------:|
| Risks.ViewSummary | عرض ملخص المخاطر وودجت مساحة العمل دون تفاصيل | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Risks.View | عرض سجل المخاطر وتفاصيله | ✓ | | ✓ | | ✓ | ✓ |
| Risks.ViewSensitive | عرض المخاطر ذات التصنيف الحساس | ✓ | | ✓ | | ✓ | ✓ |
| Risks.Create / Risks.Update | تسجيل/تحديث بيانات الخطر | ✓ | | | | | ✓ |
| Risks.AssignOwner | تعيين مالك الخطر | ✓ | | | | | ✓ |
| Risks.ManageCategories | إدارة تصنيفات المخاطر | ✓ | | | | | ✓ |
| Risks.Assess | إنشاء/إرسال تقييم | ✓ | | | | | ✓ |
| Risks.ReviewAssessment | مراجعة التقييم (four-eyes مقابل المُقيِّم) | ✓ | | ✓ | | ✓ | |
| Risks.ApproveAssessment | اعتماد التقييم (four-eyes مقابل المُقيِّم) | ✓ | | ✓ | | ✓ | |
| Risks.ManageControls | إدارة الضوابط الحالية | ✓ | | | | | ✓ |
| Risks.ManageTreatments | إنشاء/تنفيذ خطط المعالجة، **واعتمادها** (فصل المهام لا الصلاحية يمنع الاعتماد الذاتي) | ✓ | | | | ✓ | ✓ |
| Risks.CompleteTreatmentActions | تنفيذ/تقديم إجراء معالجة للتحقق | ✓ | | | | | ✓ |
| Risks.VerifyTreatmentActions | التحقق من اكتمال إجراء معالجة (four-eyes مقابل المُنفِّذ) | ✓ | | ✓ | | ✓ | |
| Risks.RequestAcceptance | طلب قبول الخطر | ✓ | | | | | ✓ |
| Risks.ApproveAcceptance | اعتماد قبول الخطر (four-eyes) | ✓ | | ✓ | | ✓ | |
| Risks.RequestClosure | طلب إغلاق الخطر | ✓ | | | | | ✓ |
| Risks.ApproveClosure | اعتماد إغلاق الخطر (four-eyes) | ✓ | | ✓ | | ✓ | |
| Risks.Reopen | إعادة فتح خطر مغلق | ✓ | | | | | ✓ |
| Risks.Escalate | تصعيد الخطر | ✓ | | | | | ✓ |
| Risks.LinkSources | ربط/فك ربط مصادر وأدلة | ✓ | | | | | ✓ |
| Risks.Export | تصدير بيانات المخاطر | ✓ | | ✓ | | ✓ | |
| Risks.Import | الاستيراد المنضبط والمصالحة | ✓ | | | | | ✓ |
| Risks.ManageMatrices | إدارة مصفوفات التقييم | ✓ | | | | | ✓ |
| Risks.ApproveMatrices | اعتماد وتفعيل مصفوفات التقييم | ✓ | | ✓ | | ✓ | |

المسار خارج نطاق المستخدم يعيد `404`، ونقص الصلاحية داخل النطاق يعيد `403`. `Risks.View` لا تمنح `Risks.ViewSensitive`. `Risks.Assess` لا تمنح `Risks.ApproveAssessment`. `Risks.RequestAcceptance`/`Risks.RequestClosure` لا تمنحان صلاحيات الاعتماد المقابلة. اعتماد خطة المعالجة والتحقق من إجراءاتها يستخدمان **فصل المهام (four-eyes) بدل صلاحية منفصلة** — تفصيل القرار في `docs/phase-d6-risk-permissions.md`. `RegionalDirector` و`HeadquartersExecutive` يريان `Risks.ViewSummary` فقط (عدادات دون تفاصيل)، اتساقًا مع بقية جداول هذه المرحلة.

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

## صلاحيات النماذج (مفعّلة في C.1)

النطاق والتصنيف يتبعان نفس قواعد الملاحظات: خارج النطاق → `404`؛ داخل النطاق بدون صلاحية → `403`. المنح (Grants) على مستوى النموذج: Deny يلغي Allow.

| الصلاحية | الوصف | SystemAdmin | FormDesigner | FormReviewer | FormApprover | Auditor |
|----------|-------|:-----------:|:------------:|:------------:|:------------:|:-------:|
| Forms.View | عرض النماذج ضمن النطاق | ✓ | ✓ | ✓ | ✓ | ✓ |
| Forms.ViewSensitive | عرض محتوى Confidential/Secret | ✓ | | | | |
| Forms.Create | إنشاء مسودة | ✓ | ✓ | | | |
| Forms.UpdateDraft | تحديث مسودة/تعديلات مطلوبة | ✓ | ✓ | | | |
| Forms.SubmitForReview | إرسال للمراجعة | ✓ | ✓ | | | |
| Forms.Review | مراجعة | ✓ | | ✓ | | |
| Forms.RequestChanges | طلب تعديلات | ✓ | | ✓ | | |
| Forms.Approve | اعتماد | ✓ | | | ✓ | |
| Forms.Reject | رفض | ✓ | | ✓ | ✓ | |
| Forms.Archive | أرشفة | ✓ | | | | |
| Forms.Restore | استعادة | ✓ | | | | |
| Forms.ManageAccess | إدارة منح الوصول | ✓ | | | | |
| Forms.ManageGovernance | إدارة سياسة الحوكمة | ✓ | | | | |
| Forms.ManageRetention | إدارة الاحتفاظ | ✓ | | | | |

صلاحيات C.2+ — **تصحيح (Phase 2A)**: `Forms.Publish` و`Forms.Respond` و`Forms.MonitorRegion` و`Forms.MonitorHeadquarters` و`Forms.ApproveResponses` مفعّلة وموصولة فعليًا بنقاط API حقيقية (`RequireAuthorization` على مسارات الحملات/الاستجابات)، وليست Seed فقط كما كان موثَّقًا سابقًا. `Forms.Analyze` و`Forms.Export` فقط يبقيان Seed فقط — معرَّفان في `PermissionCodes` لكن غير مستخدَمين في أي `RequireAuthorization()` حتى الآن.

## صلاحيات مسجّلة للوحدات اللاحقة (Seed فقط في A)

`Vehicles.*`, `Armament.*`, `Incidents.*`, `Projects.*`, `Strategy.*`, `Reports.ExportSensitive`, `Plans.*`, `Decisions.*`, `PrisonerFollowUp.*`

ملاحظة: `Workforce.*` مفعّلة في Phase D.5.1 — انظر قسم «صلاحيات جاهزية القوى البشرية» أعلاه. `Resources.*` مفعّلة في Phase D.5 و`Occupancy.*` في Phase D.4.

## قواعد فصل الواجبات (SoD)

| العملية | القاعدة |
|---------|---------|
| حركة تسليح | المنشئ ≠ المعتمد لنفس الحركة |
| ملاحظة حرجة | أي مشارك في المعالجة الفعلية ≠ المعتمد النهائي (مُفعّلة عبر NoteStatusHistory؛ SystemAdministrator لا يتجاوز) |
| إجراء تصحيحي حرج | أي مشارك في المعالجة الفعلية ≠ معتمد الإنجاز (مُفعّلة عبر CorrectiveActionStatusHistory؛ SystemAdministrator لا يتجاوز) |
| واقعة جسيمة | مدخل التقرير ≠ المعتمد النهائي |
| تصدير حساس | يتطلب `Reports.ExportSensitive` أو `Attachments.DownloadSensitive` + تسجيل تدقيق |

تُنفَّذ قواعد SoD في طبقة Application عند تفعيل الوحدات المعنية؛ في المرحلة A تُوثَّق وتُختبر بنية الصلاحيات والنطاق.

## Phase C.2 additions
| Permission | Purpose |
|------------|---------|
| Forms.CloneVersion | Clone a form version |
| Forms.ViewVersionHistory | View version history |
| Forms.ManageTemplates | Create templates / forms from templates |


## Phase C.3 campaign permissions

`Forms.ManageCampaigns`, `Forms.PreviewTargets`, `Forms.PauseCampaign`, `Forms.CancelCampaign`, `Forms.ViewCampaignAssignments` (+ wired `Forms.Publish`). FormResponse permissions remain for #48.

## Phase 2A — استوديو تصميم النماذج الموحَّد

لا صلاحيات جديدة أُضيفت؛ التوجيه الصريح لهذه المرحلة كان "راجع الصلاحيات الحالية ولا تضف مكررًا". جدول التعيين بين الصلاحيات المفاهيمية المطلوبة في تكليف المرحلة والصلاحيات الفعلية الموجودة:

| الصلاحية المفاهيمية (نص التكليف) | الصلاحية الفعلية المستخدَمة | ملاحظة |
|---|---|---|
| Forms.View | `Forms.View` | كما هي |
| Forms.Create | `Forms.Create` | يشمل بداية الاستوديو (فارغ/قالب/نسخ) |
| Forms.Update | `Forms.UpdateDraft` | لا صلاحية منفصلة لتعديل بيانات النموذج الوصفية عبر الاستوديو؛ `FormEditPage` المستقلة تستخدم `Forms.Update` الفعلية أيضًا |
| Forms.DeleteDraft | — | **غير موجودة فعليًا في الكود ولم تُضَف**: لا يوجد مسار حذف صريح لمسودة نموذج/إصدار اليوم (فقط أرشفة عبر `Forms.Archive`)؛ لا حاجة لصلاحية حذف منفصلة |
| Forms.Design | `Forms.UpdateDraft` | تصميم الحقول/الصفحات/الأقسام كلها تحت نفس صلاحية تعديل المسودة الحالية |
| Forms.ManageConditions / Forms.ManageFormulas | `Forms.UpdateDraft` | Condition Builder وFormula Builder جزء من تعديل المسودة، لا صلاحية منفصلة — نفس مستوى الحبيبية (granularity) الموجود أصلاً في `IFormVersionService` |
| Forms.Preview | `Forms.UpdateDraft` (+ `Forms.ViewVersionHistory` للقراءة فقط) | المعاينة تُبنى من نفس الـSchema المحلي، لا نقطة API مخصصة |
| Forms.Validate | `Forms.View` | يطابق صلاحية نقطة `POST .../validate` الفعلية في `ApiEndpoints.cs` |
| Forms.RequestReview | `Forms.SubmitForReview` | نفس الاسم الفعلي المستخدَم في الكود، لا تسمية "RequestReview" في الخادم |
| Forms.Review / Forms.Approve | `Forms.RequestChanges` + `Forms.Reject` (مراجعة) و`Forms.Approve` (اعتماد) | `StudioReviewPanel` يعرض الأزرار حسب `allowedActions` الفعلية القادمة من الخادم |
| Forms.Publish | `Forms.Publish` | غير مستخدَم مباشرة داخل الاستوديو؛ رابط "الانتقال إلى الجدولة والنشر" يقود لمعالج الحملة (`Forms.ManageCampaigns`) |
| Forms.ViewVersions | `Forms.ViewVersionHistory` | يشمل أيضًا صفحة مقارنة الإصدارات الجديدة `/forms/:formId/versions/compare` |
| Forms.CreateVersion | `Forms.UpdateDraft` (إنشاء إصدار داخل نفس النموذج) و`Forms.CloneVersion` (نسخ إصدار)، و**جديد**: نقطة `POST /api/v1/forms/copy-from/{sourceFormId}/{sourceVersionId}` تتطلب `Forms.Create` (لإنشاء النموذج الجديد) بالإضافة إلى فحص داخلي لـ`Forms.UpdateDraft` | انظر أدناه |
| Forms.ManageTemplates | `Forms.ManageTemplates` (إدارة/إنشاء قوالب) و`Forms.View` (معاينة/سرد القوالب، بما فيها نقطة المعاينة الجديدة `GET /api/v1/form-templates/{id}/schema`) | استخدام قالب لبدء نموذج لا يتطلب `Forms.ManageTemplates` — فقط عرض النموذج المطلوب لإنشائه (`Forms.Create` + `Forms.ManageTemplates` الفعليان في `CreateFormFromTemplateAsync`، غير معدَّل) |

**نقطة Backend جديدة واحدة مضافة في هذه المرحلة**: `POST /api/v1/forms/copy-from/{sourceFormId}/{sourceVersionId}` (نسخ نموذج موجود إلى مسودة جديدة مستقلة، دون نسخ الاستجابات أو الحملات، مع تسجيل المصدر في AuditLog) — محمية بـ`Forms.Create` على مستوى الـEndpoint، وتتحقق داخليًا من `Forms.UpdateDraft` وأن المستخدم يملك صلاحية عرض (`FormAccessCapability.View`) على النموذج المصدر (نفس فحص النطاق `formScope`/`effectiveAccess` المستخدَم في بقية `FormVersionService`؛ خارج النطاق أو بلا صلاحية → `404`، وليس `403`، اتساقًا مع بقية الخدمة). ونقطة قراءة واحدة: `GET /api/v1/form-templates/{templateId}/schema` (معاينة القالب قبل الاستخدام) — محمية بـ`Forms.View` وتتبع بالضبط نفس قواعد رؤية القالب المستخدَمة في `GET /api/v1/form-templates` (استُخرجت في دالة مشتركة `BuildVisibleTemplatesQueryAsync` لمنع الانحراف بين القائمة والمعاينة).

## Phase D.0 workspace permissions

| Permission | Purpose |
|------------|---------|
| Workspaces.View | View registered workspace shells and authorized widgets |
| Workspaces.ViewDomain | Resolve domain-level workspace context |
| Workspaces.ViewFacility | Resolve facility-level workspace context |
| Workspaces.ViewRegion | Resolve region-level workspace context |
| Workspaces.ViewHeadquarters | Resolve headquarters-level workspace context |
| Workspaces.ConfigureOwnView | Future personal layout configuration boundary for #21 |

Widget data still requires its module permission, such as `Dashboard.ViewOperational` or `Dashboard.ViewCorrectiveActions`.

## Phase D.1 facility workspace MVP

`facility-operations` requires `Workspaces.View` + `Workspaces.ViewFacility` and a valid in-scope `facilityId`.

| Widget | Additional permission |
|--------|------------------------|
| Facility context | Workspaces.ViewFacility |
| Executive summary | Dashboard.ViewOperational |
| Notes overview | Notes.View |
| Corrective actions | CorrectiveActions.View |
| Alerts/escalations | Escalations.ViewOccurrences |
| Form compliance | Forms.ViewComplianceDashboard |
| Priority queue | Dashboard.ViewRisk |
| Recent activity | Dashboard.ViewOperational |

Facility Workspace permission alone does not reveal domain widget data.
