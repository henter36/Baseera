# Phase D.5.2 Current State Analysis

| المجال | الموجود حاليًا | قابل لإعادة الاستخدام | الفجوة | قرار التنفيذ |
| ------ | -------------- | --------------------- | ------ | ------------ |
| Resources | `ResourceAsset`, placement, maintenance, requirements, import, readiness | patterns for facility scope, readiness, workspace payloads | weapons cannot be ordinary assets because serial/location/custody are sensitive | create independent `Baseera.Domain.SensitiveCustody` |
| Workforce | members, assignments, qualifications, availability, restrictions | eligibility checks by member/facility/qualification/availability | no weapon-specific custody eligibility | add `SensitiveCustodyEligibilityPolicy` and service-level checks |
| Workspace | widget registry, priority queue, data quality, context panels | `facility-operations` widget/provider patterns | no sensitive custody section/widget | add `facility.sensitive-custody` widget and safe frontend section |
| Audit | `IAuditLogService` with scoped events | safe mutation logging | raw serial/location must never be logged | audit only entity id/type/action metadata |
| EF | SQL Server, migrations, rowversion, filtered indexes | configuration conventions | no sensitive custody tables/constraints | migration `PhaseD52SensitiveCustodyReadiness` |

Evidence: code under `src/backend/Baseera.Domain/SensitiveCustody`, `src/backend/Baseera.Application/SensitiveCustody`, `src/backend/Baseera.Infrastructure/Persistence/Configurations/SensitiveCustodyConfigurations.cs`, `src/backend/Baseera.Api/Endpoints/ApiEndpoints.cs`, and `src/frontend/src/pages/workspaces/FacilityWorkspacePage.tsx`.
