# Phase B.2.2 Runbook

## Worker Settings

```json
"Escalations": {
  "Enabled": false,
  "IntervalSeconds": 300,
  "BatchSize": 100,
  "LeaseSeconds": 60,
  "MaximumAttempts": 3,
  "RetryBaseSeconds": 60
}
```

`Enabled=false` is the safe default. Enable only after migrations and seed are applied.

## Manual Run

Use `POST /api/v1/escalations/run` with `Escalations.Run`. It uses the same processor and lease path as the worker.

## Monitoring

Check:

- `EscalationOccurrences` by status.
- `Notifications` by recipient/status.
- `NotificationDeliveryAttempts` by status and `NextRetryAtUtc`.
- `BackgroundJobLeases` for expired leases.

## Retry

Use `POST /api/v1/escalations/occurrences/{id}/retry` with `Escalations.RetryFailed`.

## Rollback

Disable worker first, then roll back application deployment. Database rollback should be done only in a controlled test or rollback window.

Logs must not include secrets, tokens, stack traces in delivery safe messages, or full sensitive content.
