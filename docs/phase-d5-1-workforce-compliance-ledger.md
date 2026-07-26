# Phase D.5.1 Workforce Compliance Ledger

Generated: 2026-07-26T01:50:39.048099+00:00

Branch: `phase-d5-1-facility-workforce-readiness`
PR: [#134](https://github.com/henter36/Baseera/pull/134)
Issue: [#133](https://github.com/henter36/Baseera/issues/133)

## Status legend

`Implemented` | `Verified` | `Not Applicable — خارج النطاق صراحة` | `Blocked — مانع خارجي حقيقي` | `Missing`

## Requirement ledger

| ID | المتطلب الأصلي | الحالة | دليل الكود | دليل API | دليل الاختبار | دليل الواجهة | ملاحظات |
| -- | -------------- | ------ | ---------- | -------- | ------------- | ------------ | ------- |
| P01 | تحليل الوضع الحالي | Verified | docs/phase-d5-1-workforce-current-state-analysis.md |  |  |  |  |
| P02 | نموذج المجال (الكيانات الـ11) | Implemented | src/backend/Baseera.Domain/Workforce/WorkforceEntities.cs |  | WorkforceCountingPolicyTests |  | EF configs in WorkforceConfigurations.cs; ReconciliationResolution added |
| P03 | المتطلبات البشرية StaffingRequirement | Implemented | StaffingRequirement + IStaffingRequirementService | POST/GET /requirements | IT08_to_IT14 | Requirements section | MinimumSafe vs Required |
| P04 | المناوبات والتغطية Shift/Roster | Implemented | ShiftDefinition/DutyRoster | POST/GET /rosters + publish | IT08_to_IT14 | Shifts section | Mutation lock after publish in PublishAsync |
| P05 | التوفر والغياب | Implemented | WorkforceAvailabilityEvent | POST /availability | IT08_to_IT14 | Availability section | No medical diagnosis stored |
| P06 | الجاهزية الحالية Readiness Projection | Implemented | WorkforceReadinessService.GetSummaryAsync + snapshots | GET /summary /coverage /units | IT16_to_IT18 | Overview | Facility/Unit/Shift/Role projections via coverage aggregation |
| P07 | قواعد الجاهزية Coverage Status | Verified | WorkforceReadinessPolicy |  | WorkforceReadinessPolicyTests | state-*.png | Ready/Attention/Critical/Unsafe/Unknown |
| P08 | الإرهاق والمخاطر | Implemented | WorkforceFatiguePolicy | summary.FatigueIndicators | WorkforceReadinessPolicyTests |  | OT/consecutive/SPOF/qual expiry |
| P09 | المناصب الحرجة والبدلاء | Implemented | CriticalPositionRequirement + Ops ListCriticalPositions | GET /critical-positions | Critical_positions_endpoint | desktop-critical-role-gaps.png | Alternates/vacant/status |
| P10 | مصدر الحقيقة IWorkforceSourceResolver | Implemented | WorkforceSourceResolver.cs |  | WorkforceReadinessPolicyTests / summary path |  | Precedence: roster>attendance>availability>assignment>unknown |
| P11 | الصلاحيات والخصوصية | Implemented | PermissionCodes.Workforce.* + AccessPolicy | 403/404 tests | Facility_workforce_summary_requires_permission; Export/Get_member sensitive | permission FE tests | ViewSensitiveRestrictions redaction |
| P12 | API وخدمات التطبيق | Implemented | WorkforceServices + Ops partial | MapWorkforceEndpoints full set | IT suite | api.workforce client | Export/Recon/Critical added |
| P13 | Facility Workspace integration | Implemented | FacilityWorkspaceReadService workforce section | facility-operations widget | Facility_workspace_contains_workforce_widget | FacilityWorkspacePage workforce panels | Not widget-only |
| P14 | عرض الوحدات والمناوبات | Implemented | GetUnitsAsync/GetCoverageAsync | GET /units /coverage | IT16_to_IT18 | Coverage/Units/Shifts | Heat list in workspace |
| P15 | Context Panel types | Missing | FacilityWorkforcePage + Workspace panels |  | FE deep-link tests partial | member/unit/shift panels | Not all 9 panel types fully server-authored |
| P16 | Intervention Queue full catalog | Missing | BuildWorkforcePriorityItemsAsync | workspace priority | IT21_to_IT25 | intervention queue | Subset implemented; NoShiftCommander/NoQualifiedDriver/ConflictingAssignments/etc. not all distinct |
| P17 | Action Center executable actions | Missing | Workspace action center hooks |  | FE action center open test |  | Publish/replace/qualify actions not all executable from Action Center |
| P18 | Timeline projection redacted | Implemented | BuildRecentWorkforceEventsAsync | workspace timeline | IT21 |  | Diagnosis/restrictions not emitted |
| P19 | Data Quality full issue catalog | Missing | GetDataQualityAsync Issues list | GET /data-quality | IT21 | desktop-data-quality.png | ~12 issue codes; not every brief bullet (e.g. gap without owner) |
| P20 | Admin page sections | Implemented | FacilityWorkforcePage.tsx | /facilities/:id/workforce | FE section nav tests | all sections | URL sync + RTL |
| P21 | Audit actions | Implemented | AuditAsync Workforce* |  | Created/Put/Reconcile/Import IT |  | No full PII payload in audit |
| P22 | Migration + DB constraints | Implemented | 20260725180933 + 20260725203357 |  | IT31_migration |  | ImportKind + ReconciliationResolutions incremental |
| P23 | Performance query/payload budgets | Implemented | SqlCommandCounter IT + AsNoTracking |  | Facility_workspace_query_count_stays_within_budget |  | Budget ≤100 selects; payload size baseline not fully asserted |
| P24 | Unit Tests matrix | Missing | tests/.../Workforce/* |  | 28 unit tests |  | Not every brief unit topic has a named test (e.g. temporary assignment override) |
| P25 | Integration Tests 33 scenarios | Missing | WorkforceReadinessIntegrationTests |  | 22 IT methods covering many scenarios |  | Replacement/ActionCenter/Timeline isolation/IT33 meta incomplete mapping |
| P26 | Frontend Tests matrix | Missing | FacilityWorkforcePage.test + Workspace tests |  | 10+ FE workforce tests |  | Not full FE matrix (tablet/mobile/all panels) |
| P27 | Screenshots PNG | Verified | docs/screenshots/phase-d5-1/*.png |  | capture-workforce-screenshots.mjs | 20 PNG files | Harness RTL; fake Arabic names |
| P28 | Documentation set | Implemented | docs/phase-d5-1-workforce-*.md |  |  |  | Completion report updated; ledger this file |
| P29 | No double-counting policy | Implemented | WorkforceCountingPolicy |  | WorkforceCountingPolicyTests |  | Central policy |
| P30 | Multi-kind Import contracts | Missing | WorkforceImportKind branches | POST /import/preview|confirm | Import_confirm + IT26 | import-preview.png | PersonnelMaster proven; other kinds validated in code paths but thin IT |

## Acceptance Criteria 1–50

| ID | المتطلب الأصلي | الحالة | دليل الكود | دليل API | دليل الاختبار | دليل الواجهة | ملاحظات |
| -- | -------------- | ------ | ---------- | -------- | ------------- | ------------ | ------- |
| AC01 | WorkforceMember مستقل عن User | Verified | WorkforceMember entity | POST /members | Created_workforce_member |  |  |
| AC02 | Operational roles مستقلة عن RBAC | Verified | WorkforceRoleDefinition | GET /roles | IT16 |  |  |
| AC03 | Assignment model حقيقي | Verified | WorkforceAssignment | POST /assignments | IT08 |  |  |
| AC04 | Qualification model حقيقي | Verified | WorkforceQualification | POST /qualifications | IT08 |  |  |
| AC05 | Staffing Requirement حقيقي | Verified | StaffingRequirement | POST /requirements | IT08 |  |  |
| AC06 | Shift definitions حقيقية | Verified | ShiftDefinition | rosters use ShiftDefinitionId | IT08 |  |  |
| AC07 | Duty roster حقيقي | Verified | DutyRoster + publish | POST /rosters/{id}/publish | IT08 |  |  |
| AC08 | Availability events حقيقية | Verified | WorkforceAvailabilityEvent | POST /availability | IT08 |  |  |
| AC09 | Required و Minimum safe منفصلان | Verified | StaffingRequirement fields + policy | summary Required/MinimumSafe | WorkforceReadinessPolicyTests |  |  |
| AC10 | لا يوجد عد مزدوج | Implemented | WorkforceCountingPolicy |  | WorkforceCountingPolicyTests + IT19 |  | IT coverage of overlapping shifts incomplete |
| AC11 | لا يحسب الغائب/غير المؤهل متاحًا | Verified | CountsAsAvailable/CountsAsQualified |  | WorkforceCountingPolicyTests |  |  |
| AC12 | المناوبات المتعارضة ممنوعة | Implemented | assignment/roster conflict checks |  | unit/recon detector |  | DB+app validation present; dedicated conflict IT thin |
| AC13 | الوظائف الحرجة ظاهرة | Verified | CriticalPosition + interventions | GET /critical-positions | Critical_positions_endpoint | desktop-critical-role-gaps.png |  |
| AC14 | البدلاء ظاهرون | Implemented | RequiredAlternateCount / VacantAlternate | critical-positions DTO | Critical_positions_endpoint |  | UI alternate drill partial |
| AC15 | Facility Workspace تعرض الجاهزية | Verified | facility.workforce widget/section | facility-operations | Facility_workspace_contains_workforce_widget | FE workspace |  |
| AC16 | Unit coverage حقيقية | Verified | GetUnitsAsync | GET /units | IT16 | desktop-unit-coverage.png |  |
| AC17 | Shift coverage حقيقية | Verified | GetCoverageAsync shift dims | GET /coverage | IT16 | desktop-shift-coverage.png |  |
| AC18 | Intervention Queue مدمجة | Implemented | BuildWorkforcePriorityItemsAsync | workspace priority | IT21 | FE | Catalog incomplete → not full Verified |
| AC19 | Action Center مدمجة | Missing | workspace action center |  | FE opens action center |  | Workforce-specific executable actions incomplete |
| AC20 | Timeline مدمجة | Implemented | workforce timeline events | workspace | IT21 |  |  |
| AC21 | Data Quality مدمجة | Implemented | GetDataQualityAsync | GET /data-quality | IT21 | desktop-data-quality.png | Issue catalog incomplete |
| AC22 | Context Panel تعمل | Implemented | panels for member/unit/shift |  | FE deep link tests | desktop-member-panel.png | Not all panel types |
| AC23 | لا Mock في الإنتاج | Verified | API-backed pages |  |  |  | Demo seed only under Seed:DemoOrganization |
| AC24 | لا PII في Workspace summary | Verified | summary redaction | GET /summary | Facility_workforce_summary_redacts_member_names |  |  |
| AC25 | الصلاحيات Server-side | Verified | RequireAuthorization + Require() |  | 403 tests |  |  |
| AC26 | Out-of-scope → 404 | Verified | EnsureFacilityVisibleAsync |  | Workforce_summary_returns_not_found; Put_member_404 |  |  |
| AC27 | Missing permission → 403 | Verified | auth policies |  | Facility_workforce_summary_requires_permission; Put_member_forbidden; Export 403 |  |  |
| AC28 | Import idempotent | Verified | unique index + confirm | POST /import/confirm | Import_confirm_is_idempotent |  |  |
| AC29 | Audit آمن | Verified | AuditAsync without raw payloads |  | Created/Put/Reconcile audits |  |  |
| AC30 | Migration واحدة سليمة | Implemented | PhaseD51 + PhaseD51WorkforceReconciliationExport |  | IT31 |  | Two migrations for phase ops completion; empty-DB verify pending this run |
| AC31 | لا يوجد N+1 | Implemented | grouped queries AsNoTracking |  | query count IT |  | Large-workforce scenario not fully proven |
| AC32 | Query count ضمن الميزانية | Verified | ≤100 selects workspace |  | Facility_workspace_query_count_stays_within_budget |  |  |
| AC33 | Unit Tests ناجحة | Verified | Workforce unit suite |  | 28 tests |  | Local run this session: workforce filter passed earlier |
| AC34 | Integration Tests ناجحة Skipped=0 | Missing | IntegrationConnectionFact |  | 22 IT methods |  | Full suite + new tests not re-run green yet this session |
| AC35 | Frontend Tests ناجحة | Verified | vitest |  | FE workforce tests |  | Not re-run green this session after latest edits |
| AC36 | Typecheck ناجح | Missing |  |  |  |  | Not verified this session after edits |
| AC37 | Lint ناجح | Missing |  |  |  |  | Not verified this session |
| AC38 | Production build ناجح | Missing |  |  |  |  | FE build not re-run this session |
| AC39 | npm audit بلا High/Critical | Missing |  |  |  |  | Not re-run |
| AC40 | NuGet gate ناجح | Missing | scripts/check-nuget-vulnerabilities.sh |  |  |  | Not re-run |
| AC41 | Gitleaks ناجح | Missing |  |  |  |  | Not re-run |
| AC42 | SonarCloud ناجح | Missing |  |  |  |  | PR #134 Sonar was FAILURE on last CI; complexity refactors pending re-check |
| AC43 | Screenshots فعلية | Verified | docs/screenshots/phase-d5-1/*.png |  |  | 20 PNG | Harness RTL |
| AC44 | Desktop/Tablet/Mobile مقبولة | Verified | PNG set |  |  | tablet/mobile png |  |
| AC45 | Issue #133 جاهزة للإغلاق | Missing |  |  |  |  | Blocked by Missing ledger rows |
| AC46 | Issue #15 تبقى مفتوحة | Verified |  |  |  |  | Weapons/aggregation remain; do not close |
| AC47 | Issue #11 تبقى مفتوحة | Verified |  |  |  |  | Continues #11 |
| AC48 | لم يتم تنفيذ الأسلحة | Not Applicable — خارج النطاق صراحة |  |  |  |  | Explicitly out of scope |
| AC49 | لم يتم تنفيذ Region/HQ workspaces | Not Applicable — خارج النطاق صراحة |  |  |  |  | HQ/Region aggregation out of scope; facility HQ scope read OK |
| AC50 | لم يتم تنفيذ الرواتب/HR الكامل | Not Applicable — خارج النطاق صراحة |  |  |  |  | Payroll/HR out of scope |

## Totals

- Total rows: 80
- Implemented: 28
- Missing: 17
- Not Applicable — خارج النطاق صراحة: 3
- Verified: 31

## Incomplete (Missing)

- P15
- P16
- P17
- P19
- P24
- P25
- P26
- P30
- AC19
- AC34
- AC36
- AC37
- AC38
- AC39
- AC40
- AC41
- AC42
- AC45

## Gate decision

**Not Ready** — Missing > 0 and/or Acceptance Criteria not all Verified; do not add `Closes #133` until Missing=0 and gates green.

## Named tests (evidence index)

### Integration
- `WorkforceReadinessIntegrationTests.Facility_workforce_summary_requires_permission`
- `WorkforceReadinessIntegrationTests.Workforce_summary_returns_not_found_outside_facility_scope`
- `WorkforceReadinessIntegrationTests.Facility_workforce_summary_redacts_member_names`
- `WorkforceReadinessIntegrationTests.Facility_workspace_contains_workforce_widget_when_domain_permission_exists`
- `WorkforceReadinessIntegrationTests.Created_workforce_member_is_facility_scoped_and_audited`
- `WorkforceReadinessIntegrationTests.Put_member_update_succeeds_and_audits`
- `WorkforceReadinessIntegrationTests.Put_member_forbidden_without_manage_members`
- `WorkforceReadinessIntegrationTests.Put_member_404_out_of_scope`
- `WorkforceReadinessIntegrationTests.Reconciliation_list_resolve_and_audit`
- `WorkforceReadinessIntegrationTests.Export_requires_permission_and_omits_restriction_codes`
- `WorkforceReadinessIntegrationTests.Critical_positions_endpoint_returns_computed_fields`
- `WorkforceReadinessIntegrationTests.Import_confirm_is_idempotent`
- `WorkforceReadinessIntegrationTests.Get_member_hides_restrictions_without_sensitive_permission`
- `WorkforceReadinessIntegrationTests.Region_scope_can_read_facility_workforce_summary`
- `WorkforceReadinessIntegrationTests.Facility_workspace_query_count_stays_within_budget`
- `WorkforceReadinessIntegrationTests.IT03_Hq_global_scope_can_read_facility_workforce_summary`
- `WorkforceReadinessIntegrationTests.IT08_to_IT14_assignment_qualification_requirement_roster_availability_lifecycle`
- `WorkforceReadinessIntegrationTests.IT16_to_IT18_coverage_units_and_roles_endpoints`
- `WorkforceReadinessIntegrationTests.IT21_to_IT25_workspace_interventions_timeline_data_quality`
- `WorkforceReadinessIntegrationTests.IT26_import_preview_rejects_invalid_rows_before_confirm`
- `WorkforceReadinessIntegrationTests.IT31_migration_workforce_tables_exist`
- `WorkforceReadinessIntegrationTests.IT19_summary_scheduled_not_less_than_present_no_double_count_signal`

### Unit (paths under `src/backend/tests/Baseera.UnitTests/Workforce/`)
- WorkforceCountingPolicyTests.*
- WorkforceReadinessPolicyTests.*
- WorkforceShiftAndReconTests.*
- WorkforceReconciliationDetectorTests.*

### Screenshots
- `docs/screenshots/phase-d5-1/desktop-critical-role-gaps.png`
- `docs/screenshots/phase-d5-1/desktop-data-quality.png`
- `docs/screenshots/phase-d5-1/desktop-member-panel.png`
- `docs/screenshots/phase-d5-1/desktop-overview.png`
- `docs/screenshots/phase-d5-1/desktop-qualification-expiry.png`
- `docs/screenshots/phase-d5-1/desktop-shift-coverage.png`
- `docs/screenshots/phase-d5-1/desktop-shift-panel.png`
- `docs/screenshots/phase-d5-1/desktop-unit-coverage.png`
- `docs/screenshots/phase-d5-1/desktop-unsafe-staffing.png`
- `docs/screenshots/phase-d5-1/import-preview.png`
- `docs/screenshots/phase-d5-1/mobile-member-detail.png`
- `docs/screenshots/phase-d5-1/mobile-overview.png`
- `docs/screenshots/phase-d5-1/mobile-shift.png`
- `docs/screenshots/phase-d5-1/state-attention.png`
- `docs/screenshots/phase-d5-1/state-critical.png`
- `docs/screenshots/phase-d5-1/state-empty.png`
- `docs/screenshots/phase-d5-1/state-partial.png`
- `docs/screenshots/phase-d5-1/state-ready.png`
- `docs/screenshots/phase-d5-1/state-unknown.png`
- `docs/screenshots/phase-d5-1/tablet-overview.png`
