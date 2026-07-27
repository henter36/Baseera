#!/usr/bin/env node
// Verifies docs/ux-rescue/screen-and-route-inventory.md stays reconciled with the actual
// React Router routes and sidebar nav links declared in src/App.tsx, and that any panel
// entity type introduced in FacilityWorkspacePage.tsx is a known/documented type.
//
// Deliberately avoids parsing a dynamic router: App.tsx declares every <Route path="..."> as a
// static JSX literal, so plain regex extraction over the source text is reliable here. If the
// router ever becomes data-driven (route config object, file-based routing, etc.) this script's
// extraction step must be rewritten rather than patched.

import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, resolve } from 'node:path'

const __dirname = dirname(fileURLToPath(import.meta.url))
const repoRoot = resolve(__dirname, '..', '..', '..')

const appTsxPath = resolve(repoRoot, 'src/frontend/src/App.tsx')
const facilityWorkspacePath = resolve(repoRoot, 'src/frontend/src/pages/workspaces/FacilityWorkspacePage.tsx')
const inventoryPath = resolve(repoRoot, 'docs/ux-rescue/screen-and-route-inventory.md')

let failed = false
const fail = (message) => {
  failed = true
  console.error(`FAIL: ${message}`)
}
const warn = (message) => {
  console.warn(`WARN: ${message}`)
}

const appTsx = readFileSync(appTsxPath, 'utf8')
const facilityWorkspaceTsx = readFileSync(facilityWorkspacePath, 'utf8')
const inventoryMd = readFileSync(inventoryPath, 'utf8')

// ---------------------------------------------------------------------------
// 1. Every <Route path="..."> in App.tsx must appear as a documented row in the
//    inventory's main table, and vice versa.
// ---------------------------------------------------------------------------
const routeMatches = [...appTsx.matchAll(/<Route\s+path="([^"]+)"/g)].map((m) => m[1])
const codeRoutes = new Set(routeMatches)

if (routeMatches.length !== codeRoutes.size) {
  const seen = new Set()
  const dupes = new Set()
  for (const r of routeMatches) {
    if (seen.has(r)) dupes.add(r)
    seen.add(r)
  }
  fail(`Duplicate <Route path> declarations in App.tsx: ${[...dupes].join(', ')}`)
}

// The inventory's main table is the block of "| `/...` | ..." rows before the first
// "## " heading that follows it (the "Routes غير موجودة" section starts a new topic).
const mainTableEnd = inventoryMd.indexOf('## Routes غير موجودة إطلاقًا')
const mainTable = mainTableEnd === -1 ? inventoryMd : inventoryMd.slice(0, mainTableEnd)
const inventoryRouteMatches = [...mainTable.matchAll(/^\|\s*`(\/[^`]*)`\s*\|/gm)].map((m) => m[1])
const inventoryRoutes = new Set(inventoryRouteMatches)

for (const route of codeRoutes) {
  if (!inventoryRoutes.has(route)) {
    fail(`Route "${route}" exists in App.tsx but has no row in screen-and-route-inventory.md's main table.`)
  }
}
for (const route of inventoryRoutes) {
  if (!codeRoutes.has(route)) {
    fail(`Route "${route}" is documented in screen-and-route-inventory.md but no longer exists in App.tsx (stale doc row).`)
  }
}

// ---------------------------------------------------------------------------
// 2. Known dead routes must still be tracked in the migration/transition plan doc,
//    not silently forgotten if someone edits the inventory later.
// ---------------------------------------------------------------------------
const deadRoutesSection = inventoryMd.match(/## Route ميتة مؤكَّدة[\s\S]*?\n\n/)
if (!deadRoutesSection) {
  fail('screen-and-route-inventory.md is missing its "Route ميتة مؤكَّدة" (confirmed dead routes) section.')
}

// ---------------------------------------------------------------------------
// 3. Every static sidebar <NavLink to="..."> must resolve to a route declared in App.tsx.
//    (All nav links in the current Shell are static string literals, never templated.)
// ---------------------------------------------------------------------------
const navLinkMatches = [...appTsx.matchAll(/<NavLink\s+to="([^"]+)"/g)].map((m) => m[1])
for (const navPath of navLinkMatches) {
  const staticPath = navPath.split('?')[0]
  const matches = [...codeRoutes].some((routePath) => {
    const pattern = '^' + routePath.replace(/:[^/]+/g, '[^/]+') + '$'
    return new RegExp(pattern).test(staticPath)
  })
  if (!matches) {
    fail(`Sidebar NavLink target "${navPath}" does not resolve to any declared <Route path>.`)
  }
}

// ---------------------------------------------------------------------------
// 4. Context Panel entity types opened from FacilityWorkspacePage.tsx must be in the
//    known allowlist below. Extend the allowlist (and the Context Panel table in the
//    inventory doc) deliberately when a new panel type is introduced — this check exists
//    to catch silent drift, not to freeze the panel type list forever.
// ---------------------------------------------------------------------------
const KNOWN_PANEL_TYPES = new Set([
  'note',
  'corrective-action',
  'risk',
  'form-assignment',
  'escalation',
  'facility-unit',
  'workforce-member',
  'workforce-requirement',
  'workforce-qualification',
  'workforce-gap',
  'workforce-critical-position',
  'workforce-roster',
  'workforce-unit',
  'workforce-role',
  'workforce-shift',
  'requirement-gap',
  'equipment',
  'activity',
  'sensitive-custody',
  'vehicle',
  'communication-device',
])

const panelTypeMatches = [
  ...facilityWorkspaceTsx.matchAll(/panel\.type === '([a-zA-Z0-9-]+)'/g),
  ...facilityWorkspaceTsx.matchAll(/type: '([a-zA-Z0-9-]+)' as const/g),
  ...facilityWorkspaceTsx.matchAll(/openPanel\(\{\s*type: '([a-zA-Z0-9-]+)'/g),
].map((m) => m[1])
const panelTypesInCode = new Set(panelTypeMatches)

const undocumentedPanelTypes = [...panelTypesInCode].filter((t) => !KNOWN_PANEL_TYPES.has(t))
if (undocumentedPanelTypes.length > 0) {
  fail(
    `FacilityWorkspacePage.tsx opens Context Panel type(s) not in the known allowlist: ${undocumentedPanelTypes.join(', ')}. ` +
      'Add them to KNOWN_PANEL_TYPES in this script and to the "Context Panel" table in docs/ux-rescue/screen-and-route-inventory.md.',
  )
}

if (panelTypesInCode.size === 0) {
  warn('No panel.type literals were found in FacilityWorkspacePage.tsx — extraction regex may need updating after a refactor.')
}

// ---------------------------------------------------------------------------
// Summary
// ---------------------------------------------------------------------------
console.log(`Routes in App.tsx: ${codeRoutes.size}`)
console.log(`Routes documented in inventory: ${inventoryRoutes.size}`)
console.log(`Sidebar NavLinks checked: ${navLinkMatches.length}`)
console.log(`Context Panel types found in FacilityWorkspacePage.tsx: ${panelTypesInCode.size}`)

if (failed) {
  console.error('\nux-route-inventory-check FAILED — see FAIL lines above.')
  process.exit(1)
}

console.log('\nux-route-inventory-check passed.')
