import { render } from '@testing-library/react';
import { useRef } from 'react';
import { useSiteBarHeight } from './useSiteBarHeight';

const Bar = () => {
  const bar = useRef<HTMLElement>(null);
  useSiteBarHeight(bar);
  return <header ref={bar} />;
};

const height = () => document.documentElement.style.getPropertyValue('--site-bar-h');

describe('useSiteBarHeight', () => {
  const observed: ResizeObserverCallback[] = [];

  beforeEach(() => {
    observed.length = 0;
    vi.stubGlobal(
      'ResizeObserver',
      class {
        constructor(callback: ResizeObserverCallback) {
          observed.push(callback);
        }
        observe() {}
        disconnect() {}
      },
    );
    vi.spyOn(HTMLElement.prototype, 'offsetHeight', 'get').mockReturnValue(52);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('publishes the height of the bar and takes it back when the bar goes', () => {
    const { unmount } = render(<Bar />);
    expect(height()).toBe('52px');

    unmount();
    expect(height()).toBe('');
  });

  it('follows the bar when it wraps', () => {
    render(<Bar />);
    vi.spyOn(HTMLElement.prototype, 'offsetHeight', 'get').mockReturnValue(96);

    observed.forEach((callback) => callback([], {} as ResizeObserver));

    expect(height()).toBe('96px');
  });
});
