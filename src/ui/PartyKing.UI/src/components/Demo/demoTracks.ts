export type DemoTrack = {
  /** Shown on the chip. */
  name: string;
  /** What is queued, a link like a visitor would paste. */
  url: string;
};

/**
 * Copyright-free tracks (NoCopyrightSounds) for visitors who have no link at hand. Each was checked with YouTube's
 * oEmbed endpoint, which answers 403 for a video that cannot be embedded; check a new one the same way, because the
 * host plays them in an embedded player.
 */
export const DEMO_TRACKS: DemoTrack[] = [
  { name: 'Disfigure - Blank', url: 'https://www.youtube.com/watch?v=p7ZsBPK656s' },
  { name: 'Janji - Heroes Tonight', url: 'https://www.youtube.com/watch?v=3nQNiWdeH2Q' },
  { name: 'Elektronomia - Sky High', url: 'https://www.youtube.com/watch?v=TW9d8vYrVFQ' },
  { name: 'Cartoon - On & On', url: 'https://www.youtube.com/watch?v=K4DyBUG242c' },
  { name: 'Warriyo - Mortals', url: 'https://www.youtube.com/watch?v=yJg-Y5byMMw' },
  { name: 'Syn Cole - Feel Good', url: 'https://www.youtube.com/watch?v=q1ULJ92aldE' },
  { name: 'Different Heaven & EH!DE - My Heart', url: 'https://www.youtube.com/watch?v=jK2aIUmmdP4' },
  { name: 'DEAF KEV - Invincible', url: 'https://www.youtube.com/watch?v=J2X5mJ3HDYE' },
];
