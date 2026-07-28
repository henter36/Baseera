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
// 2. The "confirmed dead routes" section must actually list route rows (not just
//    have a heading — an emptied-out section must fail, not silently pass), and
//    every listed route must (a) still exist as a declared route and (b) carry a
//    "Remove/redirect" disposition in the main table.
// ---------------------------------------------------------------------------
const deadRoutesHeadingIndex = inventoryMd.indexOf('## Route ميتة مؤكَّدة')
if (deadRoutesHeadingIndex === -1) {
  fail('screen-and-route-inventory.md is missing its "Route ميتة مؤكَّدة" (confirmed dead routes) section.')
} else {
  const nextHeadingIndex = inventoryMd.indexOf('\n## ', deadRoutesHeadingIndex + 1)
  const deadRoutesSection = inventoryMd.slice(
    deadRoutesHeadingIndex,
    nextHeadingIndex === -1 ? undefined : nextHeadingIndex,
  )
  const deadRouteRows = [...deadRoutesSection.matchAll(/^\|\s*`(\/[^`]*)`\s*\|/gm)].map((m) => m[1])

  if (deadRouteRows.length === 0) {
    fail('The "Route ميتة مؤكَّدة" section has no route rows — it must list at least the routes it claims to track.')
  }

  for (const deadRoute of deadRouteRows) {
    if (!codeRoutes.has(deadRoute)) {
      fail(`Dead route "${deadRoute}" no longer exists in App.tsx — remove it from the dead-routes section or update the source.`)
      continue
    }
    const mainRow = mainTable.split('\n').find((line) => line.startsWith(`| \`${deadRoute}\` |`))
    if (!mainRow || !mainRow.includes('Remove/redirect')) {
      fail(`Dead route "${deadRoute}" is not marked "Remove/redirect" in the main table — disposition is out of sync.`)
    }
  }
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
// 4. Context Panel entity types: derive the authoritative list directly from
//    FacilityWorkspacePage.tsx's own `const PANEL_TYPES = [...] as const` declaration
//    (the type union every panel literal in the file is actually checked against),
//    rather than hand-maintaining a second copy here that can silently drift out of
//    sync with it. Then verify every literal type used at a call site (openPanel(...),
//    panel.type === '...', etc.) is actually a member of that declared union — this
//    catches a typo'd/invalid panel type slipping past a hand-copied allowlist.
// ---------------------------------------------------------------------------
const panelTypesDeclMatch = facilityWorkspaceTsx.match(/const PANEL_TYPES = \[([\s\S]*?)\] as const/)
if (!panelTypesDeclMatch) {
  fail('Could not find "const PANEL_TYPES = [...] as const" in FacilityWorkspacePage.tsx — extraction needs updating after a refactor.')
}
const declaredPanelTypes = new Set(
  panelTypesDeclMatch ? [...panelTypesDeclMatch[1].matchAll(/'([a-zA-Z0-9-]+)'/g)].map((m) => m[1]) : [],
)

const panelTypeMatches = [
  ...facilityWorkspaceTsx.matchAll(/panel\.type === '([a-zA-Z0-9-]+)'/g),
  ...facilityWorkspaceTsx.matchAll(/type: '([a-zA-Z0-9-]+)' as const/g),
  ...facilityWorkspaceTsx.matchAll(/openPanel\(\{\s*type: '([a-zA-Z0-9-]+)'/g),
].map((m) => m[1])
const panelTypesInCode = new Set(panelTypeMatches)

const undeclaredPanelTypes = [...panelTypesInCode].filter((t) => !declaredPanelTypes.has(t))
if (undeclaredPanelTypes.length > 0) {
  fail(
    `FacilityWorkspacePage.tsx uses Context Panel type(s) not present in its own PANEL_TYPES declaration: ${undeclaredPanelTypes.join(', ')}. ` +
      'This indicates a typo or a stale PANEL_TYPES union.',
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
console.log(`Context Panel types declared in PANEL_TYPES: ${declaredPanelTypes.size}`)
console.log(`Context Panel types actually used at call sites: ${panelTypesInCode.size}`)

if (failed) {
  console.error('\nux-route-inventory-check FAILED — see FAIL lines above.')
  process.exit(1)
}

console.log('\nux-route-inventory-check passed.')
