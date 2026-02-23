/**
 * TokenLUV Audio Alerts — Web Audio API Chiptune
 * Genera tonos sintéticos sin archivos externos
 */

let audioContext: AudioContext | null = null

function getAudioContext(): AudioContext {
  if (!audioContext) {
    audioContext = new (window.AudioContext || (window as any).webkitAudioContext)()
  }
  return audioContext
}

function playTone(
  frequency: number,
  duration: number,
  gain: number = 0.3,
  type: OscillatorType = 'sine'
): Promise<void> {
  return new Promise((resolve) => {
    const ctx = getAudioContext()
    const osc = ctx.createOscillator()
    const gainNode = ctx.createGain()

    osc.type = type
    osc.frequency.value = frequency

    gainNode.gain.setValueAtTime(gain, ctx.currentTime)
    gainNode.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + duration / 1000)

    osc.connect(gainNode)
    gainNode.connect(ctx.destination)

    osc.start(ctx.currentTime)
    osc.stop(ctx.currentTime + duration / 1000)

    setTimeout(resolve, duration)
  })
}

/**
 * Play success sound — short ascending tone
 */
export async function playSuccess(): Promise<void> {
  await playTone(440, 80, 0.3)  // Low
  await playTone(880, 80, 0.3)  // High
}

/**
 * Play warning sound — two bips
 */
export async function playWarning(): Promise<void> {
  await playTone(660, 100, 0.25, 'square')
  await new Promise((r) => setTimeout(r, 80))
  await playTone(660, 100, 0.25, 'square')
}

/**
 * Play danger sound — urgent three bips
 */
export async function playDanger(): Promise<void> {
  await playTone(880, 120, 0.3, 'square')
  await new Promise((r) => setTimeout(r, 60))
  await playTone(880, 120, 0.3, 'square')
  await new Promise((r) => setTimeout(r, 60))
  await playTone(880, 140, 0.3, 'square')
}

export type SoundType = 'success' | 'warning' | 'danger'

export function playSound(type: SoundType): void {
  if (type === 'success') {
    playSuccess().catch(console.error)
  } else if (type === 'warning') {
    playWarning().catch(console.error)
  } else if (type === 'danger') {
    playDanger().catch(console.error)
  }
}
