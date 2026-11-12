const MS_PER_MINUTE = 60_000;

/** How many whole minutes ago a moment was, never negative. */
export const minutesAgo = (startedAt: number, now: number): number => Math.max(0, Math.round((now - startedAt) / MS_PER_MINUTE));

export const fromMinutesAgo = (minutes: number, now: number): number => now - minutes * MS_PER_MINUTE;

const pad = (value: number): string => String(value).padStart(2, '0');

/** The value a `datetime-local` input takes (`2025-01-31T22:05`), in the browser's own time zone. */
export const toLocalInputValue = (epochMs: number): string => {
  const date = new Date(epochMs);
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
};

/** The moment a `datetime-local` value stands for, or null when the input is empty or incomplete. */
export const fromLocalInputValue = (value: string): number | null => {
  const time = new Date(value).getTime();
  return Number.isNaN(time) ? null : time;
};
