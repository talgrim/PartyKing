const storageKey = 'partyking.devPersona';
const queryParam = 'persona';
const frameNamePrefix = 'partyking.devPersona:';

/**
 * Development only: this document is a frame of the multi-player Resistance page (`ResistanceDevPanels`). Same-origin
 * frames share their tab's `sessionStorage`, so a frame must neither read nor write the persona there.
 */
export const isDevEmbedded: boolean = (() => {
  if (!import.meta.env.DEV) return false;
  try {
    return window.self !== window.top;
  } catch {
    return false;
  }
})();

/** The frame's `name` outlives a reload of the frame, which the `?persona=` of its first address may not. */
export const devPersonaFrameName = (persona: string): string => `${frameNamePrefix}${persona}`;

const readFramePersona = (): string | undefined =>
  window.name.startsWith(frameNamePrefix) ? window.name.slice(frameNamePrefix.length) || undefined : undefined;

const readPersona = (): string | undefined => {
  if (!import.meta.env.DEV) return undefined;
  try {
    const fromUrl = new URLSearchParams(window.location.search).get(queryParam);
    if (isDevEmbedded) return fromUrl ?? readFramePersona();

    if (fromUrl) {
      sessionStorage.setItem(storageKey, fromUrl);
    }

    return sessionStorage.getItem(storageKey) ?? undefined;
  } catch {
    return undefined;
  }
};

/**
 * Development only: this tab's persona (`?persona=anna`, kept in sessionStorage, so it is per tab). The API treats a
 * request with the persona as a plain guest with its own game player cookies, so one browser can play several players.
 */
export const devPersona: string | undefined = readPersona();

/** Keeps the persona in a link that opens another tab (sessionStorage is not shared with a noopener tab). */
export const withDevPersona = (path: string): string => {
  if (!devPersona) return path;
  const [base, query = ''] = path.split('?');
  const params = new URLSearchParams(query);
  params.set(queryParam, devPersona);

  return `${base}?${params.toString()}`;
};
