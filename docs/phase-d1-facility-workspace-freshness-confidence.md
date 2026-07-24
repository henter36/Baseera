# Phase D.1 Freshness And Confidence

Freshness is authored by the server using `WorkspaceContractFactory`.

- Current: data effective within five minutes.
- Delayed: data older than five minutes.
- Stale: data older than thirty minutes.
- Partial: widget was produced with warnings.
- Unknown: no data timestamp.

Confidence:
- High: required sources are available and internally consistent.
- Medium: no form targets exist, or the source is intentionally limited such as Recent Activity or alerts gap.
- Low/Unknown are reserved for future source inconsistencies.

The frontend only displays server-provided freshness/confidence.

