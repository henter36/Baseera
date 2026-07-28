# Phase 1A — عقد Observation Workspace (URL + Backend)

## معاملات URL على `/notes/workspace`

| المعامل | يقابل حقل الفلترة الخلفي | ملاحظات |
| --- | --- | --- |
| `search` | `search` | Debounce 300ms على الواجهة قبل الإرسال |
| `status` | `status` (رقم `NoteStatus`) | |
| `severity` | `severity` (رقم `NoteSeverity`) | |
| `noteType` | `noteTypeId` | اسم معامل URL مختلف عمدًا عن اسم الحقل الخلفي لتجنّب طول `noteTypeId=` في الرابط؛ لا فرق دلالي |
| `regionId` | `regionId` | يُصفّي قائمة السجون في الفلتر التالي |
| `facilityId` | `facilityId` | |
| `facilityUnitId` | `facilityUnitId` | يُصفّي حسب السجن المختار فقط |
| `due` | `overdueOnly` (`due=overdue`) أو `dueSoonDays=7` (`due=soon`) | قيمة واحدة تُحوَّل لحقلين خلفيين مختلفين — تبسيط متعمَّد للواجهة بدل Checkbox منفصل لكل حالة |
| `requiresMyAction` | `requiresMyAction` | |
| `requiresRouting` | `requiresRouting` | |
| `sort` | `sortBy` | يُحفَظ في الرابط باسم `sort` لكن الحالة الداخلية والطلب للخادم يستخدمان `sortBy` (توافق مع `NoteListFilters` الموجود) |
| `sortDesc` | `sortDesc` | فقط عندما تكون `false` (الافتراضي `true` لا يُكتَب في الرابط لتقصير الروابط) |
| `page` | `page` | فقط عندما > 1 |
| `noteId` | — | الملاحظة المفتوحة حاليًا؛ فتحها/إغلاقها لا يمسح أي فلتر آخر |
| `section` | — | القسم النشط داخل التفاصيل (`summary\|processing\|assignment\|evidence\|history`)؛ يُكتَب فقط عندما تكون هناك ملاحظة مفتوحة |
| `source` | — | من أين فُتحت الملاحظة؛ القيمة `facility:<facilityId>` تُظهر رابط عودة صريح لمساحة عمل السجن؛ `legacy-link` تشير لقادم من رابط `/notes/:id` قديم |
| `view` | — | `view=detail` عند التحميل الأولي يبدأ بطيّ لوحة القائمة (فائدة لروابط الجوال العميقة) |

**قواعد مُطبَّقة فعليًا (مؤكَّدة بالكود لا وصفًا فقط)**:
- فتح/إغلاق ملاحظة لا يمسح أي فلتر آخر (كلاهما state منفصل يُكتَب معًا في نفس `useEffect` دون حذف).
- تحديث الصفحة (Refresh) يستعيد نفس الحالة كاملة لأن `useSearchParams` هو مصدر الحقيقة الابتدائي الوحيد لكل state.
- لا بيانات حساسة في الرابط — كل القيم إما معرّفات (GUID) أو enum أرقام أو Booleans.
- قيم غير صالحة (مثال: `status=abc`) تُهمَل بصمت عبر `Number(...)` الذي يُنتج `NaN` فيُعامَل كـ`undefined` في المرشِّحات الفعلية المرسَلة للخادم — لا Route يُعرَض له خطأ واجهة.
- معاملات URL لا تُمكِّن المستخدم من تجاوز نطاقه: كل فلتر يُرسَل للخادم كـ"طلب"، والخادم (`INoteScopeService.FilterQueryableAsync`) هو من يقرر ما يُعاد فعليًا — تمرير `facilityId` خارج نطاق المستخدم في الرابط يعيد قائمة فارغة أو 404 على التفاصيل، لا بيانات متسربة.

## عقد Backend (`NoteWorkspaceDtos.cs`)

### `GET /api/v1/notes/workspace` → `NoteWorkspaceListDto`

```csharp
public sealed record NoteWorkspaceListDto(PagedResult<NoteListItemDto> Notes);
```

