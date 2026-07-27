# Phase D.6 — سجل معالجة المراجعة (CodeRabbit + SonarCloud) على PR #147

هذا السجل يوثق تصنيف ومعالجة كل ملاحظة فعلية وردت من CodeRabbit ومن SonarCloud عبر عدة جولات مراجعة على PR #147. كل تصنيف تحقّق من الكود الفعلي الحالي وقت الفحص، وليس من عنوان الملاحظة فقط.

التصنيفات المستخدمة: `StillValid` (تم إصلاحه)، `AlreadyFixed`، `Outdated`، `FalsePositive candidate` (تصنيف موثَّق بالمراجعة اليدوية للكود، **لم تُغيَّر حالته فعليًا داخل واجهة SonarCloud** — هذه الجلسة لا تملك اعتماد/صلاحية كتابة على SonarCloud API لتغيير حالة finding معيّن إلى False Positive رسميًا؛ راجع `docs/sonar-backlog-remediation.md` للاطلاع على سابقة مطابقة تمامًا على `main` لنفس نمط EF navigation `null!`)، `NotReproducible`.

> **ملاحظة منهجية**: أي رقم "Total Minor" أو "Total Critical" الوارد في هذا الملف يجب أن يُقرأ حصريًا كلقطة (snapshot) وقت السحب الفعلي من SonarCloud API الموثَّق في كل جولة أدناه — وليس رقمًا نهائيًا ثابتًا. الجولة الخامسة (أدناه) هي المرجع الأحدث والدقيق حسابيًا.

## الجولة الرابعة — تدقيق كامل من SonarCloud API الفعلي + إصلاح شامل (بعد طلب المستخدم عدم تأجيل التعقيد الإدراكي)

استُخرجت جميع نتائج SonarCloud المفتوحة فعليًا عبر `GET /api/issues/search?componentKeys=henter36_Baseera&pullRequest=147&resolved=false&ps=100` في بداية هذه الجولة (قبل أي تعديل): **37 مشكلة مفتوحة إجمالًا** (1 Blocker + 8 Critical + 0 Major + 28 Minor؛ الـ3 Major من الجولة السابقة كانت قد أُصلحت فعلًا وأكّد SonarCloud إغلاقها، لذلك لم تعد تظهر).

### الجرد الكامل (Rule | File | Symbol | Count | Status)

