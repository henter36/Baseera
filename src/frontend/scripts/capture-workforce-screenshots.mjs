#!/usr/bin/env node
/**
 * Repeatable Playwright captures for Phase D.5.1 workforce screenshots.
 *
 * Usage (from repository root):
 *   node src/frontend/scripts/capture-workforce-screenshots.mjs
 *
 * Fake Arabic demo names only — no real PII.
 */
import { spawn } from 'node:child_process'
import { createServer } from 'node:http'
import { readFile, mkdir, access, stat, copyFile } from 'node:fs/promises'
import path from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const frontendRoot = path.resolve(__dirname, '..')
const repoRoot = path.resolve(frontendRoot, '../..')
const outDir = path.join(repoRoot, 'docs/screenshots/phase-d5-1')
const harnessPath = path.join(outDir, 'harness.html')
const publicHarnessPath = path.join(frontendRoot, 'public/workforce-screenshot-harness.html')

const CAPTURES = [
  { file: 'desktop-overview.png', scene: 'overview', width: 1440, height: 1000 },
  { file: 'desktop-shift-coverage.png', scene: 'shift-coverage', width: 1440, height: 1000 },
  { file: 'desktop-unit-coverage.png', scene: 'unit-coverage', width: 1440, height: 1000 },
  { file: 'desktop-critical-role-gaps.png', scene: 'critical-role-gaps', width: 1440, height: 1000 },
  { file: 'desktop-member-panel.png', scene: 'member-panel', width: 1440, height: 1000 },
  { file: 'desktop-shift-panel.png', scene: 'shift-panel', width: 1440, height: 1000 },
  { file: 'desktop-qualification-expiry.png', scene: 'qualification-expiry', width: 1440, height: 1000 },
  { file: 'desktop-unsafe-staffing.png', scene: 'unsafe-staffing', width: 1440, height: 1000 },
  { file: 'desktop-data-quality.png', scene: 'data-quality', width: 1440, height: 1000 },
  { file: 'tablet-overview.png', scene: 'tablet', width: 1024, height: 900 },
  { file: 'mobile-overview.png', scene: 'mobile', width: 390, height: 844 },
  { file: 'mobile-shift.png', scene: 'shift', width: 390, height: 844 },
  { file: 'mobile-member-detail.png', scene: 'member-panel', width: 390, height: 844 },
  { file: 'state-ready.png', scene: 'ready', width: 1440, height: 900 },
  { file: 'state-attention.png', scene: 'attention', width: 1440, height: 900 },
  { file: 'state-critical.png', scene: 'critical', width: 1440, height: 900 },
  { file: 'state-unknown.png', scene: 'unknown', width: 1440, height: 900 },
  { file: 'state-empty.png', scene: 'empty', width: 1440, height: 900 },
  { file: 'state-partial.png', scene: 'partial', width: 1440, height: 900 },
  { file: 'import-preview.png', scene: 'import-preview', width: 1440, height: 1000 },
]

function run(cmd, args, cwd) {
  return new Promise((resolve, reject) => {
    const child = spawn(cmd, args, {
      cwd,
      stdio: 'inherit',
      shell: process.platform === 'win32',
    })
    child.on('exit', (code) => (code === 0 ? resolve() : reject(new Error(`${cmd} exited ${code}`))))
  })
}

async function ensurePlaywright() {
  try {
    await import('playwright')
  } catch {
    console.log('Installing playwright locally for screenshot capture...')
    await run('npm', ['install', '-D', 'playwright@1.54.2', '--ignore-scripts'], frontendRoot)
  }
}

async function launchBrowser(chromium) {
  const browsersRoot = process.env.PLAYWRIGHT_BROWSERS_PATH
  const candidates = [
    process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE,
    '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
    browsersRoot && path.join(browsersRoot, 'chromium-1161/chrome-mac/Chromium.app/Contents/MacOS/Chromium'),
    browsersRoot && path.join(browsersRoot, 'chromium-1181/chrome-mac/Chromium.app/Contents/MacOS/Chromium'),
    '/Applications/Chromium.app/Contents/MacOS/Chromium',
  ].filter(Boolean)

  for (const executablePath of candidates) {
    try {
      await access(executablePath)
      // Skip incomplete Playwright extracts (binary stub without Framework).
      if (executablePath.includes('Chromium.app')) {
        const frameworkMarker = path.join(
          path.dirname(executablePath),
          '../Frameworks/Chromium Framework.framework',
        )
        try {
          await access(frameworkMarker)
        } catch {
          console.warn(`skipping incomplete Chromium: ${executablePath}`)
          continue
        }
      }
      console.log(`using browser: ${executablePath}`)
      return await chromium.launch({ headless: true, executablePath })
    } catch (error) {
      console.warn(`launch failed for ${executablePath}: ${error.message}`)
    }
  }

  try {
    console.log('using channel: chrome')
    return await chromium.launch({ headless: true, channel: 'chrome' })
  } catch (error) {
    console.warn(`channel chrome failed: ${error.message}`)
    return chromium.launch({ headless: true })
  }
}

async function startStaticServer() {
  const html = await readFile(harnessPath)
  const server = createServer((_req, res) => {
    res.writeHead(200, {
      'Content-Type': 'text/html; charset=utf-8',
      'Cache-Control': 'no-store',
    })
    res.end(html)
  })
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve))
  const { port } = server.address()
  return { server, baseUrl: `http://127.0.0.1:${port}/` }
}

async function main() {
  await access(harnessPath)
  await mkdir(outDir, { recursive: true })
  try {
    await mkdir(path.dirname(publicHarnessPath), { recursive: true })
    await copyFile(harnessPath, publicHarnessPath)
  } catch (error) {
    console.warn('Could not sync public harness copy:', error.message)
  }

  await ensurePlaywright()
  const { chromium } = await import('playwright')
  const { server, baseUrl } = await startStaticServer()
  const browser = await launchBrowser(chromium)

  try {
    for (const capture of CAPTURES) {
      const page = await browser.newPage({
        viewport: { width: capture.width, height: capture.height },
        locale: 'ar-SA',
      })
      await page.goto(`${baseUrl}#${encodeURIComponent(capture.scene)}`, {
        waitUntil: 'domcontentloaded',
        timeout: 15_000,
      })
      await page.waitForFunction(
        (expected) => document.body.dataset.scene === expected,
        capture.scene,
        { timeout: 10_000 },
      )
      await page.evaluate(() => document.fonts?.ready?.catch?.(() => undefined))
      const target = path.join(outDir, capture.file)
      await page.screenshot({
        path: target,
        fullPage: false,
        type: 'png',
        timeout: 15_000,
        animations: 'disabled',
      })
      await page.close()
      const info = await stat(target)
      if (info.size < 5_000) {
        throw new Error(`${capture.file} is only ${info.size} bytes (< 5KB)`)
      }
      console.log(`wrote ${capture.file} (${info.size} bytes)`)
    }
  } finally {
    await browser.close()
    server.close()
  }

  console.log(`\nCaptured ${CAPTURES.length} PNGs into ${path.relative(repoRoot, outDir)}`)
  console.log(`Harness: ${path.relative(repoRoot, harnessPath)}`)
  console.log(`file URL: ${pathToFileURL(harnessPath).href}`)
}

main().catch((error) => {
  console.error(error)
  process.exit(1)
})
