import { join } from 'path'

export function preloadPath(): string {
  return join(__dirname, '../preload/index.js')
}