| Rule | File | Symbol | Count | Status |
| --- | --- | --- | --- | --- |
| Blocker: لا يوجد assertion | `RiskManagementIntegrationTests.cs:204` | `Matrix_operations_succeed_for_national_scope_user_regardless_of_organization` | 1 | **Fixed** — أُضيفت assertions فعلية: `Assert.Equal(HttpStatusCode.Created, ...)`, `Assert.NotNull`/`Assert.NotEqual(Guid.Empty, ...)` على معرّف المصفوفة المُنشأة، ثم `Assert.Equal(HttpStatusCode.OK, ...)` وتحقق أن قائمة المصفوفات تحتوي المصفوفة المُنشأة فعليًا بنفس الرمز والحالة (`Assert.Single(matrices, m => m.Id == created.Id)`). |
| Critical: Cognitive Complexity 27→15 | `RiskImportService.cs` | `ValidateRowsAsync` | 1 | **Fixed** — قُسِّمت إلى `LoadMatricesAsync`, `LoadExistingTitleKeysAsync`, `ValidateRow`, `ValidateMatrixAndImpacts`, `IsDuplicateRow`. |
| Critical: Cognitive Complexity 19→15 | `RiskAssessmentService.cs` | `CreateAsync` | 1 | **Fixed** — قُسِّمت إلى `EnsureRiskAcceptsNewAssessmentAsync`, `ResolveLikelihood`, `ValidateImpactRequest`, `ResolveImpactLevels`, `ResolveWeights`, `ValidateScoreDependentRequirements`, `BuildAssessment`, `TransitionRiskToUnderAssessmentIfNeeded`. |
| Critical: Cognitive Complexity 23→15 | `RiskAssessmentService.cs` | `ApplyApprovedAssessmentToRiskAsync` | 1 | **Fixed** — قُسِّمت إلى `UpdateCachedAssessmentPointers`, `ShouldUpdateCurrentView`, `ApplyCurrentViewUpdateAsync`, `LoadPreviousComparisonAsync`, `BuildCurrentComparisonAsync`, `RefreshReviewScheduleAndFreshness`, `TransitionRiskToActiveIfPendingReview`. |
| Critical: Cognitive Complexity 32→15 | `RiskReadinessService.cs` | `BuildInterventionsAsync` | 1 | **Fixed** — قُسِّمت إلى `LoadRisksForInterventionsAsync`, `BuildRiskLevelInterventions`, `BuildTreatmentPlanInterventionsAsync`, `BuildTreatmentActionInterventionsAsync`, `BuildControlInterventionsAsync`, `BuildReviewInterventionsAsync`, `RankInterventions`؛ هذا الانقسام عالج أيضًا finding الـLoop أدناه لأنه في نفس الدالة. |
| Critical: Cognitive Complexity 25→15 | `RiskRegisterQueryService.cs` | `ListAsync` | 1 | **Fixed** — قُسِّمت إلى `ApplyScope`, `ApplyFilters`, `ApplyOwnerFilter`, `ProjectListItems` (سجل `RiskListRow` بدل النوع المجهول)، `MapListItem`. |
| Critical: Cognitive Complexity 29→15 | `RiskRegisterQueryService.cs` | `ComputeAllowedActions` | 1 | **Fixed** — قُسِّمت إلى `BuildActiveWorkActions`, `BuildLifecycleTransitionActions`, `BuildDecisionActions` (كل واحدة `IEnumerable<string>` عبر `yield return`، تُزيل التداخل الذي كان يرفع التعقيد). |
| Critical: Cognitive Complexity 16→15 | `RiskReviewService.cs` | `RequestAsync` | 1 | **Fixed** — قُسِّمت إلى `NewReviewRequest`, `PrepareAcceptanceRequestAsync`, `PrepareClosureRequestAsync`. |
| Critical: Cognitive Complexity 25→15 | `BaseeraDbContext.cs` | `OnModelCreating` | 1 | **Fixed** — قُسِّمت إلى `RegisterSequences` + 7 دوال `ConfigureXQueryFilters` مجمَّعة حسب المجال (Identity/Notes/Forms/Occupancy-Resources/Workforce/SensitiveCustody/RiskManagement) + `ConfigureUserScopeCheckConstraints`. إعادة تنظيم ميكانيكية بحتة بلا أي تغيير في الموديل — تحقَّق ذلك فعليًا عبر `dotnet ef migrations has-pending-model-changes` (النتيجة: "No changes have been made to the model since the last migration"). |
| Minor: Loop should be simplified | `RiskReadinessService.cs` (كانت `BuildInterventionsAsync`، أصبحت `BuildTreatmentActionInterventionsAsync`) | حلقة `foreach` على إجراءات المعالجة المتأخرة/المعطَّلة | 1 | **Fixed** — حُوِّلت لسلسلة `Select(...).Concat(Select(...))` بدل حلقتي `foreach` تُضيفان عنصرًا واحدًا لكل تكرار. |
| Minor: Define a constant لـ"مسودة" | `RiskManagementDisplay.cs` | 4 استخدامات عبر `RiskStatus`/`AssessmentStatus`/`TreatmentPlanStatus`/`RiskTreatmentActionStatus` | 1 (finding واحد يغطي التكرار) | **Fixed** — أُضيف `internal static class RiskDisplayLabels { public const string DraftAr = "مسودة"; }`، ثابت **عرض فقط** (لا يربط دلالات الدومين الأربعة المختلفة ببعضها، فقط النص المعروض). |
| Minor: Remove null-forgiving operator | 9 ملفات كيانات دومين (`RiskAssessmentEntities`×7, `RiskCategoryEntity`×1, `RiskControlEntity`×1, `RiskImportEntities`×4, `RiskMatrixEntities`×6, `RiskRecordEntity`×3, `RiskReviewEntity`×1, `RiskSourceLinkEntity`×1, `RiskTreatmentEntities`×2) | خصائص EF navigation مطلوبة (`public T Nav {get;set;} = null!;`) | 26 | **FalsePositive candidate — لم تُصنَّف في واجهة Sonar فعليًا.** كل حالة: navigation مطلوبة (غير Nullable) مع Nullable enabled؛ EF Core يملؤها فعليًا عند التحميل؛ البناء الصحيح للتطبيق (كل خدمات RiskManagement) يستخدم حقل الـFK فقط دون تزويد الـnavigation عند الإنشاء. إزالة `null!` تُنتج `CS8618`؛ تحويلها لـ`required` يكسر كل بناء بـFK فقط. **لم يُعدَّل الكود** ولم تُستخدم `NOSONAR`/pragma. هذا التصنيف مطابق تمامًا لسابقة موثقة على `main` لنفس النمط بالضبط (انظر `docs/sonar-backlog-remediation.md` صفوف 113-116: `CorrectiveActionEntities.cs`، نفس القاعدة "EF Core navigation property" → "Confirmed FalsePositive" → "No code change"). |

