# Phase D.6 — سجل معالجة المراجعة (CodeRabbit + SonarCloud) على PR #147

هذا السجل يوثق تصنيف ومعالجة كل ملاحظة فعلية وردت من CodeRabbit (تعليقان Critical inline + 27 تعليق Major) ومن SonarCloud (48 مشكلة جديدة: 1 Blocker، 8 Critical، 3 Major، 36 Minor) على أحدث SHA قبل بدء هذه الجولة. كل تصنيف تحقّق من الكود الفعلي الحالي، وليس من عنوان الملاحظة فقط.

التصنيفات المستخدمة: `StillValid` (تم إصلاحه)، `AlreadyFixed`، `Outdated`، `FalsePositive`، `NotReproducible`.

## P0 — Critical (CodeRabbit inline comments)

| File | Symbol | Finding | Severity | Status | Evidence | Fix | Test |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `RiskCommandService.cs` | `TransitionAsync` | `ArchiveAsync` لا يتحقق من state machine قبل تغيير الحالة، فيمكن أرشفة أي خطر من أي حالة. | Critical | StillValid — أُصلح | `RiskLifecycleStateMachine.EnsureAllowed(from, to)` لم تكن موجودة داخل `TransitionAsync`. | أضيف `EnsureAllowed(from, to)` مركزيًا داخل `TransitionAsync` (يغطي `ArchiveAsync` و`StartMonitoringAsync` معًا)؛ أُزيل التحقق المكرر من `StartMonitoringAsync`. | اختبار تكامل جديد `Archive_is_rejected_from_a_status_the_lifecycle_does_not_allow` (409 + لا تغيّر حالة/RowVersion). |
| `RiskMatrixService.cs` | `ListAsync/CreateAsync/ApproveAsync/ActivateAsync` | `organizationId` القادم من العميل غير مُتحقَّق من النطاق إطلاقًا — أي مستخدم يملك `Risks.View` يمكنه تمرير أي `organizationId`. | Critical | StillValid — أُصلح | لا يوجد أي استدعاء لـ`Scope.*` في هذه المسارات قبل الإصلاح. | أُضيف `RiskServiceBase.EnsureOrganizationVisibleAsync` يستخدم `Scope.HasNationalAccess`/`HasHeadquartersAccess`/`FilterRegions`/`FilterFacilities` الموجودة أصلًا (بدون توسيع `IOrganizationalScopeService`)، ومركزته داخل `LoadOrgMatrixAsync` لتغطية Approve/Activate تلقائيًا، وباستدعاء مباشر في `ListAsync`/`CreateAsync`. غياب النطاق أو المنظمة يرجع `KeyNotFoundException` (404) موحّدة. | 3 اختبارات تكامل جديدة: رفض منظمة خارج النطاق (404) لكل من List/Create، نجاح لمستخدم Facility-scoped ضمن منظمته، نجاح لمستخدم National (Global) scope بصرف النظر عن المنظمة. |

## P1 — Major (27 تعليق CodeRabbit)

