import { readFileSync, writeFileSync } from 'fs'
import { join } from 'path'
import { fileURLToPath } from 'url'

const __dirname = fileURLToPath(new URL('.', import.meta.url))
const projectRoot = join(__dirname, '..')
const source = join(projectRoot, 'out/preload/index.mjs')
const dest = join(projectRoot, 'out/preload/index.js')

try {
  let content = readFileSync(source, 'utf-8')

  // Remove import statement and setup for CommonJS context
  // The .mjs file has:
  // import __cjs_mod__ from "node:module";
  // const __filename = import.meta.filename;
  // const __dirname = import.meta.dirname;
  // const require2 = __cjs_mod__.createRequire(import.meta.url);
  // const { contextBridge, ipcRenderer } = require2("electron");
  //
  // We need to convert this to work in CommonJS

  let cjsContent = content
    // Remove the import statements at the top
    .replace(/^import\s+.*?from\s+["'].*?["'];/gm, '')
    // Remove the ESM setup code
    .replace(/^const __filename = import\.meta\.filename;.*$/m, '')
    .replace(/^const __dirname = import\.meta\.dirname;.*$/m, '')
    .replace(/^const require2 = .*?createRequire.*?;.*$/m, '')
    .replace(/^const __cjs_mod__.*?;.*$/m, '')
    // Replace require2 with require
    .replace(/require2\(/g, 'require(')

  // Add proper CommonJS wrapper
  const wrappedContent = `
// Electron preload script (CommonJS)
(function() {
  try {
    ${cjsContent}
  } catch (error) {
    console.error('[preload] Fatal error:', error);
  }
})();
`

  writeFileSync(dest, wrappedContent)
  console.log('[post-build] Converted out/preload/index.mjs -> out/preload/index.js')
} catch (err) {
  console.error('[post-build] Error:', err.message)
}
