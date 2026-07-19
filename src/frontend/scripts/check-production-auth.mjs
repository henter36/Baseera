#!/usr/bin/env node
import { readFileSync, existsSync } from 'node:fs'
import { resolve } from 'node:path'

const root = resolve(process.cwd())
const envFile = resolve(root, '.env.production')
const fileText = existsSync(envFile) ? readFileSync(envFile, 'utf8') : ''

function read(key) {
  return (process.env[key] || fileText.match(new RegExp(`^${key}=(.*)$`, 'm'))?.[1] || '').trim()
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
  if (!value || value.includes('YOUR_')) {
    console.error(`REFUSED: missing/invalid production Entra setting ${key}`)
    process.exit(1)
  }
}

console.log('Production auth configuration checks passed.')
