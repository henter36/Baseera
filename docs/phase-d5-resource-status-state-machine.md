# Phase D.5 Resource Status State Machine

Supported statuses: Available, InUse, Standby, Reserved, UnderInspection, UnderMaintenance, OutOfService, AwaitingParts, Lost, Transferred, Retired, Unknown.

Rules:

- Retired assets do not return to operational statuses without a future reactivation workflow.
- UnderMaintenance and AwaitingParts require a documented reason or maintenance context.
- Lost, Transferred, Retired, and Unknown do not count as operationally ready.
- Status changes write a `ResourceStatusEvent`; the current state remains on `ResourceAsset` for efficient reads.
