# Phase 1A.1 — Compliance Ledger

| Requirement | Status | Evidence |
| --- | --- | --- |
| Observation detail is not rendered as a modal, drawer overlay, or popup. | Verified | `ObservationWorkspacePage.test.tsx` modal regression test |
| Desktop renders in-page master-detail. | Verified | `ObservationMasterDetailLayout` + `ObservationWorkspacePage.test.tsx` |
| Tablet uses narrower/collapsible list without modal. | Verified | `src/frontend/src/index.css` tablet media rules |
| Mobile uses in-page Focus Mode. | Verified | `src/frontend/src/index.css` mobile rules + mobile back URL test |
| No backdrop for observation detail. | Verified | `ObservationWorkspacePage.test.tsx` |
| No body scroll lock for observation detail. | Verified | `ObservationWorkspacePage.test.tsx` |
| No `aria-modal` on observation detail. | Verified | `ObservationWorkspacePage.test.tsx` |
| List and detail remain in document flow. | Verified | `data-testid="observation-detail-document-flow"` regression test |
| Filters are preserved when closing detail. | Verified | `ObservationWorkspacePage.test.tsx` mobile back filter-context test |
| Browser back/forward works. | Verified | `ObservationWorkspacePage.test.tsx` history test |
| Refresh/deep link works. | Verified | `ObservationWorkspacePage.test.tsx` deep-link tests |
| Facility note links open Observation Workspace, not Facility popup. | Verified | `FacilityWorkspacePage.test.tsx` note redirect test |
| Legacy `panel=note` links remain supported. | Verified | `FacilityWorkspacePage.test.tsx` legacy redirect test |
| Current Phase 1A actions remain inline. | Verified | `ObservationWorkspacePage.test.tsx` inline action tests |
| Feature flag and legacy fallback routes are not removed. | Verified | No route deletion in `App.tsx`/resolvers |
| Phase 1B is not started in this PR. | Verified | Scope limited to layout, routing correction, docs, tests |

Summary:

```text
Missing = 0
```
