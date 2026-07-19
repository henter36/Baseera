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

## صلاحيات مسجّلة للوحدات اللاحقة (Seed فقط في A)

`Vehicles.*`, `Armament.*`, `Notes.*`, `Incidents.*`, `Forms.*`, `Projects.*`, `Strategy.*`, `Reports.ExportSensitive`, `Plans.*`, `Workforce.*`, `Decisions.*`, `PrisonerFollowUp.*`

## قواعد فصل الواجبات (SoD)

| العملية | القاعدة |
|---------|---------|
| حركة تسليح | المنشئ ≠ المعتمد لنفس الحركة |
| ملاحظة حرجة | المعالج ≠ المعتمد النهائي للإغلاق منفردًا |
| واقعة جسيمة | مدخل التقرير ≠ المعتمد النهائي |
| تصدير حساس | يتطلب `Reports.ExportSensitive` أو `Attachments.DownloadSensitive` + تسجيل تدقيق |

تُنفَّذ قواعد SoD في طبقة Application عند تفعيل الوحدات المعنية؛ في المرحلة A تُوثَّق وتُختبر بنية الصلاحيات والنطاق.
