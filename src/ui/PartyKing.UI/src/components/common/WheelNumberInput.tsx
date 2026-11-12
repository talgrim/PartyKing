import { InputHTMLAttributes, ReactNode, useEffect, useRef } from 'react';
import { numberOrNull, stepNumber } from '@/utils/numberInput';

type Props = Omit<InputHTMLAttributes<HTMLInputElement>, 'value' | 'onChange' | 'type' | 'min' | 'max' | 'step'> & {
  value: number | null;
  /** Null while the field is empty. */
  onChange: (value: number | null) => void;
  min?: number;
  max?: number;
  step?: number;
};

/**
 * A number input that also takes the mouse wheel: scrolling up adds a step, scrolling down takes one off, within `min`
 * and `max`. Only the focused field reacts, so scrolling a dialog past a number never changes it. React's own wheel
 * handler is passive and cannot stop the page from scrolling, hence the native listener.
 */
export const WheelNumberInput = ({ value, onChange, min, max, step = 1, ...rest }: Props): ReactNode => {
  const input = useRef<HTMLInputElement>(null);
  const latest = useRef({ value, onChange, min, max, step });

  useEffect(() => {
    latest.current = { value, onChange, min, max, step };
  });

  useEffect(() => {
    const element = input.current;
    if (!element) return;

    const onWheel = (event: WheelEvent) => {
      if (document.activeElement !== element || event.deltaY === 0) return;
      event.preventDefault();
      const { value: current, onChange: change, ...options } = latest.current;
      change(stepNumber(current, event.deltaY < 0 ? 1 : -1, options));
    };

    element.addEventListener('wheel', onWheel, { passive: false });
    return () => element.removeEventListener('wheel', onWheel);
  }, []);

  return (
    <input
      {...rest}
      ref={input}
      type="number"
      min={min}
      max={max}
      step={step}
      value={value ?? ''}
      onChange={(e) => onChange(numberOrNull(e.target.value))}
    />
  );
};
