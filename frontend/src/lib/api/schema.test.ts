// @vitest-environment node
/// <reference types="node" />
// schema.d.ts must be what `pnpm run api:types` makes from the committed openapi.json (QA10 M5-4): editing one
// without the other fails here. (The backend checks that openapi.json is what it serves.)
import { execFileSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import openapiTS, { astToString } from 'openapi-typescript'

describe('generated API types', () => {
  it('are up to date with the committed OpenAPI document', async () => {
    const folder = resolve(process.cwd(), 'src/lib/api')
    const schemaFile = resolve(folder, 'schema.d.ts')
    const generated = astToString(await openapiTS(readFileSync(resolve(folder, 'openapi.json'), 'utf8')))

    // Formatted by the project's Prettier, as the script does (the CLI: Vitest resolves Prettier's browser build).
    const formatted = execFileSync(
      resolve(process.cwd(), 'node_modules/.bin/prettier'),
      ['--stdin-filepath', schemaFile],
      {
        input: generated,
        encoding: 'utf8',
      },
    )

    // The CLI adds a "generated, don't edit" banner on top that the library call doesn't.
    const committed = readFileSync(schemaFile, 'utf8').replace(/^\/\*\*[\s\S]*?\*\/\n\n/, '')
    expect(committed).toBe(formatted)
  }, 30_000)
})
