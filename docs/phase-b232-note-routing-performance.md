# Phase B.2.3.2 — Performance

Routing resolution uses server-side filtering by note type and active state, then deterministic in-memory ordering on the bounded candidate rule set.

Role target selection batches:
- Role membership.
- User permissions.
- User scopes.
- Effective note type access.
- Active assignment workload.

The intended query count remains bounded as candidate users grow. Formal 1,000-user and 1,000-rule performance dataset execution remains a residual validation item for CI hardening.
