import { isMobileDevice } from './isMobileDevice';

describe('isMobileDevice', () => {
  const original = window.matchMedia;
  afterEach(() => {
    window.matchMedia = original;
  });

  it('is true when the main pointer is a finger', () => {
    window.matchMedia = vi.fn().mockReturnValue({ matches: true }) as unknown as typeof window.matchMedia;

    expect(isMobileDevice()).toBe(true);
    expect(window.matchMedia).toHaveBeenCalledWith('(hover: none) and (pointer: coarse)');
  });

  it('is false for a mouse, and where matchMedia is missing', () => {
    window.matchMedia = vi.fn().mockReturnValue({ matches: false }) as unknown as typeof window.matchMedia;
    expect(isMobileDevice()).toBe(false);

    window.matchMedia = undefined as unknown as typeof window.matchMedia;
    expect(isMobileDevice()).toBe(false);
  });
});
