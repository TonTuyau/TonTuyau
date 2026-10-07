// The shorts player's mouse wheel / trackpad (1.1.0, the Firefox fixes): one short per gesture.
// - A gesture is a run of wheel events with no gap longer than QUIET_MS: a mouse notch with smooth scrolling (several
//   small events), a trackpad swipe and its momentum (events for a second or more), a wheel spun without a pause.
// - It moves one short once it has gone STEP_PX in one direction, then ignores the rest of the gesture, so a long
//   swipe or the momentum can't move twice, and a partial scroll doesn't add up with the next gesture.
// - Firefox gives DOM_DELTA_LINE (3 lines per notch) where Chromium gives pixels: a line counts LINE_PX, so one
//   notch is a step in both.

export const STEP_PX = 50
export const QUIET_MS = 250
export const LINE_PX = 40

/** One wheel event's vertical distance in pixels, whatever its unit. */
export function wheelPixels(deltaY: number, deltaMode: number, pageHeight: number): number {
  if (deltaMode === 1) {
    return deltaY * LINE_PX
  }
  return deltaMode === 2 ? deltaY * pageHeight : deltaY
}

export class WheelGesture {
  #distance = 0
  #lastAt = Number.NEGATIVE_INFINITY
  #moved = false

  /** Feeds one wheel event (its time in ms); 1 or -1 when the gesture moves a short now, else 0. */
  feed(pixels: number, now: number): -1 | 0 | 1 {
    if (now - this.#lastAt > QUIET_MS) {
      this.#distance = 0
      this.#moved = false
    }
    this.#lastAt = now
    if (this.#moved) {
      return 0
    }
    this.#distance += pixels
    if (Math.abs(this.#distance) < STEP_PX) {
      return 0
    }
    this.#moved = true
    return this.#distance > 0 ? 1 : -1
  }
}
