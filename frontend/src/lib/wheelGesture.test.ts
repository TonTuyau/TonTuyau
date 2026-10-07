import { LINE_PX, QUIET_MS, STEP_PX, WheelGesture, wheelPixels } from './wheelGesture'

/** Feeds events (pixels) every `every` ms from `from`; the steps it gives, in order. */
function run(gesture: WheelGesture, deltas: number[], every: number, from = 0): number[] {
  return deltas.map((pixels, index) => gesture.feed(pixels, from + index * every)).filter((step) => step !== 0)
}

describe('the shorts wheel: one short per gesture', () => {
  it("counts Firefox's lines and pages in pixels: one notch (3 lines) is a step", () => {
    expect(wheelPixels(3, 1, 800)).toBe(3 * LINE_PX)
    expect(wheelPixels(3, 1, 800)).toBeGreaterThanOrEqual(STEP_PX)
    expect(wheelPixels(-1, 2, 800)).toBe(-800)
    expect(wheelPixels(120, 0, 800)).toBe(120)
  })

  it('moves once for a mouse notch, in line or pixel units', () => {
    expect(run(new WheelGesture(), [wheelPixels(3, 1, 800)], 0)).toEqual([1])
    expect(run(new WheelGesture(), [-120], 0)).toEqual([-1])
  })

  it('moves once for a smooth-scrolled notch (many small events)', () => {
    expect(run(new WheelGesture(), Array<number>(12).fill(12), 16)).toEqual([1])
  })

  it('moves once for a long trackpad swipe and its momentum, however long it lasts', () => {
    const momentum = Array.from({ length: 150 }, (_, index) => Math.max(1, 60 - index / 3))
    expect(run(new WheelGesture(), momentum, 16)).toEqual([1])
  })

  it('moves once for a wheel spun without a pause, and again after a pause', () => {
    const gesture = new WheelGesture()
    expect(run(gesture, Array<number>(10).fill(120), 80)).toEqual([1])
    expect(run(gesture, [120], 0, 9 * 80 + QUIET_MS + 1)).toEqual([1])
  })

  it("doesn't add a small scroll to the next gesture", () => {
    const gesture = new WheelGesture()
    expect(run(gesture, [30], 0)).toEqual([])
    expect(run(gesture, [30], 0, QUIET_MS + 1)).toEqual([])
    expect(run(gesture, [20, 20], 10, 2 * (QUIET_MS + 1))).toEqual([])
  })

  it('goes up as well as down, by the sum of the gesture', () => {
    expect(run(new WheelGesture(), [-20, -20, -20], 16)).toEqual([-1])
    expect(run(new WheelGesture(), [40, -60, -40], 16)).toEqual([-1])
  })
})
