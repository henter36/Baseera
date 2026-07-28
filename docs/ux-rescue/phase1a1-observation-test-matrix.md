# Phase 1A.1 — Test Matrix

| Requirement | Evidence | Status |
| --- | --- | --- |
| Desktop master-detail renders list and detail together | `ObservationWorkspacePage.test.tsx` — in-page modal regression test | Verified |
| Selecting a note does not create a dialog | `ObservationWorkspacePage.test.tsx` — no `role=dialog` | Verified |
| No `aria-modal` on observation detail | `ObservationWorkspacePage.test.tsx` | Verified |
| No backdrop/overlay for observation detail | `ObservationWorkspacePage.test.tsx` | Verified |
| No body scroll lock for observation detail | `ObservationWorkspacePage.test.tsx` | Verified |
| Browser back/forward restores selected notes | `ObservationWorkspacePage.test.tsx` — history navigation test | Verified |
| Refresh/deep link restores detail | `ObservationWorkspacePage.test.tsx` — initial `noteId` tests | Verified |
| Filters are retained when closing detail | `ObservationWorkspacePage.test.tsx` — mobile back filter-context test | Verified |
| Previous/next navigation works | `ObservationWorkspacePage.test.tsx` | Verified |
| Assign/Verify remain inline | `ObservationWorkspacePage.test.tsx` existing inline action tests | Verified |
| Facility note click opens Observation Workspace | `FacilityWorkspacePage.test.tsx` — note redirect test | Verified |
| Legacy `panel=note` facility link redirects safely | `FacilityWorkspacePage.test.tsx` — legacy redirect test | Verified |
| Non-note context panels still work | `FacilityWorkspacePage.test.tsx` corrective-action/context-panel tests | Verified |

Targeted run:

```text
npm run test -- ObservationWorkspacePage.test.tsx FacilityWorkspacePage.test.tsx
Test Files: 2 passed
Tests: 44 passed
Skipped: 0
```
