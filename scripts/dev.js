#!/usr/bin/env node
import { spawn } from 'child_process'
import { watch } from 'fs'
import { join } from 'path'
import { fileURLToPath } from 'url'
import { execSync } from 'child_process'

const __dirname = fileURLToPath(new URL('.', import.meta.url))
const projectRoot = join(__dirname, '..')
const preloadMjsPath = join(projectRoot, 'out/preload/index.mjs')

// Function to run post-build
function runPostBuild() {
  try {
    execSync('node scripts/post-build.js', { cwd: projectRoot, stdio: 'inherit' })
  } catch (err) {
    console.error('[dev-watch] Error running post-build:', err.message)
  }
}

// Watch for changes to the preload file
watch(join(projectRoot, 'out/preload'), (eventType, filename) => {
  if (filename === 'index.mjs') {
    console.log('[dev-watch] Detected preload change, running post-build...')
    runPostBuild()
  }
})

// Start the electron-vite dev server
const child = spawn('electron-vite', ['dev'], {
  cwd: projectRoot,
  stdio: 'inherit',
  shell: true
})

child.on('exit', (code) => {
  console.log('[dev-watch] electron-vite dev exited with code', code)
  process.exit(code)
})