**التحقق الحسابي**: 1 (Blocker) + 8 (Critical) + 1 (Loop) + 1 (مسودة) + 26 (null-forgiving) = **37** — يطابق تمامًا إجمالي SonarCloud API المسحوب فعليًا في بداية هذه الجولة. لا توجد فجوة بين الجدول والمجموع المُعلَن.

**الحالة بعد هذه الجولة**: 11 من 11 finding قابل للإصلاح الفعلي (Blocker + 8×Critical + Loop + مسودة) **أُصلحت جميعها بالكود**، بانتظار إعادة تحليل SonarCloud على الـSHA الجديد للتأكيد الرسمي. الـ26 المتبقية FalsePositive candidates موثقة، غير مُصلَحة فعليًا في واجهة Sonar (خارج نطاق ما يمكن فعله في هذه الجلسة بدون صلاحية كتابة على Sonar).

### تصحيحات دقة إضافية طُلبت هذه الجولة

| البند | الحالة | التفاصيل |
| --- | --- | --- |
| وصف صلاحيات `RiskOfficer` في `permissions-matrix.md` | Fixed | صيغت الفقرة لتفصل بوضوح بين: (1) منح الصلاحية للدور، (2) فرض `EnforceFourEyes` الفعلي على العملية، (3) `Risks.Export` كصلاحية منفصلة تمامًا لا علاقة لها بفصل المهام. أُضيف اختبار تكامل آلي جديد `RiskOfficer_permission_bundle_matches_documented_exclusions` يقارن فعليًا: إجمالي صلاحيات `Risks.*` المعرَّفة (25، عبر Reflection على `PermissionCodes`) مقابل ما هو ممنوح فعليًا لـ`RiskOfficer` في القاعدة (18) والاستثناءات السبعة بالضبط. |
| توثيق "5 من 18" نوع مصدر محقَّق (كان قد أُصلح الكود سابقًا لـ4 لكن التوثيق لم يُحدَّث) | Outdated — أُصلح | `docs/phase-d6-risk-completion-report.md` كان لا يزال يذكر "5 من 18" رغم أن الكود الفعلي (`RiskSourceLinkService.ScopeCheckedTypes`) يحتوي 4 أنواع فقط منذ الجولة الثالثة. صُحِّح للرقم الفعلي. |
| عقد تجاوز النطاق الأعلى (rating-band overflow) | Fixed (تصحيح لتصحيح سابق) | الجولة السابقة وثّقت أن النطاق الأخير "بلا حد أعلى فعلي" (unbounded)، لكن هذا لم يعد الخيار المفضَّل هذه الجولة. **العقد الجديد**: كل نطاق قبل الأخير `[Min, Max)` (نصف مفتوح)؛ النطاق الأخير `[Min, Max]` (مغلق بالكامل)؛ أي درجة تتجاوز `Max` للنطاق الأخير **تُرفض صراحة** برسالة عربية واضحة بدل قبولها ضمن النطاق الأعلى. `RiskScoringEngine.SelectRatingBand` عُدِّلت لترمي عند `score > last.MaximumScore` قبل أي محاولة مطابقة. اختبارات وحدة جديدة تغطي: درجة أقل من أول حد أدنى (رفض)، درجة تساوي أول حد أدنى (النطاق الأول)، حد التماس (النطاق الأعلى)، درجة تساوي الحد الأقصى النهائي (النطاق الأخير)، درجة تتجاوز الحد الأقصى النهائي (رفض)، ودرجة كسرية 6.99 من الصيغة الموزونة. وثائق `phase-d6-risk-matrix-versioning.md`/`phase-d6-risk-scoring.md` حُدِّثت لتطابق هذا العقد بالضبط. |

