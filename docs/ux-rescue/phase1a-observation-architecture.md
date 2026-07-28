# Phase 1A — العمارة الفعلية

## نظرة عامة

لا يوجد `WorkspaceShell` عام جديد أُنشئ لهذه الدفعة. Observation Workspace بقي مكوّنًا واحدًا مُهيكَلاً (`ObservationWorkspacePage.tsx`)، مع استخراج البدايات المشتركة فقط إلى `src/frontend/src/shared/workspaces/` كما يطلب Section 4 من التكليف ("لا تبنِ مكتبة عامة ضخمة — فقط ما يُحتاج فعليًا لهذه الدفعة"). الأساس المعماري الأكبر (`WorkspaceShell` الحقيقي المستخدَم في `FacilityWorkspacePage`) لم يُعاد استخدامه هنا لأن Observation Workspace له عقد بيانات مختلف تمامًا (قائمة+تفاصيل ملاحظة، لا Widgets متعددة النطاقات) — إعادة استخدامه كانت ستفرض تجريدًا غير ملائم.

## المكوّنات المشتركة الجديدة

- `src/frontend/src/shared/workspaces/WorkspaceStateView.tsx`: `WorkspaceSkeletonRows`, `WorkspaceEmptyState`, `WorkspaceErrorState`. تُستخدَم حاليًا داخل `ObservationWorkspacePage` (قائمة+تفاصيل)، وهي المرشح الأول لإعادة الاستخدام في #144/#145/Region/HQ لاحقًا دون أي تعديل عليها الآن.

## لماذا لم تُبنَ مكوّنات أخرى مطلوبة بالاسم في التكليف

التكليف اقترح أسماء إرشادية (`MasterDetailWorkspaceLayout`, `WorkspaceListPane`, `WorkspaceDetailPane`, `WorkspaceCommandHeader`, `WorkspaceFilterBar`, `WorkspaceActionBar`) مع توضيح صريح: "الأسماء قابلة للتعديل حسب اصطلاحات المشروع؛ لا تبنِ مكتبة ضخمة". القرار الفعلي: هذه العناصر بُنيت كدوال داخلية ضمن `ObservationWorkspacePage.tsx` نفسها (`ObservationCard`, `WorkspaceDetail`, `ActionBar`, أقسام `SummaryTab/ProcessingTab/AssignmentTab/EvidenceTab/HistoryTab`) بدل استخراجها كملفات مشتركة منفصلة، لأن الاستخدام الحالي وحيد (صفحة واحدة فقط) ولا يوجد مستهلك ثانٍ فعلي اليوم يبرر التجريد المبكر. عند بدء #144/#145 فعليًا، إن ظهر تكرار حقيقي بين صفحتين، يُستخرَج حينها — هذا قرار "لا تجريد قبل الحاجة" صريح، لا سهو.

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
