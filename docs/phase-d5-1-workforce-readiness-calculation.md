# Phase D.5.1 Workforce Readiness Calculation

Calculations live in `WorkforceReadinessPolicy` (coverage), `WorkforceFatiguePolicy` (indicators), and `WorkforceReadinessService` (aggregation).

Coverage (`Calculate`):

- Without requirement baseline → status `Unknown`, null rates, zero gaps.
- `Gap = max(0, Required - OperationallyAvailable)` when `Required > 0`.
- `SafeGap = max(0, MinimumSafe - OperationallyAvailable)`.
- `CoverageRate = OperationallyAvailable / Required` (4 d.p.) when `Required > 0`.
- `QualificationCoverage = Qualified / (Qualified + Unqualified)` when denominator > 0.
- Status thresholds (when an operational signal exists): `≥1.0` Ready, `≥0.85` Attention, `≥0.70` Critical, else Unsafe.

Operationally available (facility summary): employment eligible and not blocked by availability. Blocking types (when `AffectsOperationalAvailability`): AnnualLeave, SickLeave, Training, ExternalAssignment, Suspended, EmergencyLeave, UnexcusedAbsence. `RestrictedDuty` counts as restricted but is not in the blocking set unless combined with other blocking types.

Role/unit coverage rows prefer published roster present counts; otherwise scheduled counts feed availability.

Freshness: `missing` (no members), `partial` (any stale verification), else `current`. Confidence: `unknown` / `medium` (warnings present) / `high`.

Fatigue indicators (deterministic strings, not predictions): `ExcessiveOvertimeHours` (≥12h), `ConsecutiveShiftsWithoutRest` (≥3), `SinglePointOfFailure` (critical role coverage ≤1 when required), `QualificationExpiringSoonDays` (within 30 days).

No AI, no automated shift optimization.