## الجولة الخامسة — منع التقييمات المتزامنة المكررة + تنظيف Sonar الجديد بعد دفع الجولة الرابعة

بعد دفع كوميتات الجولة الرابعة، أعادت SonarCloud تحليل الـSHA الجديد. سُحبت النتائج الفعلية عبر نفس الـAPI في بداية هذه الجولة (قبل أي تعديل): **28 مشكلة مفتوحة إجمالًا** (0 Blocker + 0 Critical + 1 Major + 27 Minor). كل الـBlocker/Critical من الجولة الرابعة أُغلقت فعليًا وأكّدها Sonar (لم تعد تظهر) — يؤكد أن إصلاحات الجولة الرابعة صحيحة فعليًا وليست ادّعاءً غير محقَّق.

### الجرد الكامل لهذه الجولة (Rule | File | Symbol | Count | Status)

| Rule | File | Symbol | Count | Status |
| --- | --- | --- | --- | --- |
| Major: csharpsquid:S107 — Method has 8 parameters | `RiskAssessmentService.cs:145` | `BuildAssessment` | 1 | **Fixed** — استُبدلت قائمة المعاملات الثمانية بسجلين: `ResolvedAssessmentImpact(Request, Level)` و`AssessmentBuildContext(Risk, Request, Matrix, Likelihood, ImpactLevels, Score, Band)`؛ الدالة الآن تأخذ معاملًا واحدًا فقط (`AssessmentBuildContext`). `ResolveImpactLevels` عُدِّلت لتُعيد `IReadOnlyList<ResolvedAssessmentImpact>` بدل tuple طويلة. لا منطق عمل انتقل إلى السجلّين — بناء بيانات فقط. |
| Minor: csharpsquid:S2325 — Make method static | `RiskAssessmentService.cs:358` | `ShouldUpdateCurrentView` | 1 | **Fixed** — الدالة لا تستخدم أي حالة خاصة بالـinstance (`CurrentFamily` حقل static أصلًا)؛ أُضيفت `static` إلى التوقيع. |
| Minor: csharpsquid:S8970 — Remove null-forgiving operator | نفس 9 ملفات كيانات الجولة الرابعة (`RiskAssessmentEntities`×7, `RiskCategoryEntity`×1, `RiskControlEntity`×1, `RiskImportEntities`×4, `RiskMatrixEntities`×6, `RiskRecordEntity`×3, `RiskReviewEntity`×1, `RiskSourceLinkEntity`×1, `RiskTreatmentEntities`×2) | خصائص EF navigation مطلوبة | 26 | **FalsePositive candidate — بلا تغيير عن الجولة الرابعة.** أُعيدت مراجعة كل حالة من الـ26 يدويًا هذه الجولة: كل `null!` مقترن بخاصية FK غير Nullable (`Guid`, وليس `Guid?`) مباشرة قبله، وكل navigation اختيارية في نفس الملفات (مثل `OverallImpactLevel`, `SupersedesAssessment`, `ParentCategory`, `PreviousVersionMatrix`, إلخ) مُعرَّفة فعلًا كـ`T?` بلا `null!` — لا يوجد تصنيف خاطئ (لا optional يُعامَل كـrequired، ولا العكس). لم يُعدَّل الكود. |

