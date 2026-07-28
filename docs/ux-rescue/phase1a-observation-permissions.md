# Phase 1A — الصلاحيات

## لا صلاحيات جديدة

صفر أكواد صلاحية جديدة، صفر أدوار جديدة، صفر Migration. كل ما استُخدِم في هذه الدفعة موجود مسبقًا ومُوثَّق في `docs/permissions-matrix.md` (قسم "صلاحيات الملاحظات التشغيلية" + الإضافة الجديدة أسفله في نفس الملف: "UX Rescue Phase 1A — لا صلاحيات جديدة").

## القدرات المميَّزة في الواجهة (وصلاحياتها الفعلية)

| القدرة | الصلاحية المطلوبة | أين تُتحقَّق فعليًا |
| --- | --- | --- |
| عرض قائمة/تفاصيل الملاحظة في Workspace | `Notes.View` | `AuthPolicies.NotesView` على الـEndpoint + `usePermission('Notes.View')` في الواجهة (بوابة عرض، ليست مصدر الحقيقة) |
| إنشاء ملاحظة (من الـWorkspace أو من Facility Workspace) | `Notes.Create` | `AuthPolicies.NotesCreate` على `POST /notes` — الخادم هو الحَكَم الوحيد |
| Assign / Reassign Inline | `Notes.Assign` | `AuthPolicies.NotesAssign` + `NoteTypeAccessService` (قدرة `Assign` لنوع الملاحظة) |
| StartWork / RequestVerification / RejectVerification / Reopen / Cancel Inline | صلاحياتها المقابلة (`Notes.StartWork` إلخ) | كما في الجدول الأصلي، بلا تغيير |
| VerifyClosure Inline (نموذج ملخص الإغلاق) | `Notes.VerifyClosure` | `AuthPolicies.NotesVerifyClosure` + فصل الواجبات (SoD) الموجود مسبقًا في `NoteWorkflowService` — لم يتغيّر أي منطق SoD في هذه الدفعة |
| رفع مرفق Inline من قسم "الأدلة" | `Attachments.Upload` | نفس الصلاحية المستخدَمة في كل مسارات رفع المرفقات الأخرى بالتطبيق |
| زر "فتح ملاحظة" داخل Facility Workspace | `Notes.Create` | يظهر فقط عند توفرها (`usePermission('Notes.Create')` داخل `CommandHeader`)؛ الخادم يعيد التحقق بشكل مستقل عند الإرسال الفعلي |

## قاعدة صريحة: Facility Workspace لا يوسّع صلاحيات الملاحظات

الدخول إلى `workspaces/facilities/:facilityId` يتطلب `Workspaces.View` + `Workspaces.ViewFacility`، وهما صلاحيتان منفصلتان تمامًا عن `Notes.*`. مستخدم يملك وصول Facility Workspace لكن بلا `Notes.Create` **لن يرى** زر "فتح ملاحظة" إطلاقًا (مؤكَّد باختبار)، ولو تجاوز الواجهة (طلب مباشر لـ`POST /api/v1/notes`) سيُرفَض بـ403 من الخادم بغضّ النظر عن كونه داخل سياق سجن معيَّن أم لا.

## أمان وراثة السجن/الوحدة (وليس مجرد UX)

`NoteScopeService.ResolveIntakeAsync(userId, requestedRegionId, requestedFacilityId, ct)` — موجودة مسبقًا، غير مُعدَّلة في هذه الدفعة — تتحقق من: وجود السجن ونشاطه، انتماء السجن للمنطقة المطلوبة، `orgScope.CanAccess` (نطاق المستخدم التنظيمي)، وقفل `UserNoteIntakeProfile` إن وُجد (مستخدم مقفَل على منطقة/سجن لا يمكنه تجاوزه حتى لو أرسل قيمة أخرى). أي `FacilityId`/`FacilityUnitId` يصل من الواجهة (سواء من `NoteCreatePanel` داخل Facility Workspace أو من `NoteCreatePage` القديمة) هو **حالة عرض (Presentation state)**، لا مصدر تفويض — اختبارات التكامل الجديدة (`Create_rejects_client_supplied_facility_outside_the_callers_scope_over_http`) تثبت الرفض الفعلي (403) عند محاولة تلاعب.

## لا تغيير في `NoteStateMachine`

كل انتقالات الحالة (`Draft→Open→...→Closed→Reopened`) بقيت كما هي حرفيًا. الإضافة الوحيدة (`VERIFY_CLOSURE` ضمن `AllowedActions`) هي تصحيح **حساب** الإجراءات المسموحة، لا تغيير في **قواعد** الانتقال نفسها — الانتقال (`PendingVerification → Closed`) وصلاحيته (`Notes.VerifyClosure`) كانا موجودَين ومفعَّلين مسبقًا في `NoteWorkflowService.VerifyClosureAsync`؛ الفجوة كانت فقط أن الواجهة لم تكن تعرف أن الزر يجب أن يظهر.
