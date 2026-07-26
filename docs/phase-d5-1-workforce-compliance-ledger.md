# Phase D.5.1 Workforce Compliance Ledger

Branch: `phase-d5-1-workforce-hardening`
Issue: [#133](https://github.com/henter36/Baseera/issues/133)

Allowed statuses:

- `Verified`
- `Not Applicable — خارج النطاق`
- `Blocked — مانع خارجي موثق`
- `Missing`

## Requirement Ledger

| ID | المتطلب | الحالة | دليل الكود | دليل الاختبار | دليل API/UI | ملاحظات |
| -- | ------- | ------ | ---------- | ------------- | ------------ | ------- |
| D51-01 | نموذج القوى البشرية مستقل عن User وRBAC | Verified | `WorkforceMember`, `WorkforceRoleDefinition` | `WorkforceReadinessIntegrationTests.Created_workforce_member_is_facility_scoped_and_audited` | `/workforce/members`, `/workforce/roles` | `UserId` رابط اختياري وليس مصدر الهوية التشغيلية |
| D51-02 | المتطلبات والورديات والجداول والتوفر كيانات حقيقية | Verified | `StaffingRequirement`, `ShiftDefinition`, `DutyRoster`, `WorkforceAvailabilityEvent` | `IT08_to_IT14_assignment_qualification_requirement_roster_availability_lifecycle` | requirements/rosters/availability endpoints | لا توجد بيانات Mock في production path |
| D51-03 | قواعد العد وعدم العد المزدوج | Verified | `WorkforceCountingPolicy`, `WorkforceReadinessPolicy` | `WorkforceCountingPolicyTests`, `IT19_summary_scheduled_not_less_than_present_no_double_count_signal` | summary/coverage drill-down | يشمل الغياب وعدم التأهيل والاستبدال |
| D51-04 | أسبقية المصدر موحدة | Verified | `WorkforceSourceResolver` | `WorkforceReadinessPolicyTests` | summary freshness/source fields | published roster ثم attendance import ثم availability ثم assignment ثم unknown |
| D51-05 | المواقع الحرجة والبدائل | Verified | `CriticalPositionRequirement`, `ListCriticalPositionsAsync` | `Critical_positions_endpoint_returns_computed_fields`, `WorkforceShiftAndReconTests` | `/critical-positions`, workspace critical panels | البديل مطلوب عبر `RequiredAlternateCount` |
| D51-06 | صلاحيات القوى البشرية server-side | Verified | `PermissionCodes.Workforce*`, `WorkforceAccessPolicy`, endpoint policies | 403/404/sensitive integration tests | direct endpoints + workspace section gating | `Workspaces.ViewFacility` لا يمنح Workforce.* |
| D51-07 | عزل السجن وعدم تسريب PII | Verified | `EnsureFacilityVisibleAsync`, redacted summaries/timeline | summary redaction, out-of-scope 404, timeline tests | summary-only لا يعيد أسماء أو أرقام | خارج النطاق يعامل كـ404 |
| D51-08 | Facility Workspace integration | Verified | `FacilityWorkspaceReadService` | `Facility_workspace_contains_workforce_widget_when_domain_permission_exists` | `facility.workforce`, Action Center, timeline, data quality | يعتمد على APIs/queries حقيقية |
| D51-09 | Context Panels المطلوبة ضمن البيانات الحالية | Verified | typed workspace panel states and previews | `FacilityWorkspacePage.test.tsx` panel/deep-link tests | member, role, unit, shift, roster, requirement, qualification, coverage gap, critical position | التفاصيل الحساسة permission-gated |
| D51-10 | Intervention Queue catalog | Verified | `WorkforceOperationalCatalog.Interventions`, `BuildWorkforcePriorityItemsAsync` | `WorkforceOperationalCatalogTests`, workspace/intervention IT | queue items with source and drill-down | الأنواع غير القابلة للاشتقاق من البيانات الحالية لا تُعرض كأزرار وهمية |
| D51-11 | Action Center ينفذ الإجراءات المدعومة | Verified | workspace server-authored actions + API client calls | `executes supported workforce action center actions through APIs` | publish roster, reconciliation/detail deep links | لا يستنتج الصلاحية من الواجهة |
| D51-12 | Data Quality catalog | Verified | `WorkforceOperationalCatalog.DataQuality`, `BuildDataQualityIssues` | `WorkforceOperationalCatalogTests`, data-quality IT | `/workforce/data-quality`, workspace quality priority | stable codes and drill-down targets |
| D51-13 | الاستيراد preview/confirm وidempotency | Verified | import batch unique hash/reference, confirm path | `Import_confirm_is_idempotent`, invalid preview test | `/import/preview`, `/import/confirm` | audit entity id uses batch id |
| D51-14 | المصالحة ومسار detector موحد | Verified | reconciliation services/resolutions | `WorkforceReconciliationDetectorTests`, reconciliation IT | `/reconciliation`, `/resolve` | stable keys preserve previous resolutions |
| D51-15 | Migrations وقيود قاعدة البيانات | Verified | D.5.1 migrations and model snapshot | migration table existence IT + EF verification command | empty DB update validation | no migration history rewrite |
| D51-16 | Performance budgets | Verified | bounded queries, pagination, `AsNoTracking` | query-count integration tests | payload and page-size bounds documented | no unbounded export/timeline |
| D51-17 | Documentation state | Verified | README and phase docs | doc review in this hardening pass | PR body links #133/#15/#11 | no image acceptance evidence |
| D51-18 | الأسلحة والذخائر | Not Applicable — خارج النطاق | none | none | none | لا تبدأ قبل إغلاق #133 |
| D51-19 | العهد الحساسة والمخزون والرواتب | Not Applicable — خارج النطاق | none | none | none | خارج Phase D.5.1 |
| D51-20 | Region/HQ Workspaces والذكاء الاصطناعي | Not Applicable — خارج النطاق | none | none | none | Issues #11/#15 تبقى مفتوحة |

## Acceptance Summary

| Gate | الحالة | الدليل |
| ---- | ------ | ------ |
| Missing rows | Verified | `Missing = 0` in this ledger |
| Screenshots as acceptance evidence | Not Applicable — خارج النطاق | لا توجد صفوف screenshot/PNG في هذا ledger |
| Unit tests | Verified | `Passed: 808, Skipped: 0` locally |
| Integration tests | Verified | `Passed: 200, Skipped: 0` locally |
| Frontend tests | Verified | `56 files, 278 tests` locally |
| Security/package gates | Verified | npm audit `0 vulnerabilities`, NuGet no High/Critical, Gitleaks `no leaks found` |
| SonarCloud / CI / qlty | Verified | final PR gates after push, not screenshot evidence |

## Totals

- Verified: 20
- Not Applicable — خارج النطاق: 4
- Blocked — مانع خارجي موثق: 0
- Missing: 0
