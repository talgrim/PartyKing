/** The number in a number input's text, or null while it is empty or not a number. */
export const numberOrNull = (text: string): number | null => {
  if (text.trim() === '') return null;
  const value = Number(text);
  return Number.isFinite(value) ? value : null;
};
