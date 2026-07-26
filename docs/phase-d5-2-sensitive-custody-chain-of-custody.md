# Chain Of Custody

`CustodyTransaction` is append-only operational history. Approved transactions are never physically deleted. Corrections use `CorrectionOfTransactionId` or reversal status.

Rules implemented:

- one active custody row per weapon through filtered unique index;
- issue destinations required for member/unit/armory flows;
- return/transfer transitions require rowversion;
- four-eyes approval blocks self-approval;
- current custody changes only after completion policy accepts the transition.

Audit events store action, entity type, entity id, and safe reason only.
