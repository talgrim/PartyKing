const pad = (value: number) => String(value).padStart(2, '0');

/** Seconds as the .NET TimeSpan string the API reads ("00:01:05"). */
export const secondsToTimeSpan = (totalSeconds: number): string => {
  const seconds = Math.round(totalSeconds);

  return `${pad(Math.floor(seconds / 3600))}:${pad(Math.floor((seconds % 3600) / 60))}:${pad(seconds % 60)}`;
};

/** Seconds in a .NET TimeSpan string ("00:00:20" or "00:00:05.5"). */
export const timeSpanToSeconds = (timeSpan: string): number => {
  const [hours = 0, minutes = 0, seconds = 0] = timeSpan.split(':').map(Number);

  return hours * 3600 + minutes * 60 + seconds;
};