**التحقق الحسابي**: 1 (S107) + 1 (S2325) + 26 (S8970) = **28** — يطابق تمامًا إجمالي SonarCloud API المسحوب فعليًا في بداية هذه الجولة.

**الحالة بعد هذه الجولة**: 2 من 2 finding قابل للإصلاح الفعلي (S107 + S2325) **أُصلحا بالكود**، بانتظار إعادة تحليل SonarCloud على الـSHA الجديد للتأكيد الرسمي. الـ26 المتبقية بلا تغيير — FalsePositive candidates موثقة، غير مُصنَّفة فعليًا في واجهة Sonar.

### بنود إضافية طُلبت هذه الجولة (خارج جرد Sonar المباشر)

| Finding | Status | Fix | Test |
| --- | --- | --- | --- |
| BuildAssessment has 8 parameters | Fixed | `AssessmentBuildContext` + `ResolvedAssessmentImpact` (سجلّان خاصّان بالخدمة) بدل قائمة معاملات مسطّحة | Unit (`RiskScoringEngineTests` غير متأثرة) + build نظيف |
| ShouldUpdateCurrentView static | Fixed | إضافة `static` إلى التوقيع؛ لا تغيير سلوكي | Build + مسار اختبارات التقييم الحالية (Integration) |
| Concurrent assessment invariant | Fixed | فهرس فريد مفلتر `UX_RiskAssessments_RiskRecordId_AssessmentType_InProgress` على `(RiskRecordId, AssessmentType)` عند `IsDeleted=0 AND Status IN (0,1,2)` (migration `EnforceSingleInProgressRiskAssessment`) + ترجمة `DbUpdateException` (SQL 2601/2627 + مطابقة اسم الفهرس) إلى رسالة تعارض المجال الموجودة أصلًا في `EnsureRiskAcceptsNewAssessmentAsync` | اختبار تكامل حي حاسم (deterministic) على SQL Server فعلي: `RiskAssessmentConcurrencyIntegrationTests.Concurrent_assessment_creation_for_same_type_yields_one_success_and_one_conflict` يستخدم `SaveChangesInterceptor` (`Barrier(2)`) يوقف كلا الطلبين المتزامنين عند نقطة الحفظ بالضبط ثم يُطلقهما معًا — يثبت أن الفهرس هو الفاصل الفعلي وليس ترتيب الطلبات. بالإضافة إلى 9 اختبارات مصفوفة الحالة (`Draft`/`PendingReview`/`Reviewed` تمنع [3]، `Approved`/`Superseded`/`Rejected` تسمح [3]، نوع تقييم مختلف يُسمح، خطر مختلف يُسمح، تقييم محذوف منطقيًا (Soft-deleted) لا يمنع [3 إضافية]) |
| Draft status contradiction | Fixed | توثيق `phase-d6-review-remediation.md` كان يحتوي سطرين متناقضين لنفس finding "مسودة" (سطر يقول Fixed عبر `RiskDisplayLabels.DraftAr`، وسطر أقدم يقول رُفض عمدًا ولم يُعدَّل الكود). رُوجعت الحالة الفعلية للكود (الثابت موجود ومستخدَم في التعيينات الأربعة)، وصُحِّح السطر الأقدم ليطابق الواقع، وحُدِّثت الإجماليات المتأثرة في نفس القسم | مراجعة توثيقية (لا كود) |

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

### Critical — Cognitive Complexity (8) — **مؤجَّلة صراحة عند كتابة هذا القسم؛ أُصلحت جميعها لاحقًا في "الجولة الرابعة" أعلاه**

