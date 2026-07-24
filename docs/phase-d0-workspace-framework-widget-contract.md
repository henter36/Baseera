# Phase D0 Workspace Framework Widget Contract

Widget definitions include:

- Key, title, category, supported levels.
- Required permission and data capability.
- Default/min/max size.
- Refresh and freshness policies.
- Empty/error behavior.
- Drill-down/configuration/sensitive-data flags.

Widget data uses `WidgetDataEnvelopeDto`:

- Widget key.
- Generated/effective timestamps in UTC.
- Server-authored freshness and confidence.
- Scope summary.
- Partial/warnings.
- Typed payload.
- Drill-down targets.
- Allowed actions.

D0 reference payloads:

- `ReferenceOperationalSummaryPayload`
- `ReferenceCorrectiveActionsPayload`
