# Phase 1B — عقد API الجديد

كل المسارات تحت `/api/v1/notes/{id}/...` (نفس `MapGroup` القائم في `ApiEndpoints.cs`)، بنفس اصطلاح minimal-API + `IValidator<T>.ValidateAndThrowAsync` + `.RequireAuthorization(AuthPolicies.X)` المتَّبع في بقية الملف.

## بوابة الفرز

| Route | Verb | Policy | Body → Response |
| --- | --- | --- | --- |
| `/triage/valid` | POST | `Notes.Update` | `{ rowVersion }` → `NoteDetailDto` |
| `/triage/propose-invalid` | POST | `Notes.ProposeInvalid` | `{ justificationAr, rowVersion }` → `NoteDecisionApprovalDto` |
| `/triage/propose-duplicate` | POST | `Notes.ProposeDuplicate` | `{ originalNoteId, justificationAr, rowVersion }` → `NoteDecisionApprovalDto` |

## نتيجة المعالجة

| Route | Verb | Policy | Body → Response |
| --- | --- | --- | --- |
| `/treatment/result` | POST | `Notes.StartWork` | `{ treatmentResultText, executionType, rowVersion }` → `NoteDetailDto` |
| `/treatment/propose-no-action` | POST | `Notes.ProposeNoAction` | `{ justificationAr, rowVersion }` → `NoteDecisionApprovalDto` |

## اعتماد موحَّد (Four-eyes)

| Route | Verb | Policy | Body → Response |
| --- | --- | --- | --- |
| `/decisions` | GET | `Notes.View` | → `NoteDecisionApprovalDto[]` |
| `/decisions/{approvalId}/approve` | POST | `Notes.ApproveAnyDecision`* | `{ reviewReason?, rowVersion }` → `NoteDetailDto` |
| `/decisions/{approvalId}/return` | POST | `Notes.ApproveAnyDecision`* | `{ reviewReason, rowVersion }` → `NoteDetailDto` |

\* سياسة مركَّبة جديدة (`AnyPermissionRequirement`، نفس نمط `AuthPolicies.FormsViewResponseDetail` القائم) تتطلب **أي واحدة** من `Notes.ApproveInvalid`/`Notes.ApproveDuplicate`/`Notes.ApproveNoAction` كبوابة أولى خشنة؛ الصلاحية الدقيقة الفعلية للـ`DecisionType` تُفرَض داخل `NoteDecisionApprovalService.EnsurePermissionForType`.

## القطع والمواد

| Route | Verb | Policy | Body → Response |
| --- | --- | --- | --- |
| `/parts` | GET | `Notes.View` | → `NotePartsRequirementDto[]` |
| `/parts` | POST | `Notes.StartWork` | `AddPartsRequirementRequest` → `NotePartsRequirementDto` (201) |
| `/parts/{itemId}` | PUT | `Notes.StartWork` | `UpdatePartsRequirementRequest` → `NotePartsRequirementDto` |
| `/parts/{itemId}` | DELETE | `Notes.StartWork` | — → 204 |
| `/parts/{itemId}/status` | POST | `Notes.StartWork` | `{ status, rowVersion }` → `NotePartsRequirementDto` |
| `/parts/{itemId}/cancel` | POST | `Notes.StartWork` | `{ reason, rowVersion }` → `NotePartsRequirementDto` |

## SLA الثلاثي

| Route | Verb | Policy | Body → Response |
| --- | --- | --- | --- |
| `/sla` | GET | `Notes.View` | → `NoteSlaStateDto` |
| `/sla/request-pause` | POST | `Notes.StartWork` | `RequestSlaPauseRequest` → `NoteSlaStateDto` |
| `/sla/pauses/{pauseId}/approve` | POST | `Notes.ApproveSlaPause` | `WorkflowActionRequest` (`{ reason?, rowVersion }`) → `NoteSlaStateDto` |

## توسيع العقود القائمة (بلا Breaking Change)

- `NoteDetailDto` (`GET /notes/{id}`, وضمن `NoteWorkspaceDetailDto.Note`): 15 حقلًا إضافيًا في **نهاية** السجل الموضعي (`TriageOutcome`…`NoteTypeSupportsPartsWorkflow`) — إضافة صرفة، الحقول القديمة بنفس الترتيب والاسم.
- `NoteWorkspaceDetailDto` (`GET /notes/{id}/workspace`): 4 حقول جديدة مضافة في النهاية: `DecisionApprovals`, `PartsRequirements`, `Sla`, `ActionCenter`.
- `NoteWorkspaceSummaryDto.WaitingClosureApproval`: كان دائمًا `false` صوريًا (`phase1a-observation-implementation-gap.md`)؛ أصبح الآن قيمة حقيقية (`pendingDecision is not null`).
- `AllowedActions` (نص Enum-like مصفوفة نصوص): 8 Token جديدة **مضافة فقط**، بلا تغيير في شروط الـ10 القديمة (باستثناء `VERIFY_CLOSURE` الذي أُعيد تثبيته صراحة على `Status==PendingVerification` بدل `CanTransition(status, Closed)` العام — راجع الشرح في الكود وفي `state-mapping.md`): `TRIAGE_VALID`, `TRIAGE_PROPOSE_INVALID`, `TRIAGE_PROPOSE_DUPLICATE`, `RECORD_TREATMENT`, `PROPOSE_NO_ACTION`, `MANAGE_PARTS`, `REQUEST_SLA_PAUSE`, `APPROVE_SLA_PAUSE`.

## `NoteActionCenterDto` (مركز الإجراءات، محسوب بالكامل من الخادم)

```csharp
record NoteActionCenterDto(
    IReadOnlyList<string> AllowedActions,
    string? PrimaryAction,
    IReadOnlyList<string> SecondaryActions,   // بحد أقصى 3 (مقصوص من قِبل الخادم)
    string? PendingDecision,
    bool DecisionApprovalRequired,
    bool CanSelfApprove,                      // "هل يمكن للمستخدم الحالي اعتماد القرار المعلَّق؟" — راجع التوضيح في الكود
    string? Blocker,
    string? NextAction,
    string ClosureReasonToken,
    NotePartsProgressDto? PartsProgress,
    bool ClosureReadiness);
```

## `NoteSlaStateDto` (وحدات: ثوانٍ `double`، وليس `TimeSpan` لتفادي تعقيد تسلسل System.Text.Json)

```csharp
record NoteSlaStateDto(
    double OverallAgeSeconds,
    double ProcessingSlaSeconds,
    double ExternalWaitDurationSeconds,
    bool IsProcessingSlaPaused,
    Guid? ActivePauseId,
    DateTimeOffset? ActivePauseStartedAtUtc,
    string? ActivePauseReason,
    DateTimeOffset? ActivePauseReviewDueAtUtc);
```

## Frontend (`src/frontend/src/api/client.ts`)

كل الأنواع والدوال أعلاه لها مقابل حرفي في `api.notes.*` (انظر إضافات `client.ts`: `triageValid`, `triageProposeInvalid`, `triageProposeDuplicate`, `recordTreatment`, `proposeNoAction`, `decisions`, `approveDecision`, `returnDecision`, `parts`, `addPart`, `updatePart`, `deletePart`, `updatePartStatus`, `cancelPart`, `slaState`, `requestSlaPause`, `approveSlaPause`) — بلا حقل واحد مخترَع لا يقابله حقل خلفي فعلي.
