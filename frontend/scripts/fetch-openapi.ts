// Copies the running backend's OpenAPI document into src/lib/api/openapi.json (QA10 M5-4). Then
// `pnpm run api:types` turns it into schema.d.ts. A backend test fails while the copy differs from what the API
// serves, so a backend change can't ship without the front's types following it.
// Usage: pnpm run api:openapi (backend at TONTUYAU_BACKEND_URL, default http://localhost:5080).
import { writeFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

const backend = process.env.TONTUYAU_BACKEND_URL ?? 'http://localhost:5080'
const target = fileURLToPath(new URL('../src/lib/api/openapi.json', import.meta.url))

const response = await fetch(`${backend}/openapi/v1.json`)
if (!response.ok) {
  throw new Error(`${backend}/openapi/v1.json answered ${response.status}`)
}
writeFileSync(target, `${JSON.stringify(await response.json(), null, 2)}\n`)
console.log(`Wrote ${target}`)
