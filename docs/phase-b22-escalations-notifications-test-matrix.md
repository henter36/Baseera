# Phase B.2.2 Test Matrix

## Unit

- DueSoon.
- Overdue.
- Due today in Riyadh is not overdue.
- TargetCycleKey.
- OccurrenceKey.
- DeduplicationKey.
- Policy and rule validators.
- Lease expiry takeover.

## Integration

- DueSoon note creates one InApp notification.
- Running the processor twice does not duplicate occurrence or notification.
- Notification owner-only access.
- Mark read and archive with RowVersion.
- Closed/cancelled notes are not escalated.

## Frontend

- Inbox empty state and retry.
- Mark read.
- Mark all read.
- Archive.
- Shell unread counter for users with `Notifications.ViewOwn`.

## Counts

- Unit: 274 passed.
- Integration: 70 passed.
- Frontend: 94 passed.
- Skipped: 0.
- Failed: 0.
