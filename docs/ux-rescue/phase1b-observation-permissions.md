# Phase 1B — الصلاحيات وفصل الأدوار

## صلاحيات جديدة (7 فقط، بالاسم المطلوب حرفيًا في التكليف)

```
Notes.ProposeInvalid      Notes.ApproveInvalid
Notes.ProposeDuplicate    Notes.ApproveDuplicate
Notes.ProposeNoAction     Notes.ApproveNoAction
Notes.ApproveSlaPause
```

لا صلاحية جديدة لقرار "صحيحة" نفسه ولا لبوابة الفرز عمومًا — التكليف لم يسمِّ صلاحية جديدة لها، فأُعيد استخدام `Notes.Update` القائمة (يملكها كل من يستطيع أصلًا تحديث بيانات الملاحظة، وهي أدنى تفويض معقول لأول قرار في دورة الحياة). موثَّق صراحة كي لا يُفهَم كسهو.

## Mapping التصنيفات المفاهيمية الواردة في التكليف

النص يشير إلى `Notes.Process`/`Notes.Assign`/`Notes.Close` كأسماء مفاهيمية عامة. في هذا الكود الفعلي:

| اسم مفاهيمي في التكليف | الصلاحية الفعلية في الكود |
| --- | --- |
| `Notes.Process` | `Notes.StartWork` + `Notes.SubmitForVerification` |
| `Notes.Assign` | `Notes.Assign` (موجودة بنفس الاسم) |
| `Notes.Close` | `Notes.VerifyClosure` |

القاعدة "لا تمنح `Notes.ApproveInvalid` تلقائيًا لمن يملك `Notes.Process`/`Notes.Assign`/`Notes.Close`" مُطبَّقة بنيويًا: الصلاحيات السبع الجديدة أكواد منفصلة تمامًا في `PermissionCodes`/`AuthPolicies`، لا تُشتَق ولا تُستنتَج من أي صلاحية أخرى.

## توزيع الأدوار (`DatabaseInitializer.cs`)

| الدور | Propose* | Approve* (+ApproveSlaPause) | لماذا |
| --- | --- | --- | --- |
| `RegionalCoordinator` | ✓ | ✗ | يملك `StartWork`/`SubmitForVerification` (طبقة معالجة)، لا يملك `VerifyClosure` أصلًا |
| `RegionalDirector` | ✓ | ✓ | يملك `VerifyClosure` أصلًا (طبقة اعتماد) |
| `FacilityDirector` | ✓ | ✓ | نفس المنطق |
| `DecisionSupportDirector` | ✓ | ✓ | نفس المنطق |
| `HeadquartersExecutive` | ✗ | ✓ | رقابي بحت، لا معالجة مباشرة |
| `SystemAdministrator` | ✓ (كل الصلاحيات) | ✓ (كل الصلاحيات) | كما في كل صلاحيات النظام؛ **Four-eyes لا يُستثنى حتى لهذا الدور** — الفحص على مستوى السجل (`ProposedByUserId != ReviewedByUserId`) لا على مستوى الدور |

**السماح لنفس الدور بحمل Propose+Approve معًا مقصود**: فصل الواجبات في هذا النظام مُنفَّذ **على مستوى السجل الواحد** (شخص بعينه لا يعتمد اقتراحه هو)، وليس بفصل الأدوار — نفس النمط المتَّبع مسبقًا في `NoteWorkflowService.EnforceCriticalSoDAsync` للملاحظات الحرجة (`docs/permissions-matrix.md` §Critical SoD). فصل الأدوار وحده كان سيمنع سيناريوهات مشروعة (منسّق إقليمي يقترح، مدير سجن آخر يعتمد) دون أن يضيف حماية فعلية.

## قواعد Four-eyes المُنفَّذة (`NoteDecisionApprovalService`, `NoteSlaService`, `NoteWorkflowService`)

- `ProposedByUserId == ReviewedByUserId` → `409 Conflict` صريح (`InvalidOperationException`)، مهما كانت الأدوار.
- لا يُسمح بطلبَي اعتماد Pending من نفس النوع على نفس الملاحظة في آنٍ واحد (فهرس فريد مُفلتَر `IsUnique + HasFilter("[Status] = 0")` على `NoteDecisionApprovals`).
- الإعادة (`Return`) تتطلب سببًا إلزاميًا؛ التحقق مزدوج (FluentValidation + الخدمة).
- تجميد SLA: منشئ الطلب لا يعتمد تجميده الخاص (`RequestedByUserId != ApprovedByUserId`)، مُنفَّذ في `NoteSlaService.ApprovePauseAsync` ومختبَر في `NotePhase1BServicesTests.Sla_pause_approval_rejects_self_approval...` و`NotePhase1BIntegrationTests.Sla_pause_request_and_approval_pauses_processing_clock...`.
- **اعتماد "معالجة" (Treated) لا يمر عبر `NoteDecisionApproval` إطلاقًا** — بل عبر خط الأنابيب القائم `SubmitForVerification→VerifyClosure` نفسه، مع توسيع شرط `EnforceCriticalSoDAsync` (كان محصورًا بـ`NoteSeverity.Critical`) ليشمل أيضًا `TreatmentResultType==Treated` بأي خطورة. هذا القرار التصميمي موثَّق في `phase1b-observation-architecture.md` §مسارات الاعتماد؛ خيار مقصود لتفادي بناء مسار اعتماد مواز فوق State Machine قائمة، وهو Inert تمامًا على أي ملاحظة/اختبار سابق لهذه الدفعة (الحقل يبقى `null` ما لم تُستدعَ نقاط النهاية الجديدة).

## فجوة موثَّقة (غير مُغلَقة بالكامل، Partial)

لا يوجد حاجز خادم صارم يمنع `Assign` قبل اعتماد "صحيحة" (`TriageOutcome=Valid`) — الإنفاذ حاليًا على مستوى Action Center/الواجهة فقط (`ASSIGN`/`START_WORK` لا تُصبح `PrimaryAction` قبل الفرز، لكن الاستدعاء المباشر لـ`POST /notes/{id}/assign` يبقى ممكنًا تقنيًا). سبب هذا القرار: إضافة حاجز صارم في `NoteAssignmentService.AssignAsync` كانت ستكسر عشرات اختبارات التكامل/الوحدة القائمة مسبقًا التي تُنشئ→تُسنِد الملاحظة مباشرة دون فرز (لأن الفرز مفهوم جديد كليًا في هذه الدفعة). مسجَّل صراحة في `phase1b-observation-compliance-ledger.md` كبند `Partial` مع توصية متابعة.
