# Phase D.5 Resource Readiness Calculation

Readiness is calculated in `ResourceReadinessPolicy`.

Definitions:

- Operational = Available + InUse + Standby + Reserved.
- Available = Available + Standby.
- Retired and Transferred are excluded from the operational availability denominator.
- UnderMaintenance, OutOfService, AwaitingParts, Lost, and Unknown are not operational.
- Gap = max(0, Required - Operational).
- Surplus = max(0, Operational - Required).
- Readiness rate is null when no requirement baseline exists; it is never displayed as 100% by default.

The calculation is deterministic and covered by unit tests.
