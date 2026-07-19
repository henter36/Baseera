#!/usr/bin/env node
import { readFileSync, existsSync } from 'node:fs'
import { resolve } from 'node:path'

const root = resolve(process.cwd())
const envFile = resolve(root, '.env.production')
const fileText = existsSync(envFile) ? readFileSync(envFile, 'utf8') : ''

function read(key) {
  return (process.env[key] || fileText.match(new RegExp(`^${key}=(.*)$`, 'm'))?.[1] || '').trim()
}

const NIL_GUID = /^0{8}-0{4}-0{4}-0{4}-0{12}$/i
const ZEROISH_GUID = /00000000-0000-0000-0000-00000000000[0-9a-f]/i

function isPlaceholder(value) {
  if (!value) return true
  if (value.includes('YOUR_')) return true
  if (NIL_GUID.test(value)) return true
  if (ZEROISH_GUID.test(value)) return true
  return false
}

const mode = read('VITE_AUTH_MODE')
if (mode === 'test') {
  console.error('REFUSED: production build cannot use VITE_AUTH_MODE=test')
  process.exit(1)
}

if (mode && mode !== 'entra') {
  console.error(`REFUSED: unsupported VITE_AUTH_MODE=${mode}`)
  process.exit(1)
}

for (const key of ['VITE_ENTRA_CLIENT_ID', 'VITE_ENTRA_TENANT_ID', 'VITE_ENTRA_API_SCOPE']) {
  const value = read(key)
  if (isPlaceholder(value)) {
    console.error(`REFUSED: missing/invalid production Entra setting ${key}`)
    process.exit(1)
  }
}

const redirect = read('VITE_ENTRA_REDIRECT_URI')
if (redirect) {
  if (!/^https:\/\//i.test(redirect)) {
    console.error('REFUSED: VITE_ENTRA_REDIRECT_URI must be HTTPS when set')
    process.exit(1)
  }
  if (/localhost|127\.0\.0\.1/i.test(redirect)) {
    console.error('REFUSED: VITE_ENTRA_REDIRECT_URI cannot be localhost in production')
    process.exit(1)
  }
}

console.log('Production auth configuration checks passed.')
