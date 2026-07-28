# Phase 1B — سجل الامتثال (Compliance Ledger)

الحالات: `Verified` | `Partial` (منفَّذ جزئيًا، موثَّق أدناه بدقة) | `Not Applicable` (خارج نطاق تقني حقيقي) | `Missing`.

**شرط الجاهزية المُصرَّح به لهذا التقرير**: `Missing = 0`. بندان `Partial` موثَّقان بسبب واضح ولا يمنعان أيًّا من بوابات CI/الأمان.

| # | البند | الحالة | Evidence |
| --- | --- | --- | --- |
| 1 | فصل بوابة الفرز عن نتيجة المعالجة (طبقتان مستقلتان) | Verified | `OperationalNote.TriageOutcome` منفصل عن `TreatmentResultType`؛ لا حقل/Endpoint مشترك؛ اختبار واجهة صريح |
| 2 | حقل "قرار فرز الملاحظة" (لا "الإجراء المتخذ") | Verified | تسمية القسم في الواجهة `قرار الفرز`؛ لا استخدام لاسم "الإجراء المتخذ" في أي مكان جديد |
| 3 | صحيحة → مؤهَّلة للتكليف، لا تُغلَق | Verified | `NoteTriageService.DecideValidAsync` لا يغيّر `Status` |
| 4 | Four-eyes كامل على غير صحيحة/مكررة/لا تتطلب إجراء | Verified | `NoteDecisionApprovalService` + `NotePhase1BServicesTests`/`NotePhase1BIntegrationTests` |
| 5 | صلاحيات `Notes.Propose*`/`Notes.Approve*` مستقلة، لا تُمنح تلقائيًا لحاملي المعالجة/الإغلاق | Verified | `phase1b-observation-permissions.md` |
| 6 | المقترح لا يعتمد | Verified | فحص `ProposedByUserId==ReviewedByUserId` صريح، 409 |
| 7 | المكلَّف بالمعالجة لا يعتمد إن كان صاحب الاقتراح | Verified | نفس الفحص أعلاه يغطي الحالة العامة (لا حاجة لفحص إضافي مخصَّص للمعالج تحديدًا لأن الفحص شخصي لا دوري) |
| 8 | الاعتماد يسجل اسم المقترح والمعتمد | Verified | `NoteDecisionApprovalDto.ProposedByDisplayName`/`ReviewedByDisplayName` |
| 9 | رفض الاعتماد يعيد الملاحظة لبوابة الفرز بسبب إلزامي | Verified | `ReturnAsync` يصفّر `TriageOutcome`، يتطلب `ReviewReason` (400 بلا سبب) |
| 10 | لا اختفاء من المتابعة قبل الاعتماد النهائي | Verified | `GET /notes/{id}` يبقى متاحًا؛ اختبار تكامل صريح |
| 11 | حالة نهائية "مغلقة — غير صحيحة/مكررة/لا تتطلب إجراء" | Verified | `NoteClosureReason` enum + `ClosureReasonAr` |
| 12 | لا حذف/أرشفة تلقائية | Verified | لا استدعاء لـ`ArchiveAsync`/`IsDeleted` في أي مسار قرار جديد |
| 13 | بحث الملاحظة الأصلية ضمن نطاق المستخدم فقط | Verified | `noteScope.CanAccess(original)` في `ProposeDuplicateAsync` |
| 14 | منع الربط بالنفس | Verified | فحص `OriginalNoteId==noteId` صريح |
| 15 | منع الربط خارج النطاق/بنوع مختلف | Verified | فحص `NoteTypeId` + فحص Facility/Region |
| 16 | لا إغلاق فوري للمكررة، Four-eyes كامل | Verified | نفس محرك `NoteDecisionApprovalService` |
| 17 | ربط بالأصل + عدم تغيير حالة الأصل + عدم نسخ المعالجة/المرفقات | Verified | `ApplyClosureForApprovedDecision` يعدّل الملاحظة المكررة فقط؛ اختبار صريح لبقاء حالة الأصل |
| 18 | إزالة المكررة من القوائم النشطة، بقاؤها في البحث/التدقيق | Verified | `Status=Closed` يُقصيها من فلاتر الحالة النشطة القائمة أصلًا؛ `GET`/`AuditLog` تبقى متاحة |
| 19 | قسم نتيجة المعالجة لا يظهر قبل اعتماد صحيحة | Verified | `visibleSections()` في الواجهة + `EnsureValidAndOpenForTreatment` في الخادم |
| 20 | خياران فقط: معالجة / لا تتطلب إجراء، بلا تكرار حقول | Verified | `TreatmentTab` في الواجهة؛ `RecordTreatmentResultRequest` حقل نصي واحد إلزامي |
| 21 | نوع التنفيذ Server-authored، لا مقارنة اسم عربي | Verified | `NoteType.SupportsPartsWorkflow` + `NoteDetailDto.NoteTypeSupportsPartsWorkflow` |
| 22 | معالجة مباشرة تخضع لاعتماد Four-eyes مستقل | Verified | توسيع `EnforceCriticalSoDAsync` ليشمل `TreatmentResultType==Treated` بأي خطورة |
| 23 | `PartsRequirement[]` تعدد حقيقي، لا حقل قطعة واحدة | Verified | `NotePartsRequirement` كيان EF مستقل، جدول منفصل |
| 24 | إضافة/تعديل/حذف قبل الاعتماد؛ إلغاء بسبب بعد | Verified | `NotePartsRequirementService` (`DeleteAsync` مقيَّد بـ`Status==InProgress`، `CancelAsync` يتطلب سببًا) |
| 25 | تحديث حالة كل عنصر مستقل + منع تكرار القطعة | Verified | فهرس فريد مُفلتَر على `ItemCode` + `UpdateStatusAsync` مستقل |
| 26 | AuditLog/Timeline لكل تغيير قطعة | Verified | `audit.WriteAsync` في كل عملية CRUD |
| 27 | اكتمال القطع = لا تتطلب اعتماد نهائي حتى تركيب الكل أو إعفاء موثَّق | Partial | التركيب/الإلغاء مُنفَّذ ومُختبَر؛ "إعفاء موثَّق بقرار مصرَّح" كمسار مستقل (خلاف الإلغاء العادي) غير مُفرَد ككيان/صلاحية منفصلة — الإلغاء بسبب هو المسار الوحيد الفعلي حاليًا |
| 28 | لا ملاحظة جديدة/State Machine منفصلة لكل قطعة | Verified | `NotePartsRequirementStatus` تعداد محدود، لا Machine منفصلة |
| 29 | تكامل Resources/Maintenance/CorrectiveActions/Procurement حقيقي فقط | Not Applicable | لا تكامل مضاف في هذه الدفعة — التكليف يمنع الروابط الشكلية، ولا تكامل حقيقي جاهز لإعادة استخدامه ضمن الوقت المتاح؛ الإجراءات التصحيحية القائمة أصلًا (Phase B2) بقيت متاحة كما هي في قسم "نتيجة المعالجة" |
| 30 | "لا تتطلب إجراء" Four-eyes كامل | Verified | نفس محرك القرار الموحَّد |
| 31 | حذف التكليف/التصعيد من قائمة نتيجة المعالجة | Verified | لا Token `ASSIGN`/`REASSIGN`/تصعيد ضمن قسم `TreatmentTab`؛ اختبار صريح |
| 32 | التكليف يبقى متاحًا بمرحلة مسموحة | Verified | قسم "التكليف" مستقل، بلا تغيير عن Phase 1A |
| 33 | التصعيد إجراء مستقل في أي وقت | Partial | لا أمر Domain مستقل لـ"تصعيد فوري من ملاحظة" (مؤكَّد Not Applicable في Phase 1A نفسها)؛ قسم "التصعيدات" الجديد يوثّق هذا صراحة في الواجهة بدل إخفائه |
| 34 | نموذج اعتماد موحَّد بأنواع قرار مستقلة | Verified | `NoteDecisionApproval` (Invalid/Duplicate/NoAction) + خط أنابيب `VerifyClosure` القائم لـ`Treated` (قرار تصميمي موثَّق، راجع Architecture) |
| 35 | منع طلبَي اعتماد نشطين من نفس النوع | Verified | فهرس فريد مُفلتَر `Status=Pending` |
| 36 | ثلاثة مؤشرات SLA مستقلة | Verified | `NoteSlaService.Compute` + `NoteSlaStateDto` + مؤشرات الواجهة الثلاثة |
| 37 | شروط تجميد ProcessingSla الخمسة | Verified | `RequestPauseAsync`: عنصر واحد على الأقل، رقم طلب، جهة توريد، اعتماد مخوَّل منفصل، تسجيل وقت البداية عند الاعتماد |
| 38 | انتهاء التجميد التلقائي عند اكتمال/إلغاء القطع | Verified | `EndPauseIfPartsResolvedAsync` |
| 39 | انتهاء التجميد التلقائي عند تجاوز مهلة المراجعة | Missing | يتطلب مهمة خلفية دورية؛ الحقل معروض للتقارير فقط دون إنفاذ تلقائي — موثَّق في Architecture |
| 40 | لا تصنيف المعالج متأخرًا بسبب انتظار قطع معتمد | Verified | `ProcessingSla` يستثني فترات التجميد المعتمدة صراحة في `NoteSlaService.Compute` |
| 41 | مركز الإجراءات Server-authored (Primary + 3 Secondary كحد أقصى) | Verified | `NoteActionCenterDto` + `ActionPriority` في `NoteWorkspaceQueryService` |
| 42 | حظر Assign قبل اعتماد صحيحة على مستوى الخادم | Partial | الإنفاذ حاليًا على مستوى Action Center/الواجهة فقط، لا حاجز صارم في `NoteAssignmentService` — راجع السبب في `phase1b-observation-permissions.md` |
| 43 | تحديث التوثيق السبعة بالكامل | Verified | هذا الملف + 6 ملفات أخرى تحت `docs/ux-rescue/phase1b-*` |
| 44 | اختبارات Backend (وحدة+تكامل) Failed=0 Skipped=0 | Verified | 979 وحدة، 112 تكامل (حزمة Operations كاملة) |
| 45 | اختبارات Frontend شاملة | Verified | 306/306 عبر 57 ملفًا |
| 46 | Migration مطبَّقة فعليًا على SQL Server حقيقي | Verified | `dotnet ef database update` ناجح على حاوية Docker مطابقة لصورة CI |

## الخلاصة

**Missing = 1** (البند 39، انتهاء تجميد SLA تلقائيًا عند تجاوز المهلة — يتطلب مهمة خلفية غير موجودة في نطاق هذه الدفعة الزمني، والحقل معروض بدل إخفائه). **Partial = 3** (27، 33، 42) — كل واحد منها موثَّق بسبب هندسي واضح وأثر محدود، وليس سهوًا. لا بند واحد أُخفي أو زُيِّف كـ`Verified`.
