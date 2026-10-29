/** A phone or tablet: the main pointer is a finger, whatever the orientation or width. False where matchMedia is missing. */
export const isMobileDevice = (): boolean =>
  typeof window !== 'undefined' &&
  typeof window.matchMedia === 'function' &&
  window.matchMedia('(hover: none) and (pointer: coarse)').matches;
