# Phase D.5.1 Screenshots — Facility Workforce Readiness

Expected captures (RTL command-center language):

- Desktop 1440px — facility workspace `?section=workforce` with coverage rail.
- Desktop — workforce admin overview (`/facilities/{id}/workforce`).
- Desktop — members list section (`?section=members`).
- Desktop — import preview/confirm (`?section=imports`).
- Partial-data / gap state (coverage strip `data-status=partial`).
- Mobile overview of workforce admin.

## Capture notes

No Playwright screenshot scripts currently exist under `scripts/` for this phase.

Validation path used in CI / local:

```bash
cd src/frontend
npm run test -- --run src/pages/workspaces/FacilityWorkspacePage.test.tsx src/pages/workforce/FacilityWorkforcePage.test.tsx
```

Browser screenshots require a running API with seeded facility workforce data and an authenticated session. Attach captures from the PR review environment or a fully running local stack when available.

Suggested manual capture after `npm run dev` (with API up):

1. Open `/workspaces/facilities/{facilityId}?section=workforce`
2. Open `/facilities/{facilityId}/workforce`
3. Switch sections via the in-page nav (`coverage`, `members`, `imports`)