> هذا الجدول سجل تاريخي لقرار التأجيل الأصلي وقت هذه الجولة. الحالة الحالية: كل الـ8 أُصلحت فعليًا (انظر جدول الجرد الكامل في "الجولة الرابعة" أعلى هذا الملف)، وأكّد SonarCloud إغلاقها في السحب الفعلي لبداية "الجولة الخامسة" (لم تعد تظهر ضمن الـ28 مشكلة المفتوحة).

| File:Line | Complexity | القرار وقت هذه الجولة |
| --- | --- | --- |
| `RiskAssessmentService.cs:41` | 17 → 15 | مؤجَّل حينها |
| `RiskAssessmentService.cs:285` | 23 → 15 | مؤجَّل حينها |
| `RiskImportService.cs:113` | 26 → 15 | مؤجَّل حينها |
| `RiskReadinessService.cs:45` (`BuildInterventionsAsync`) | 42 → 15 | مؤجَّل حينها |
| `RiskRegisterQueryService.cs:103` | 25 → 15 | مؤجَّل حينها |
| `RiskRegisterQueryService.cs:353` | 29 → 15 | مؤجَّل حينها |
| `RiskReviewService.cs:36` | 16 → 15 | مؤجَّل حينها |
| `BaseeraDbContext.cs:389` | 25 → 15 | مؤجَّل حينها |

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
| Define a constant لـ"مسودة" (4 تكرارات) | 1 | **StillValid — أُصلح** (تصحيح: جولة سابقة من هذا التقرير وثّقت هذا البند خطأً كمرفوض عمدًا؛ الكود الفعلي في `RiskManagementDisplay.cs` يحتوي `internal static class RiskDisplayLabels { public const string DraftAr = "مسودة"; }` مستخدَمًا في التعيينات الأربعة كافة — انظر قسم "الجولة الرابعة" أعلاه للتفاصيل. الثابت **عرض فقط**: لا يربط دلالات الدومين المستقلة لـ`RiskStatus`/`AssessmentStatus`/`TreatmentPlanStatus`/`RiskTreatmentActionStatus` ببعضها، فقط النص العربي المعروض المتطابق مصادفة). |
| Loop simplification (`Select`) | 1 | **StillValid — أُصلح**: حُوِّلت 9 حلقات `foreach` بنمط "إضافة عنصر واحد فقط" في `RiskReadinessService.BuildInterventionsAsync` إلى `items.AddRange(...Select(...))`، مطابقًا للنمط المقترح. |

## ملخص الحالة

- **P0 (Critical)**: 2/2 مُصلَحة بالكامل ومُختبرة حيًّا (integration tests جديدة).
- **P1 (Major CodeRabbit)**: 27/27 جرى فتحها والتحقق منها؛ 26 مُصلَحة أو مُوثَّقة كتوضيح، 1 مصنَّفة FalsePositive بأدلة ملموسة (قيد DB + استحالة بنيوية).
- **Sonar Blocker**: 1/1 مُصلَح.
- **Sonar Major**: 3/3 مُصلَحة.
- **Sonar Minor** (36 إجمالًا): 10 أُصلحت فعليًا (7 ثوابت + ثابت "مسودة" المشترك + تبسيط حلقة واحد + null-forgiving فعلي واحد)، 26 مصنَّفة FalsePositive بأدلة (null-forgiving على EF navigation مطلوبة). لا يوجد بند مرفوض بقرار تصميم — التصحيح أعلاه في جدول الـMinor يوثّق أن ثابت "مسودة" نُفِّذ فعليًا في الكود، خلافًا لما ورد سابقًا في هذا القسم.
- **Sonar Critical (Cognitive Complexity)**: 0/8 عند كتابة هذا القسم (جولة سابقة) — كانت **مؤجَّلة صراحة** حينها بسبب حجم/مخاطر إعادة الهيكلة مقابل الوقت المتاح. **محدَّثة لاحقًا**: جميع الـ8 أُصلحت فعليًا في "الجولة الرابعة" أعلاه (انظر جدول الجرد الكامل) — هذا السطر مُبقًى كسجل تاريخي لقرار التأجيل الأصلي، وليس الحالة الحالية.

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
