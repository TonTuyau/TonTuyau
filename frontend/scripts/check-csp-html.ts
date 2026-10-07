// What in an HTML page the Content-Security-Policy (server/server.ts) would block: inline <script> code, <style>
// elements, style="" attributes and on*="" event handlers. index.html has none (its theme script is
// public/theme-init.js); src/lib/theme.test.ts checks the source page, and check.sh runs this file on the build:
//   node scripts/check-csp-html.ts dist/index.html
import { readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

/** One description per construct the policy would block; empty when the page is clean. */
export function cspViolations(html: string): string[] {
  const page = html.replace(/<!--[\s\S]*?-->/g, '')
  const found: string[] = []
  for (const script of page.matchAll(/<script\b([^>]*)>([\s\S]*?)<\/script\s*>/gi)) {
    const attributes = script[1] ?? ''
    const body = script[2] ?? ''
    if (!/\ssrc\s*=/i.test(attributes)) {
      found.push(`inline script: <script${attributes}>${body.trim().slice(0, 60)}`)
    } else if (body.trim() !== '') {
      found.push(`script with both src and inline code: <script${attributes}>`)
    }
  }
  if (/<style\b/i.test(page)) {
    found.push('a <style> element')
  }
  for (const tag of page.matchAll(/<[a-z][^>]*>/gi)) {
    if (/\sstyle\s*=/i.test(tag[0])) {
      found.push(`a style attribute: ${tag[0]}`)
    }
    if (/\son[a-z]+\s*=/i.test(tag[0])) {
      found.push(`an inline event handler: ${tag[0]}`)
    }
  }
  return found
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const file = process.argv[2]
  if (!file) {
    console.error('usage: node scripts/check-csp-html.ts <page.html>')
    process.exit(2)
  }
  const violations = cspViolations(readFileSync(file, 'utf8'))
  if (violations.length > 0) {
    console.error(`${file}: the Content-Security-Policy would block:\n  ${violations.join('\n  ')}`)
    process.exit(1)
  }
  console.log(`${file}: nothing the Content-Security-Policy would block.`)
}
