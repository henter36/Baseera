# Phase C.4 — Security

## Controls verified
- Facility/region scope → 404 (not 403) for IDOR
- Respondent cannot review/approve own response (SoD)
- Deny-overrides-allow via form grants; permission codes server-side
- Attachments bound to FormResponse entity + scope
- Sensitive fields redacted without capability; no raw answers in AuditLog
- RowVersion/DraftVersion conflicts; mutation idempotency
- Calculated/read-only writes rejected; regex timeouts; payload size limits
- Self-approval and duplicate level approval blocked

## Manual review checklist
IDOR, scope bypass, facility/region/campaign mismatch, submission/response mismatch, attachment reuse, sensitive leakage, mutation replay, double submit/approve, level skipping, hidden required, calculated tampering, oversized payload, repeating-table abuse, invalid org refs.
