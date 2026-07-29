# Phase 1B — العمارة الفعلية

## فصل الطبقتين (المطلب المحوري في التكليف)

```
الطبقة الأولى: قرار صحة الملاحظة (قرار فرز الملاحظة)
  → OperationalNote.TriageOutcome (Valid/Invalid/Duplicate), TriageDecidedAtUtc/ByUserId
  → NoteTriageService

الطبقة الثانية: نتيجة المعالجة
  → OperationalNote.TreatmentResultType/TreatmentExecutionType/TreatmentResultText/NoActionJustificationAr
  → NoteTreatmentService
```

لا حقل ولا Endpoint مشترك بين الطبقتين. الحقل `TriageOutcome` لا يظهر أبدًا ضمن أي قائمة "نتيجة معالجة"، والعكس. تحقَّق منه اختبار الواجهة `shows the treatment-result section only after triage is approved Valid, and it excludes invalid/duplicate/assign/escalate`.

## الخدمات الجديدة (`src/backend/Baseera.Application/Notes/`)

| الملف | المسؤولية |
| --- | --- |
| `NoteDecisionServices.cs` | `NoteTriageService` (قرار صحيحة/اقتراح غير صحيحة/اقتراح مكررة) + `NoteEvidencePolicy` (سياسة الأدلة server-authored حسب الخطورة) + `NoteDecisionApprovalMapper` (تعيين مشترك) |
| `NoteDecisionApprovalService.cs` | محرك Four-eyes موحَّد لأنواع القرار الثلاثة (Invalid/Duplicate/NoAction) — اعتماد/إعادة، إغلاق النوع المناسب |
| `NoteTreatmentService.cs` | تسجيل نتيجة المعالجة + اقتراح لا تتطلب إجراء |
| `NotePartsRequirementService.cs` | CRUD + حالة مستقلة لكل عنصر قطعة + حساب التقدم |
| `NoteSlaService.cs` | حساب الساعات الثلاث + طلب/اعتماد تجميد SLA |

## قرار تصميمي محوري: لماذا لا يوجد `TreatmentResultApproval` كسجل `NoteDecisionApproval` منفصل

التكليف يذكر نموذجًا لأنواع اعتماد تشمل `TreatmentResultApproval`. القرار الفعلي: اعتماد "معالجة" (Treated) **يبقى يمر عبر خط الأنابيب القائم** `NotesSubmitForVerification → NotesVerifyClosure` (`NoteWorkflowService`)، وليس عبر `NoteDecisionApprovalService`. السبب: هذا الخط موجود مسبقًا، مُختبَر بعمق (SoD الحرجة، منع الإجراءات التصحيحية المفتوحة، RowVersion)، وإعادة بنائه كسجل `NoteDecisionApproval` مواز كانت ستُنتج **منطقين متوازيين لنفس الهدف** — بالضبط ما يحظره التكليف صراحة ("لا تنشئ منطقًا موازيًا لـ State Machine أو الصلاحيات القائمة"). بدلًا من ذلك: وُسِّع `EnforceCriticalSoDAsync` (كان يعمل فقط عند `NoteSeverity.Critical`) ليعمل أيضًا عند `TreatmentResultType==Treated` بأي خطورة — إضافة شرط واحد `OR`، لا خدمة جديدة. هذا التغيير **Inert تمامًا** على أي ملاحظة/اختبار سابق لهذه الدفعة (الحقل الجديد يبقى `null` ما لم تُستدعَ نقاط النهاية الجديدة صراحة) — مؤكَّد بنجاح كل الاختبارات الـ34 القديمة + 78 اختبارًا آخر من نطاق Operations دون أي تعديل على توقّعاتها الجوهرية (3 اختبارات فقط احتاجت إضافة خطوتَي "فرز صحيحة" و"تسجيل نتيجة معالجة" لأن السلوك الجديد يتطلبهما فعليًا الآن — موثَّق في `phase1b-observation-test-matrix.md`).

## `NoteStateMachine`: إضافة، لا استبدال

راجع `phase1b-observation-state-mapping.md`. ثلاث حالات `(Open|Assigned|InProgress, Closed)` أُضيفت إلى **نفس** الجدول الثابت الوحيد. لا `NoteStateMachineV2`، لا `if` موازٍ خارج هذا الملف.

## `NoteType.SupportsPartsWorkflow`: بوابة Server-authored حقيقية

عمود `bit` جديد على `NoteTypes`. الواجهة **لا تقارن نصًا عربيًا ولا Code** لإظهار خيار "تتطلب قطع أو مواد" — تعتمد حصرًا على `note.noteTypeSupportsPartsWorkflow` القادم من الخادم (`NoteDetailDto.NoteTypeSupportsPartsWorkflow`، محسوب من `note.NoteType.SupportsPartsWorkflow` في `NoteQueryService.GetDetailAsync`). مفعَّل حاليًا لنوعي `TECHNICAL`/`OPERATIONAL` فقط (Catalog الفعلي به 6 أنواع، لا تطابق حرفي لكل من "صيانة/مرافق/تجهيزات" المذكورة في التكليف كأمثلة توضيحية).

## `PartsRequirement[]`: كيان فرعي حقيقي، لا State Machine لكل قطعة

`NotePartsRequirement` كيان EF مستقل (`NotePartsRequirements` جدول)، بحالة محدودة (`NotePartsRequirementStatus` — 6 قيم، انتقال أمامي فقط + إلغاء صريح بسبب)، لا Domain Note منفصلة لكل قطعة، ولا Machine حالة خاصة بها — دوال تحقق ثابتة (`EnsureForwardTransition`) بدل رسم بياني حالات كامل.

## `NoteSlaPausePeriod`: سجل تاريخي Append-mostly

فترة تجميد واحدة نشطة كحد أقصى لكل ملاحظة (فهرس فريد مُفلتَر `EndedAtUtc IS NULL`). `StartedAtUtc` يُسجَّل عند **الاعتماد** فقط (لا عند الطلب) — تطبيق حرفي لبند "تسجيل وقت بداية التجميد" بعد اعتماد مخوَّل. لا تُعدَّل الفترات المنتهية عند إعادة الحساب (`NoteSlaService.Compute` دالة نقية بلا كتابة).

## فجوة موثَّقة: نهاية تجميد SLA التلقائية عند تجاوز `ReviewDueAtUtc`

منفَّذ تلقائيًا: إنهاء التجميد عند اكتمال/إلغاء كل القطع الفعالة (`NoteSlaService.EndPauseIfPartsResolvedAsync`، يُستدعى من `NotePartsRequirementService` عند كل تغيير حالة/إلغاء). **غير منفَّذ**: إنهاء قسري تلقائي عند تجاوز `ReviewDueAtUtc` دون تمديد (يتطلب مهمة خلفية دورية، خارج نطاق الوقت المتاح لهذه الدفعة) — الحقل يبقى ظاهرًا في `NoteSlaStateDto.ActivePauseReviewDueAtUtc` للتقارير/الواجهة لعرضه كمؤشر، لا كإجراء تلقائي. مسجَّل `Partial` في سجل الامتثال.
