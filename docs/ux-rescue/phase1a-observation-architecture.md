# Phase 1A — العمارة الفعلية

## Phase 1A.1 corrective update

Phase 1A.1 صححت نمط عرض تفاصيل الملاحظة بعد PR #149: لم تعد ملاحظة Facility Workspace اليومية تُفتح داخل `CommandContextPanel`/`dialog`. أي عنصر ملاحظة من مساحة السجن أو رابط legacy بشكل `panel=note&entityId=...` ينتقل الآن إلى `/notes/workspace?facilityId=...&noteId=...&source=facility:...`، حيث تُعرض التفاصيل داخل `ObservationMasterDetailLayout` كجزء من الصفحة.

المكونات المستخرجة في هذه الدفعة:

- `src/frontend/src/pages/notes/workspace/ObservationWorkspaceHeader.tsx`
- `src/frontend/src/pages/notes/workspace/ObservationMasterDetailLayout.tsx`

هذا تصحيح تخطيط وربط فقط؛ لا يزيل Feature Flag ولا يبدأ Phase 1B.

## نظرة عامة

لا يوجد `WorkspaceShell` عام جديد أُنشئ لهذه الدفعة. Observation Workspace بقي قائمًا على عقد قائمة+تفاصيل ملاحظة، لا Widgets متعددة النطاقات. بعد Phase 1A.1 استُخرجت ترويسة وتخطيط master-detail محليان تحت `pages/notes/workspace/` بدل إبقاء كامل التخطيط في ملف الصفحة.

## المكوّنات المشتركة الجديدة

- `src/frontend/src/shared/workspaces/WorkspaceStateView.tsx`: `WorkspaceSkeletonRows`, `WorkspaceEmptyState`, `WorkspaceErrorState`. تُستخدَم حاليًا داخل `ObservationWorkspacePage` (قائمة+تفاصيل)، وهي المرشح الأول لإعادة الاستخدام في #144/#145/Region/HQ لاحقًا دون أي تعديل عليها الآن.

## حدود الاستخراج

التكليف اقترح أسماء إرشادية (`MasterDetailWorkspaceLayout`, `WorkspaceListPane`, `WorkspaceDetailPane`, `WorkspaceCommandHeader`, `WorkspaceFilterBar`, `WorkspaceActionBar`) مع توضيح صريح: "الأسماء قابلة للتعديل حسب اصطلاحات المشروع؛ لا تبنِ مكتبة ضخمة". القرار بعد Phase 1A.1: استخراج الترويسة وتخطيط master-detail فقط لأنهما موضع المشكلة. بقيت `ActionBar` والأقسام داخل `ObservationWorkspacePage.tsx` لأنها ما زالت مرتبطة بعقد الملاحظة الحالي ولا يوجد مستهلك ثانٍ.

`WorkspaceFilterBar` الموجود فعليًا في `src/frontend/src/workspaces/WorkspaceShell.tsx` (يُستخدَم في Facility Workspace) لم يُعاد استخدامه في Observation Workspace لأن شكل فلاتره (`fromUtc/toUtc` زمنية) لا يطابق فلاتر الملاحظات (حالة/خطورة/سجن/وحدة/نوع/بحث) — إعادة استخدامه كانت ستفرض واجهة غير ملائمة على البيانات.

## تدفق البيانات

```
ObservationWorkspacePage
├─ useSearchParams() ←→ حالة الفلاتر/القسم/التحديد (مصدر الحقيقة الوحيد، لا state مكرر)
├─ useQuery(['notes-workspace', filters])       → GET /api/v1/notes/workspace
├─ useQuery(['notes-workspace-detail', noteId]) → GET /api/v1/notes/{id}/workspace
├─ useQuery(['workspace-regions'|'facilities'|'facility-units'|'note-types']) → قوائم الفلاتر (Regions/Facilities/FacilityUnits/NoteTypes الموجودة مسبقًا في api/client.ts)
└─ ActionBar
   ├─ useMutation → api.notes.{submit,startWork,submitForVerification,returnForRework,reopen,cancel,verifyClosure,assign}
   └─ useQuery(['note-eligible-assignees']) → GET /api/v1/notes/{id}/eligible-assignees (عميل واجهة جديد لEndpoint موجود مسبقًا)
```

لا Global state إضافي (Redux/Zustand/إلخ) — التوجه نفسه المتّبع في بقية التطبيق (TanStack Query + URL كمصدر حقيقة).

## Facility Workspace: نقطة الدخول الجديدة

`NoteCreatePanel` (داخل `FacilityWorkspacePage.tsx`) يُفتَح عبر نمط `openPanel({ type: 'note-create', entityId })` الموجود مسبقًا لكل أنواع الـContext Panel الأخرى (33 نوعًا قبل هذه الدفعة، أصبحت 34). `entityId` هنا يحمل معرّف الوحدة عند الفتح من صفّ وحدة محدَّدة، أو Sentinel ثابت `'create'` عند الفتح العام من رأس الصفحة (نفس نمط الـSentinel المستخدَم مسبقًا في عناصر `WorkforceActionCenterItem`، مثل `'action:replacement'`) — ضروري لأن `panelFromSearch` يرفض `entityId` فارغًا كحالة "لا Panel مفتوح".

`facilityId`/`regionId` يُمرَّران كـProps من `FacilityWorkspacePage`/`shell.context`، لا يُقرَآن أبدًا من حقل اختيار في النموذج — لا يوجد Selector سجن في `NoteCreatePanel` إطلاقًا (مؤكَّد باختبار يبحث عن غيابه صراحة).

## Route resolution و feature flag

`src/frontend/src/pages/notes/observationWorkspaceFlag.ts` + `NotesRouteResolvers.tsx` — تفصيل كامل في `phase1a-observation-route-transition.md`.
