import sharp from 'sharp'
import fs from 'fs'
import path from 'path'
import { fileURLToPath } from 'url'

const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)
const assetsDir = path.join(__dirname, '../assets')

async function generateIcon(size, filename) {
  // Create a simple purple icon with 💜 emoji
  const svg = `
    <svg width="${size}" height="${size}" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${size} ${size}">
      <defs>
        <linearGradient id="grad" x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" style="stop-color:#d946ef;stop-opacity:1" />
          <stop offset="100%" style="stop-color:#ad1dd9;stop-opacity:1" />
        </linearGradient>
      </defs>
      <rect width="${size}" height="${size}" fill="url(#grad)" rx="${size * 0.1}"/>
      <circle cx="${size/2}" cy="${size/2}" r="${size * 0.35}" fill="white" opacity="0.1"/>
    </svg>
  `

  try {
    await sharp(Buffer.from(svg))
      .png()
      .toFile(path.join(assetsDir, filename))
    console.log(`✓ Generated ${filename}`)
  } catch (error) {
    console.error(`✗ Error generating ${filename}:`, error.message)
  }
}

async function main() {
  console.log('Generating icons...')

  // Create assets directory if it doesn't exist
  if (!fs.existsSync(assetsDir)) {
    fs.mkdirSync(assetsDir, { recursive: true })
  }

  // Generate icons
  await generateIcon(256, 'icon.png')
  await generateIcon(32, 'tray-icon.png')
  await generateIcon(32, 'tray-icon-active.png')

  console.log('Icons generated!')
}

main().catch(console.error)
