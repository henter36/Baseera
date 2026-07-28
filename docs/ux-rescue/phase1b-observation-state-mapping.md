# Phase 1B — تخطيط الحالات التشغيلية (State Mapping)

## المبدأ

`NoteStatus` (Domain enum) بقي كما هو حرفيًا في قيمه القديمة (`Draft…Cancelled`)، مع **إضافتين فقط** على جدول الانتقالات في `NoteStateMachine.CanTransition` (لا Enum جديد، لا State Machine موازية):

```
(Open, Closed)        // إغلاق مباشر بعد اعتماد قرار غير صحيحة/مكررة/لا تتطلب إجراء
(Assigned, Closed)
(InProgress, Closed)
```

كل الحالات الخمسة عشر المطلوبة في التكليف تُمثَّل بتركيب `Status` + حقول إضافية على `OperationalNote`، لا بتضخيم Enum:

| الحالة المطلوبة | Status | TriageOutcome | TreatmentResultType | ملاحظات |
| --- | --- | --- | --- | --- |
| بانتظار الفرز | Open | `null` | — | حالة افتراضية بعد `Submit` |
| بانتظار اعتماد غير صحيحة | Open/Assigned/InProgress | `Invalid` | — | + `NoteDecisionApproval(Invalid, Pending)` |
| بانتظار اعتماد التكرار | Open/Assigned/InProgress | `Duplicate` | — | + `NoteDecisionApproval(Duplicate, Pending)` + `OriginalNoteId` مرشَّح |
| صحيحة — بانتظار التكليف | Open | `Valid` | `null` | لا تكليف حالي بعد |
| قيد المعالجة | InProgress | `Valid` | أي | — |
| بانتظار قطع | InProgress | `Valid` | `Treated` | `TreatmentExecutionType=RequiresParts` و`PartsProgress.AllResolved=false` |
| بانتظار تحقق المعالجة | PendingVerification | `Valid` | `Treated` | مسار `SubmitForVerification`/`VerifyClosure` القائم دون تغيير |
| معادة للمعالجة | InProgress | `Valid` | `Treated` | بعد `ReturnForRework` (بلا تغيير) |
| بانتظار اعتماد لا تتطلب إجراء | أي غير نهائي | `Valid` | `NoActionRequired` | + `NoteDecisionApproval(NoAction, Pending)` |
| جاهزة للإغلاق | PendingVerification أو أي حالة بها اعتماد Pending | — | — | `ActionCenter.ClosureReadiness=true` |
| مغلقة — تمت المعالجة | Closed | `Valid` | `Treated` | `ClosureReason=Treated` (عبر `VerifyClosure` القائم) |
| مغلقة — غير صحيحة | Closed | `Invalid` | — | `ClosureReason=Invalid` (عبر `NoteDecisionApprovalService`) |
| مغلقة — مكررة | Closed | `Duplicate` | — | `ClosureReason=Duplicate` + `DuplicateOfNoteId` |
| مغلقة — لا تتطلب إجراء | Closed | `Valid` | `NoActionRequired` | `ClosureReason=NoActionRequired` |
| معادة الفتح | Reopened | يُصفَّر عند إعادة القرار فقط | — | لا تغيير عن السلوك القائم |

`ClosureReason` (enum جديد: `Treated/Invalid/Duplicate/NoActionRequired`) يمثّل **سبب الإغلاق النهائي**، منفصل تمامًا عن `Status=Closed` — لا يستبدله ولا يوسّعه.

## Progress / Blocker / NextAction / PendingDecision / SlaState / PartsProgress / ClosureReadiness

هذه الحقول محسوبة (لا مخزَّنة) في `NoteWorkspaceQueryService.BuildActionCenter` و`NoteSlaService.Compute`، وتُعاد ضمن `NoteActionCenterDto`/`NoteSlaStateDto`/`NotePartsProgressDto` — راجع `phase1b-observation-api-contract.md`.

## قرار تصميمي: لماذا لم تُضاف حالات NoteStatus جديدة

التكليف يطلب صراحة: *"استخدم الـState Machine القائم مع Mapping واضح، دون إضافة حالات غير لازمة"*. توسيع `NoteStatus` بـ15 قيمة جديدة كان سيُغرق كل الكود القائم (فرز/تقارير/فلاتر/صلاحيات) الذي يتعامل مع 8 قيم فقط، ويكسر قاعدة "لا منطق مواز". التركيب أعلاه يحقق نفس التعبير الدلالي بأثر أصغر بكثير.

## أثر إضافة `(Open/Assigned/InProgress, Closed)`

انتقالات الإغلاق الثلاثة الجديدة **لا** تُستخدَم إطلاقًا عبر مسار `VerifyClosure` (الذي يبقى محصورًا فعليًا بـ`PendingVerification→Closed` عبر حارس `VERIFY_CLOSURE` في `ComputeAllowedActions`؛ راجع التعليق في `NoteWorkspaceQueryService.cs`). إغلاقها الوحيد الفعلي هو عبر `NoteDecisionApprovalService.ApproveAsync` بعد اعتماد Four-eyes مستقل. تأكيد ذلك موثَّق باختبارات `NoteStateMachineTests` و`NotePhase1BIntegrationTests`.