| File | Finding | Status | Fix / Reasoning |
| --- | --- | --- | --- |
| `RiskManagementIntegrationTests.cs:633-640` | seed "SEC" ImpactDimension غير idempotent ضمن قاعدة مشتركة، قد يخالف unique index. | StillValid (منخفض الاحتمال فعليًا) — أُصلح دفاعيًا | `SharedIntegrationFixture.ResetAsync` يعيد ضبط القاعدة قبل كل اختبار (تحقّقت من الكود)، لذا التصادم غير قابل للتكرار فعليًا ضمن sequential xUnit execution الافتراضي. مع ذلك، حُوّل الـ seed إلى get-or-create دفاعيًا لإزالة الاعتماد الضمني على ترتيب/عزل التنفيذ. |
| `docs/phase-d6-risk-compliance-ledger.md:17-18` + `phase-d6-risk-source-linking.md:22-26` | قبول أنواع مصادر غير محقَّقة بلا رفض (fail-open). | StillValid — أُصلح | `EnsureSourceInScopeAsync` أصبح fail-closed: أي نوع خارج `ScopeCheckedTypes` (4 أنواع فقط الآن) يُرفض بـ`InvalidOperationException` بدل القبول الضمني. الوثيقتان حُدِّثتا لتعكسا هذا. |
| `RiskTreatmentEntities.cs:57-58` | يُزعم أن `A→A` و`A→B→A` ممكنان في سلسلة الاعتمادية. | **FalsePositive** | `RiskTreatmentAction.Id = Guid.NewGuid()` يُولَّد من طرف العميل (C#) عند الإنشاء ولا يُعرَف للمستدعي مسبقًا — فذاتية الاعتماد (`A→A`) مستحيلة بنيويًا عبر الـ API. لا يوجد أي مسار لتعديل `DependencyActionId` بعد الإنشاء (مؤكَّد بقراءة `ExecuteActionCommandAsync` كاملة) فتشكّل دورة لاحقة (`A→B→A`) مستحيل أيضًا: كل اعتمادية تُنشأ يجب أن تشير إلى إجراء **موجود مسبقًا** ضمن نفس الخطة، وبما أن العُقد لا يمكن إعادة توصيلها لاحقًا فإن ترتيب الإنشاء وحده يضمن DAG. كما يوجد أصلًا قيد قاعدة بيانات `CK_RiskTreatmentActions_NoSelfDependency` كطبقة حماية إضافية. لم تُضَف منطق اكتشاف دورات (unreachable code). |
| `RiskTreatmentEntities.cs:22-26` | `TargetScore` يُقبَل من العميل حرفيًا دون إعادة احتساب. | StillValid — أُصلح | أُزيل `TargetScore` من `RiskTreatmentPlanCreateRequest` بالكامل؛ `CreatePlanAsync` يحسبه من `TargetLikelihoodLevel.NumericValue × TargetImpactLevel.NumericValue` عند تحديد المستويين معًا (مع تحقق وجودهما)، أو يرفض تحديد أحدهما دون الآخر. |
| `RiskTreatmentEntities.cs:41-49` | `AssignedOrganizationId`/`AssignedFacilityUnitId` موجودان في الكيان لكن لا مسار إنشاء يملؤهما أو يتحقق نطاقهما. | StillValid — أُصلح | أُزيل الحقلان (والتهيئة المرتبطة بهما في EF Configuration) بدل توسيع منطق غير مطلوب حاليًا؛ فقط Workforce/User assignment مدعومان فعليًا. Migration أُعيد توليدها لتعكس الحذف (migration واحدة نظيفة، لم تُدمَج بعد). |
| `docs/phase-d6-risk-completion-report.md:20` + `permissions-matrix.md:239-266` | ادّعاء "الحزمة الكاملة" لـ`RiskOfficer` يناقض الجدول (ينقصه 7 صلاحيات اعتماد)؛ `Auditor` غير موجود في الجدول رغم ذكره في تقرير الإنجاز. | StillValid — أُصلح (توثيق) | حُدِّث التقرير والمصفوفة ليوضحا: `RiskOfficer` يحمل حزمة إدارة (18 من 25) تستثني عمدًا صلاحيات الاعتماد المحكومة بفصل المهام؛ `Auditor` يحصل فقط على `Risks.ViewSummary` (نفس حزمة `RegionalDirector`/`HeadquartersExecutive`) — لا حزمة إدارة أو اعتماد. لا تغيير في الصلاحيات الفعلية، فقط توضيح دقيق. |
| `RiskMatrixEntities.cs:17-18` | تعارض إصدار مصفوفة متزامن (`MAX(Version)+1`) قد يصطدم بـ unique index دون معالجة. | StillValid — أُصلح | `CreateAsync` يلتقط `DbUpdateException` من `SaveChangesAsync` ويحوّله لـ`InvalidOperationException` (409) برسالة تعارض واضحة بدل خطأ 500 غامض. |
| `RiskRecordEntity.cs:37-40` + `RiskCommandService.cs:69-76` (يشملان أيضًا `AssignOwnerAsync` و`RiskControlService.CreateAsync`) | مالك القوى البشرية (`OwnerWorkforceMemberId`) يُتحقق وجوده فقط دون نطاق المنظمة؛ `OwnerUserId` غير مُتحقَّق إطلاقًا. | StillValid — أُصلح | أُضيف `RiskServiceBase.EnsureOwnerAssignableAsync(organizationId, workforceMemberId, userId)` مشترك: يتحقق أن عضو القوى البشرية ينتمي لنفس `OrganizationId`، وأن المستخدم يملك نطاقًا (Region/Facility/Global) يتقاطع مع تلك المنظمة. مُستخدم في `RiskCommandService.CreateAsync`/`AssignOwnerAsync` و`RiskControlService.CreateAsync`. |
| `RiskImportService.cs:60-70` | N+1: إعادة تحميل نفس المصفوفة (3 Includes) لكل صف مطبَّق رغم أنها محمّلة مسبقًا في `ValidateRowsAsync`. | StillValid — أُصلح | `ValidateRowsAsync` يعيد الآن `Dictionary<Guid, RiskAssessmentMatrix>` (مع `.Include(RatingBands)` المضافة) يُمرَّر مباشرة لـ`CreateRiskFromRowAsync` — استعلام واحد للمصفوفات بدل استعلام لكل صف. |
| `RiskCommandService.cs:69-76` (owner org scope) | مكرر مع البند أعلاه — نفس الإصلاح. | مُدمَج أعلاه | — |
| `RiskImportService.cs:168-181` | عنوان فارغ (`Title`) يمكن أن يسبب `NullReferenceException` عند `row.Title.Trim()`. | StillValid — أُصلح | أُضيف شرط `!string.IsNullOrWhiteSpace(row.Title)` قبل حساب `titleKey`/فحص التكرار، مطابقًا للإصلاح المقترح حرفيًا. |
| `RiskCommandService.cs:25-34` | فحص تفرّد رمز التصنيف يقارن القيمة الخام غير المهذَّبة بينما التخزين يستخدم القيمة المهذَّبة (`Trim()`). | StillValid — أُصلح | يُحسب `code = request.Code.Trim()` مرة واحدة ويُستخدم في كل من فحص التكرار والتخزين. |
| `RiskImportService.cs:46-57` | Confirm للاستيراد قد يفوت تعارضًا متزامنًا لأن الفهرس الفريد (`FacilityId, ImportKind, FileHash`) أضيق من مفتاح البحث في الكود (يضيف `SourceSystem`/`SourceReference`). | StillValid — أُصلح بنهج مختلف عن المقترح | لم يُوسَّع الفهرس (توسيعه كان سيُضعف ضمان idempotency الفعلي المرتكز على محتوى الملف — نفس hash يعني نفس الاستيراد منطقيًا بصرف النظر عن نظام المصدر). بدلًا من ذلك، أُضيف `try/catch(DbUpdateException)` حول `SaveChangesAsync` النهائي: عند خسارة السباق يُعاد تحميل الدفعة الفائزة وتُرجَع نتيجتها idempotent-إعادة، مطابقًا للنمط الآمن القياسي في هذا الكود لهذا النوع من السباقات. |
| `RiskTreatmentService.cs:58-61` | فحص وجود عضو القوى البشرية (مالك خطة/منفِّذ إجراء) غير مقيَّد بالسجن — عضو من سجن آخر يمكن ربطه ويظهر اسمه عبر DTO لمستخدم بنطاق مختلف. | StillValid — أُصلح | أُضيف `RiskServiceBase.EnsureWorkforceMemberInFacilityAsync(facilityId, memberId, message)` يستخدم نفس المِحمول (`CurrentOperationalFacilityId == facilityId \|\| HomeFacilityId == facilityId`) الموجود أصلًا في `RiskSourceLinkService`؛ مُستخدم في `CreatePlanAsync` (مالك الخطة) و`CreateActionAsync` (منفِّذ الإجراء). |
| `RiskSourceLinkService.cs:19-23` | `WorkforceCoverageGap`/`WorkforceQualificationIssue` يُتحقق منهما خطأً ضد جدول `WorkforceMember` (معرّف مختلف تمامًا)، يرفض أي ربط شرعي بهذين النوعين، ويناقض توثيق الكلاس نفسه. | StillValid — أُصلح | أُزيل النوعان من `ScopeCheckedTypes` (لا يوجد محلّل نطاق صحيح لهما بعد)؛ بالاشتراك مع تفعيل fail-closed أعلاه، أصبحا مرفوضين صراحة بدل مقبولين بتحقق خاطئ أو بلا تحقق. |
| `RiskReviewService.cs:142-148` | `RequestedReviewFrequencyDays` يُحفَظ ويُطلَب لكنه لا يُطبَّق أبدًا — `NextReviewDueAtUtc` يُضبَط دائمًا على انتهاء القبول بدل دورة المراجعة، فيسكت مؤشر `OverdueReview`. | StillValid — أُصلح | عند اعتماد القبول: `NextReviewDueAtUtc = now.AddDays(RequestedReviewFrequencyDays)` إن وُجدت، وإلا `RequestedAcceptedUntilUtc` (fallback)، مطابقًا للإصلاح المقترح حرفيًا. |
| `RiskRegisterQueryService.cs:82-85` | حساب `AverageOpenRiskAgeDays` يُحمِّل صفًا واحدًا لكل خطر مفتوح بدل دمجه في التجميع الموجود. | StillValid — **حاولنا الإصلاح المقترح حرفيًا فاكتشفنا خللًا حقيقيًا به** | تم بالفعل تطبيق الإصلاح المقترح (`SUM(FirstIdentifiedAtUtc.UtcTicks)` ضمن `GroupBy(1)`)، لكنه فشل فعليًا: EF Core / SQL Server provider لا يترجم `DateTimeOffset.UtcTicks` داخل تجميع SQL، فرمى `InvalidOperationException` عند التنفيذ الفعلي — ظهر كاستجابة 409 غير متوقعة على endpoint الملخص (اكتُشف عبر اختبار تكامل حي فشل فجأة، وليس عبر مراجعة نظرية). أُعيد الكود للنهج الأصلي (استعلام إضافي خفيف بعمود واحد فقط) مع توثيق سبب عدم إمكانية الدمج في هذا التعليق البرمجي. |
| `FacilityWorkspaceReadService.cs:843-844` | عناصر المخاطر تُصدَّر بـ`Type = InterventionType` الخام الذي لا يعرفه `panelForPriorityItem` في الواجهة، فينتهي بها المطاف في اللوحة العامة `activity` بدل `RiskPanel`. | StillValid — أُصلح | `Type` أصبح ثابتًا `"risk"`، و`Reference` يحمل `"{InterventionType}:{RiskRecordId}"`. فرع جديد في `panelForPriorityItem` (frontend) يستخرج الجزء الثاني كـ`entityId`. |
| `RiskManagementEndpoints.cs:148-165` | `organizationId` في مجموعة `/risk-matrices` يُقرأ من الاستعلام دون التحقق من نطاق المستدعي على مستوى الـ endpoint. | StillValid — **أُصلح على مستوى الخدمة بدل الـ endpoint** | بدل ربط/تحقق عند الـ endpoint (كما اقترحت المراجعة)، رُكِّز التحقق داخل `RiskMatrixService` نفسها (`EnsureOrganizationVisibleAsync`) — تغطية أقوى لأنها تحمي أي مستدعٍ مستقبلي للخدمة بصرف النظر عن مسار الـ HTTP، لا فقط هذا الـ endpoint. مطابق للإصلاح المذكور في بند P0 أعلاه. |
| `FacilityWorkspaceReadService.cs:847` | ضرب `PriorityRank` في 9 يجعل مخاطر متوسطة الأهمية تتصدر كل الأنواع الأخرى في قائمة الأولويات المشتركة. | StillValid — أُصلح (من الجولة السابقة) | أُزيل الضرب؛ `PriorityRank` يُستخدم كما هو من `RiskInterventionItemDto` (نطاق 30-100 مطابق لبقية المجالات). |
| `FacilityWorkspacePage.tsx:1009-1051` | `reasonDraft` مشترك بين التصعيد وإعادة الفتح — كتابة سبب تصعيد ثم النقر على "إعادة فتح" يرسل نص التصعيد كسبب لإعادة الفتح. | StillValid — أُصلح (من الجولة السابقة) | `reasonDraft` استُبدل بـ`reasonDrafts: Record<string, string>` مفتاح بكل أمر (`Escalate`/`Reopen` منفصلان). |

## SonarCloud — 48 مشكلة جديدة (تحقّق فعلي عبر SonarCloud API على PR #147)

### Blocker (1) — أُصلح

| File:Line | Finding | Fix |
| --- | --- | --- |
| `RiskLifecycleStateMachineTests.cs:52` | `EnsureAllowed_DoesNotThrowOnValidTransition` بلا أي assertion. | أُضيف `Assert.Null(Record.Exception(...))` — assertion حقيقية وليست شكلية. |

### Critical — Cognitive Complexity (8) — **مؤجَّلة صراحة**

| File:Line | Complexity | القرار |
| --- | --- | --- |
| `RiskAssessmentService.cs:41` | 17 → 15 | مؤجَّل |
| `RiskAssessmentService.cs:285` | 23 → 15 | مؤجَّل |
| `RiskImportService.cs:113` | 26 → 15 | مؤجَّل |
| `RiskReadinessService.cs:45` (`BuildInterventionsAsync`) | 42 → 15 | مؤجَّل |
| `RiskRegisterQueryService.cs:103` | 25 → 15 | مؤجَّل |
| `RiskRegisterQueryService.cs:353` | 29 → 15 | مؤجَّل |
| `RiskReviewService.cs:36` | 16 → 15 | مؤجَّل |
| `BaseeraDbContext.cs:389` | 25 → 15 | مؤجَّل |

**سبب التأجيل الصريح**: إعادة هيكلة 8 methods بهذا الحجم (بعضها يتجاوز 40 نقطة تعقيد) لخفضها إلى ≤15 كل واحدة تتطلب تقسيمًا دقيقًا إلى methods فرعية واضحة المسؤولية مع الحفاظ الكامل على: صحة الدرجة المحسوبة على الخادم، فصل المهام (four-eyes)، دورة الحياة، سجل التدقيق، وميزانية عدد الاستعلامات — وإعادة اختبار كل مسار بعد كل تقسيم. حجم هذا العمل (8 methods منفصلة، كل منها بحاجة اختبارات تكامل حية للتحقق من عدم كسر أي مسار) يتجاوز ما يمكن إنجازه بثقة كافية ضمن هذه الجولة دون تعريض الإصلاحات الأمنية/الوظيفية الحرجة أعلاه (P0/P1) لخطر الانحدار. تم إصلاح كل ما هو أمني/وظيفي حقيقي أولًا؛ هذا البند تنظيف كود بحت (لا يؤثر على السلوك) ومُوثَّق هنا كفجوة صريحة بدل التظاهر بإغلاقه.

### Major (3) — أُصلحت جميعها

| File:Line | Finding | Fix |
| --- | --- | --- |
| `RiskReadinessService.cs:206` | `Build` بـ9 معاملات (الحد 7). | دُمجت `(riskId, code, title)` في tuple واحد `(Guid Id, string Code, string Title) risk` → 7 معاملات. |
| `RiskTreatmentService.cs:133/144` | `Start` و`Unblock` لهما نفس التنفيذ حرفيًا. | دُمج `case Start: case Unblock:` بتعليق يوضح أن كل حالة تُقيَّد فعليًا عبر `EnsureAllowed` بحالتها المصدر الصحيحة (Approved أو Blocked على التوالي). |
| `FacilityWorkspacePage.tsx:1057` | Ternary متداخل لرسالة الخطأ. | استُخرج إلى `riskCommandErrorMessage(conflict, error)`. |

### Minor (36)

| الفئة | العدد | القرار |
| --- | --- | --- |
| Null-forgiving operator على navigation properties مطلوبة (EF) في 9 ملفات كيانات (`RiskAssessmentEntities` ×7, `RiskCategoryEntity` ×1, `RiskControlEntity` ×1, `RiskImportEntities` ×4, `RiskMatrixEntities` ×6, `RiskRecordEntity` ×3, `RiskReviewEntity` ×1, `RiskSourceLinkEntity` ×1, `RiskTreatmentEntities` ×2) | 26 | **FalsePositive** — كل حالة هي `public T Nav { get; set; } = null!;` لـnavigation property مطلوبة (`RiskRecord`, `Organization`, إلخ) مع Nullable enabled وEF Core يملؤها فعليًا عند التحميل، بينما البناء الصحيح للتطبيق يستخدم حقل الـFK فقط دون تزويد الـnavigation. إزالة `null!` تُنتج `CS8618`؛ تحويلها لـ`required` يكسر كل بناء بالـFK فقط في كل الخدمات. لم تُستخدم `NOSONAR`/pragma بناءً على التعليمات الصريحة — التصنيف موثَّق هنا فقط. |
| Null-forgiving operator فعلي (redundant) | 1 | **StillValid — أُصلح**: `RiskAssessmentService.cs:85` (`matrix.ImpactWeightingJson!`) كان يخفي احتمال null حقيقيًا (بيانات تالفة/مصفوفة لم تُصادَق بشكل صحيح تُنتج `ArgumentNullException` غامضة من `JsonSerializer.Deserialize`). استُبدل بحارس صريح يرمي `InvalidOperationException` برسالة عربية واضحة. |
| Define a constant بدل تكرار literal | 7 (`RiskDataQualityService.cs` ×3، `RiskReadinessService.cs` ×2، `FacilityWorkspaceReadService.cs` ×1، `BaseeraDbContext.cs` ×1) | **StillValid — أُصلحت جميعها**: `SeverityLowAr`/`SeverityMediumAr`/`SeverityHighAr`/`RiskOfficerRoleAr` في `RiskDataQualityService`؛ نفس الأنماط في `RiskReadinessService`؛ `DataQualityMissing` في `FacilityWorkspaceReadService` (بجانب `DataQualityComplete`/`DataQualityPartial` الموجودتين)؛ `InMemoryProviderName` في `BaseeraDbContext`. |
| Define a constant لـ"مسودة" (4 تكرارات) | 1 | **رُفض عمدًا (ليس FalsePositive تقني، بل قرار تصميم)**: التكرارات الأربعة في `RiskManagementDisplay.cs` تمثل حالة "مسودة" لأربعة enums مختلفة تمامًا (`RiskStatus`, `AssessmentStatus`, `TreatmentPlanStatus`, `RiskTreatmentActionStatus`) — تطابق النص العربي مصادفة لا يعني أنها نفس المفهوم الدلالي. دمجها في ثابت واحد مشترك يخلق اقترانًا زائفًا بين أربع حالات دومين مستقلة تمامًا، وقد يُغيَّر أحدها مستقبلًا (نص أو قيمة) دون الآخرين. لم يُعدَّل الكود. |
| Loop simplification (`Select`) | 1 | **StillValid — أُصلح**: حُوِّلت 9 حلقات `foreach` بنمط "إضافة عنصر واحد فقط" في `RiskReadinessService.BuildInterventionsAsync` إلى `items.AddRange(...Select(...))`، مطابقًا للنمط المقترح. |

## ملخص الحالة

- **P0 (Critical)**: 2/2 مُصلَحة بالكامل ومُختبرة حيًّا (integration tests جديدة).
- **P1 (Major CodeRabbit)**: 27/27 جرى فتحها والتحقق منها؛ 26 مُصلَحة أو مُوثَّقة كتوضيح، 1 مصنَّفة FalsePositive بأدلة ملموسة (قيد DB + استحالة بنيوية).
- **Sonar Blocker**: 1/1 مُصلَح.
- **Sonar Major**: 3/3 مُصلَحة.
- **Sonar Minor** (36 إجمالًا): 9 أُصلحت فعليًا (7 ثوابت + تبسيط حلقة واحد + null-forgiving فعلي واحد)، 26 مصنَّفة FalsePositive بأدلة (null-forgiving على EF navigation مطلوبة)، 1 مرفوضة بقرار تصميم موثَّق (دمج "مسودة" عبر 4 enums مختلفة).
- **Sonar Critical (Cognitive Complexity)**: 0/8 — **مؤجَّلة صراحة** بسبب حجم/مخاطر إعادة الهيكلة مقابل الوقت المتاح؛ لا تمثل عيوبًا وظيفية أو أمنية، بل قابلية صيانة الكود. موثقة كفجوة صريحة وليست خللاً مخفيًا.

## جولة ثانية — مراجعة CodeRabbit على الكوميتات السبعة أعلاه (3 تعليقات جديدة)

بعد دفع الكوميتات السبعة وطلب مراجعة CodeRabbit مجددًا، أعادت المراجعة 3 ملاحظات جديدة (خارج نطاق الـdiff المباشر، كلها في `RiskSourceLinkService.cs`):

| Finding | Status | Fix / Reasoning |
| --- | --- | --- |
| فحص التكرار (`AnyAsync`) قبل الإضافة عرضة لسباق (TOCTOU)؛ بلا فهرس فريد قد تُخزَّن نسخ مكررة، ومع وجوده يصل الطلب الخاسر لـ`SaveChangesAsync` كخطأ قاعدة بيانات غير معالَج. | **StillValid جزئيًا — أُصلح** | الفهرس الفريد المفلتر (`RiskRecordId, SourceEntityType, SourceEntityId, RelationshipType` عند `IsDeleted=0`) **موجود بالفعل** في `RiskSourceLinkConfiguration` (لم يكن مفقودًا كما افترضت الملاحظة). الفجوة الفعلية الوحيدة: لا يوجد `catch` لـ`DbUpdateException` عند تصادم السباق. أُضيف try/catch يحوّل التصادم لنفس رسالة "الرابط موجود بالفعل" (409) بدل خطأ 500 خام، بنفس النمط المستخدم في `RiskMatrixService`/`RiskImportService`. |
| حفظ الرابط بـ`SaveChangesAsync` منفصل قبل تسجيل التدقيق — فشل التدقيق أو الحفظ الثاني يترك التغيير محفوظًا بلا سجل تدقيق مرافق. | **Outdated / نمط عام على مستوى الكود بأكمله** | هذا الشكل بالضبط (Add → SaveChanges → Audit → SaveChanges) مستخدم بلا استثناء عبر كل خدمات RiskManagement التسع (وأصله في `SensitiveCustodyServices`، النمط المرجعي المذكور صراحة في تعليقات الكود). إصلاحه في هذا الملف فقط يخلق تناقضًا أسلوبيًا مع بقية الخدمات دون معالجة القرار المعماري الحقيقي (هل يجب أن يفشل الحفظ الأساسي إذا فشل التدقيق، أم العكس؟) — قرار يتجاوز نطاق ملف واحد ويحتاج تبنّيًا موحدًا عبر التطبيق كله. لم يُعدَّل. |
| `RelationshipType` (وكذلك `SourceEntityType` ضمنيًا) لا يُتحقق أنه قيمة enum معرَّفة قبل الحفظ. | **StillValid — أُصلح** | أُضيف `Enum.IsDefined` على كلا الحقلين في `AddAsync` قبل أي معالجة أخرى، يرفض بـ409 عند قيمة غير معرَّفة. لا يوجد تحقق enum مماثل في بقية التطبيق (خاصية عامة للنظام كله)، لكن نطاق الإصلاح هنا محدود ومنخفض المخاطر ويخدم مبدأ التوثيق الصريح لهذا الملف ("نمط مُنضبط، لا JSON عام"). اختبار تكامل جديد `Source_link_rejects_undefined_enum_values`. |

## جولة ثالثة — مراجعة CodeRabbit على التوثيق (3 تعليقات إضافية)

| Finding | Status | Fix |
| --- | --- | --- |
| `permissions-matrix.md`/`completion-report.md`: وصف الاستثناءات السبعة لـ`RiskOfficer` بأنها كلها "صلاحيات اعتماد" غير دقيق — `Risks.Export` ليست صلاحية اعتماد. | StillValid — أُصلح | صيغ النص لتوضيح: 6 صلاحيات اعتماد محكومة بفصل المهام + `Risks.Export` بصفتها صلاحية مقيَّدة منفصلة (لأسباب إشرافية/رقابية لا فصل مهام). |
| `phase-d6-review-remediation.md`: مجموع فئات Minor (24+1+7+1+1=34) لا يطابق الإجمالي المُعلَن (36). | StillValid — أُصلح | كان عدّ null-forgiving الخاص بـEF navigation خطأ حسابيًا (24 بدل 26 الفعلية عبر الملفات التسعة). صُحِّح الجدول والملخص؛ 26+1+7+1+1=36 مطابق الآن. |
| `phase-d6-risk-matrix-versioning.md`/`phase-d6-risk-scoring.md`: العبارة "الحد الأعلى للنطاق الأخير يبقى شاملًا" غامضة/غير دقيقة — قد تُقرأ كأن التغطية تقف عند `MaximumScore` بينما الكود الفعلي (`SelectRatingBand`) لا يفرض أي حد أعلى على الإطلاق للنطاق الأخير. | StillValid — أُصلح | صيغت العبارة صراحة: النطاق الأخير بلا حد أعلى فعلي في الاختيار؛ `MaximumScore` هناك للتحقق البنيوي فقط (`ValidateRatingBands`)، مطابقًا لاختبار `SelectRatingBand_LastBandCoversAnyScoreAtOrAboveItsMinimum`. |

## ما لم يُلمَس عمدًا (خارج نطاق هذه الجولة، غير مذكور في مراجعة CodeRabbit/Sonar لكنه ذو صلة)

- Composite foreign keys لضمان اتساق المصفوفة عبر التقييمات/سلسلة الإصدارات/اعتمادية إجراءات المعالجة (بنود ورد ذكرها في `BaseeraDbContextModelSnapshot.cs` ضمن التعليقات الأصلية لمراجعة CodeRabbit كـ"Heavy lift" ولم تُدرَج ضمن قائمة الـ27 Major المُرقَّمة أعلاه لأنها ظهرت كتعليقات إضافية على نفس الملف مرتبطة ببعضها) — تتطلب تصميم مخطط جديد (composite keys) وmigration إضافية، وهذا تغيير مخاطرته أعلى من نطاق جولة مراجعة واحدة على domain قيد المراجعة الأولى. موثَّقة هنا صراحة كفجوة مستقبلية.
