# Phase B.3.1 — Dashboard API Contract

Base path: `/api/v1/dashboard/operations`

Shared query parameters: `periodDays` (7|30|90), `fromUtc`, `toUtc`, `regionId`, `facilityId`, `facilityUnitId`, `noteTypeId`, `severity`, `status`, `breakdownBy`, `queue`.

Validation: `fromUtc <= toUtc`; range ≤ 90 days.

## Endpoints

| Method | Path | Auth |
|--------|------|------|
| GET | `/summary` | Any dashboard permission |
| GET | `/trends` | `Dashboard.ViewOperational` |
| GET | `/breakdowns` | `Dashboard.ViewOperational` + `breakdownBy` required |
| GET | `/priority-queues` | Section permissions per queue |

## KPI definitions

### Workload (open notes: not Closed/Cancelled)

| KPI | Definition |
|-----|------------|
| openTotal | Count open notes |
| assigned | Status = Assigned |
| inProgress | Status = InProgress |
| pendingVerification | Status = PendingVerification |
| reopened | Status = Reopened |
| unassigned | Open + no current assignment |
| requiresRouting | Open/Reopened + no assignment + failed/no routing decision |

### Risk

| KPI | Definition |
|-----|------------|
| overdue | `DueAtUtc < now` and not terminal |
| dueSoon | Due within 7 days (default), not terminal |
| criticalOrHigh | Severity High/Critical and open |
| overdueUnassigned | Overdue + unassigned |
| activeEscalations | Occurrences with NotificationsCreated in period, in scope |
| routingFailure* | Decisions in period by failure status |

### Corrective actions

| KPI | Definition |
|-----|------------|
| active | Not Completed/Cancelled |
| overdue | CA overdue per state machine |
| pendingVerification | Status PendingVerification |
| reopened | Status Reopened |
| notesWithStalledActions | Open note with ≥1 overdue CA |

### Breakdown row fields

openBurden, overdue, critical, unassigned, correctiveActionsOverdue, closureRateWithinDue = closedWithinDue / closedInPeriod.

### Priority queues (limit 10)

mostOverdueNotes, criticalUnassignedNotes, topOverdueLocations, mostOverdueCorrectiveActions, recentRoutingFailures.

## Drill-down mapping

| KPI | List URL |
|-----|----------|
| overdue | `/notes?overdueOnly=true` |
| dueSoon | `/notes?dueSoonDays=7` |
| unassigned | `/notes?unassignedOnly=true` |
| requiresRouting | `/notes?requiresRouting=true` |
| CA overdue | `/corrective-actions?overdueOnly=true` |

Trend buckets use Riyadh calendar day boundaries (`Asia/Riyadh`).