بلا تغيير بنيوي في هذه الدفعة (كان موجودًا مسبقًا وناضجًا) عدا تثبيت حد الصفحة الأقصى `Math.Clamp(query.PageSize, 1, 50)`.

### `GET /api/v1/notes/{id}/workspace` → `NoteWorkspaceDetailDto` (مُعدَّل في هذه الدفعة)

```csharp
public sealed record NoteWorkspaceDetailDto(
    NoteDetailDto Note,
    IReadOnlyList<string> AllowedActions,
    NoteWorkspaceSummaryDto Summary,
    IReadOnlyList<NoteAssignmentDto> Assignments,
    PagedResult<CorrectiveActionListItemDto> CorrectiveActions,
    IReadOnlyList<AttachmentDto> Attachments,
    IReadOnlyList<NoteWorkspaceTimelineEntryDto> Timeline);
```

**التغيير الوحيد على الشكل**: حُذفت حقول `Resources`, `Decisions`, `Links` نهائيًا (كانت دائمًا `Array.Empty<>()` بلا Domain خلفها — راجع `phase1a-observation-implementation-gap.md`). لا حقول جديدة أُضيفت.

### `AllowedActions` — القيم الممكنة (بعد إضافة `VERIFY_CLOSURE`)

`SUBMIT, ASSIGN, REASSIGN, START_WORK, ADD_ACTION, REQUEST_VERIFICATION, REJECT_VERIFICATION, VERIFY_CLOSURE, REOPEN, CANCEL`

القاعدة: `NoteWorkspaceQueryService.ComputeAllowedActions(NoteDetailDto note, ICurrentUser currentUser)` — دالة Static نقية (بلا I/O)، تتحقق من `currentUser.HasPermission(...)` **و** `NoteStateMachine.CanTransition(note.Status, target)` معًا. الواجهة الأمامية لا تخمّن أي إجراء من الحالة وحدها — تعرض فقط ما يُرجعه الخادم حرفيًا، وتختار أول عنصر كـ"Primary" والباقي "Secondary" (ترتيب القائمة نفسه من الخادم يعكس أولوية منطقية: SUBMIT قبل ASSIGN قبل START_WORK... إلخ).

### Timeline محدود

```csharp
private const int TimelinePreviewLimit = 30;
// ...
.OrderByDescending(entry => entry.OccurredAtUtc).Take(TimelinePreviewLimit).ToList();
```

لا "Audit كامل" يُسرَّب عبر هذا المسار مطلقًا — مؤكَّد باختبار تكامل يُنتج أكثر من 30 عنصر تاريخ حقيقي عبر انتقالات حالة صالحة متكررة، ويتحقق أن الاستجابة تعود بـ30 بالضبط.

### `POST /api/v1/notes` — وراثة الوحدة الداخلية (مُفعَّلة في هذه الدفعة)

`CreateNoteRequest.FacilityUnitId` كان موجودًا في العقد لكن غير مفعَّل فعليًا في `NoteCommandService.CreateDraftAsync` (كان يُثبَّت `null` دائمًا). الآن: عند وجود `FacilityUnitId`، يُتحقَّق شكليًا (`ValidateScopeShape`) ووجوديًا/نشاطًا وانتماءً للسجن الصحيح (`EnsureOrgEntitiesActiveAsync`) قبل القبول — كلاهما بنية تحتية موجودة مسبقًا في `OrganizationalScopeShape`/`OrganizationalScopeEntityGuard`، لم تُبنَ من جديد، فقط استُدعيت من نقطة لم تكن تستدعيها.

### `GET /api/v1/notes/{id}/eligible-assignees` و`/eligible-reviewers` — عميل واجهة جديد فقط

الـEndpoints موجودة مسبقًا (`INoteEligibilityService`)؛ الجديد هنا هو عميل الواجهة الأمامية (`api.notes.eligibleAssignees`, `api.notes.eligibleReviewers`) المستخدَم في نموذج Assign الجديد داخل الـWorkspace. الشكل: `EligibleUserDto(Guid Id, string DisplayNameAr, string UserName)`.
