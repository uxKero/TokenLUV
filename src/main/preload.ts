import { join } from 'path'

export function preloadPath(): string {
  // .js file is a copy of .mjs for Windows compatibility
  return join(__dirname, '../preload/index.js')
}
