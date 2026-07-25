# Phase D.5.1 Workforce — Current State Analysis

Analysis against `main` after merge of PR #130 (Occupancy) and PR #132 (Resource Readiness Core).

| المجال | الموجود | مصدر الحقيقة | قابل لإعادة الاستخدام | الفجوة | قرار التنفيذ |
| ------ | ------- | ------------ | --------------------- | ------ | ------------ |
| User / Identity | `User`, `UserRole`, `UserScope`, Entra subject | Identity DB | Auth, scopes, audit actor | ليس سجل موظف تشغيلي | الإبقاء على User كحساب دخول فقط؛ `WorkforceMember.UserId?` اختياري |
| Employee / Staff / Person | غير موجود | — | — | لا يوجد نموذج قوى بشرية | إنشاء `WorkforceMember` مستقل |
| Organization / Region / Facility / FacilityUnit | كامل | Org hierarchy | FKs ونطاقات | — | إعادة استخدام مباشرة |
| RBAC Role | `Role`, `Permission`, `WorkforceOfficer` placeholder | Identity | تسمية الأدوار | لا أدوار تشغيلية | `WorkforceRoleDefinition` منفصل عن RBAC |
| Assignments (workflow) | Note/CA assignees | Workflow | أنماط تكليف | ليست تكليفات تشغيلية | `WorkforceAssignment` جديد |
| Shifts / Rosters / Attendance | غير موجود | — | — | كامل | `ShiftDefinition`, `DutyRoster`, `DutyRosterAssignment` |
| Skills / Qualifications | غير موجود | — | — | كامل | `WorkforceQualification` |
| Availability / Leave | غير موجود | — | — | كامل | `WorkforceAvailabilityEvent` |
| Staffing requirements | غير موجود | — | نمط `ResourceRequirement` | كامل | `StaffingRequirement` |
| Resource readiness (D.5) | كامل | Resources | Policy، import، permissions، widget | مختلف عن البشر | محاكاة الأنماط لا الكيانات |
| Occupancy (D.4) | كامل | Occupancy | Snapshot، freshness، widget | مختلف عن البشر | محاكاة freshness/confidence |
| Facility Workspace | كامل | Workspace framework | Sections، Context Panel، Queue | فجوة بيانات القوى البشرية | قسم `workforce` + widget `facility.workforce` |
| Import / Audit / Soft-delete / RowVersion | موجود للمنصات | Infrastructure | ResourceImportBatch، AuditLog | لا دفعات قوى بشرية | `WorkforceImportBatch` بنفس عقد Preview/Confirm |
| Demo seed staff | `dev-admin` فقط | DatabaseInitializer | هيكل البذور | لا موظفين تجريبيين | بذور محدودة لـ Facility A1 |

## قرار الهوية

**`User` يمثل حساب دخول وصلاحيات فقط.** لا يُوسَّع ليصبح ملفًا وظيفيًا كاملًا. الموظف التشغيلي هو `WorkforceMember`، وقد يرتبط اختياريًا بمستخدم عبر `UserId?`.

## ما يُعاد استخدامه من D.5 / D.4

- Facility-scoped API group تحت `/api/v1/facilities/{facilityId}/workforce/...`
- `*ReadinessPolicy` للحسابات والفجوات
- Import preview/confirm + idempotency indexes
- Soft-delete filters، RowVersion، Audit actions المسماة
- Widget provider + `FacilityWorkspaceReadService` feeds
- Permission separation عن `Workspaces.ViewFacility`
