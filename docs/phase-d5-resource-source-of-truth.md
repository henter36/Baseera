# Phase D.5 Resource Source Of Truth

The authoritative record for resource identity is `ResourceAsset`.

Source priority:

1. Approved source/system record represented by `SourceType` and `SourceReference`.
2. Current `ResourceAsset` state for summary counts.
3. Latest `ResourceStatusEvent` for traceability.
4. Active `ResourcePlacement` for operational location.
5. Active `ResourceRequirement` for readiness and gap denominator.

Latest timestamp alone is not source authority. Missing verification or missing requirement baseline lowers confidence and appears in Data Quality.
