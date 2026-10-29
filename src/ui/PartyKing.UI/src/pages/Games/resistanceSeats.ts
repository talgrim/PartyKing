export type Seat = { left: number; top: number };

// Ellipse around the board (percent of the page), the first seat at the bottom, going clockwise
const CENTER_X = 50;
const CENTER_Y = 55;
const RADIUS_X = 44;
const RADIUS_Y = 38;

/** Page coordinates (percent) of `count` seats spread evenly around the board, in join order. */
export const ringSeatPositions = (count: number): Seat[] =>
  Array.from({ length: count }, (_, index) => {
    const angle = Math.PI / 2 + (index / count) * 2 * Math.PI;

    return { left: CENTER_X + RADIUS_X * Math.cos(angle), top: CENTER_Y + RADIUS_Y * Math.sin(angle) };
  });
