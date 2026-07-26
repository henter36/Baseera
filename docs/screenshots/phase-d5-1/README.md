# Phase D.5.1 Screenshots — Facility Workforce Readiness

RTL command-center captures (fake Arabic names only; no real PII).

## Files

| File | Viewport | Scene / hash |
|------|----------|--------------|
| `desktop-overview.png` | 1440×1000 | `#overview` |
| `desktop-shift-coverage.png` | 1440×1000 | `#shift-coverage` |
| `desktop-unit-coverage.png` | 1440×1000 | `#unit-coverage` |
| `desktop-critical-role-gaps.png` | 1440×1000 | `#critical-role-gaps` |
| `desktop-member-panel.png` | 1440×1000 | `#member-panel` |
| `desktop-shift-panel.png` | 1440×1000 | `#shift-panel` |
| `desktop-qualification-expiry.png` | 1440×1000 | `#qualification-expiry` |
| `desktop-unsafe-staffing.png` | 1440×1000 | `#unsafe-staffing` |
| `desktop-data-quality.png` | 1440×1000 | `#data-quality` |
| `tablet-overview.png` | 1024×900 | `#tablet` |
| `mobile-overview.png` | 390×844 | `#mobile` |
| `mobile-shift.png` | 390×844 | `#shift` |
| `mobile-member-detail.png` | 390×844 | `#member-panel` |
| `state-ready.png` | 1440×900 | `#ready` |
| `state-attention.png` | 1440×900 | `#attention` |
| `state-critical.png` | 1440×900 | `#critical` |
| `state-unknown.png` | 1440×900 | `#unknown` |
| `state-empty.png` | 1440×900 | `#empty` |
| `state-partial.png` | 1440×900 | `#partial` |
| `import-preview.png` | 1440×1000 | `#import-preview` |

## Re-run capture

From the repository root:

```bash
node src/frontend/scripts/capture-workforce-screenshots.mjs
```

The script:

1. Serves `docs/screenshots/phase-d5-1/harness.html` (RTL Arabic command center: dark/teal strip, coverage rail, shift rows, unit heat, panels).
2. Uses Playwright Chromium via `npx playwright` (`dir=rtl`, hash routes).
3. Writes PNGs under `docs/screenshots/phase-d5-1/` and rejects any file &lt; 5KB.
4. Syncs a copy to `src/frontend/public/workforce-screenshot-harness.html` for optional Vite preview.

Optional: open the harness directly while `npm run dev` is running:

```text
http://localhost:5173/workforce-screenshot-harness.html#overview
```

Auth note: full React pages require API + session (`VITE_AUTH_MODE=test`). The harness is the repeatable offline path; frontend Vitest covers React behavior with mocked APIs.
