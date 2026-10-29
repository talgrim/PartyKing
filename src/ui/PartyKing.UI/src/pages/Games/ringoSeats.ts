export type Seat = { left: number; top: number };

// The seats run along a U in the margins around the board: down the left edge, along the bottom, up the right edge.
// The top edge belongs to the navigation bar.
const LEFT_X = 5;
const RIGHT_X = 95;
const TOP_Y = 15;
const BOTTOM_Y = 95;

const LEFT_LENGTH = BOTTOM_Y - TOP_Y;
const BOTTOM_LENGTH = RIGHT_X - LEFT_X;
const TOTAL_LENGTH = LEFT_LENGTH + BOTTOM_LENGTH + LEFT_LENGTH;

/** Page coordinates (percent) of `count` seats spread evenly along the U, in turn order. */
export const seatPositions = (count: number): Seat[] =>
  Array.from({ length: count }, (_, index) => {
    const distance = ((index + 0.5) / count) * TOTAL_LENGTH;

    if (distance <= LEFT_LENGTH) {
      return { left: LEFT_X, top: TOP_Y + distance };
    }
    if (distance <= LEFT_LENGTH + BOTTOM_LENGTH) {
      return { left: LEFT_X + (distance - LEFT_LENGTH), top: BOTTOM_Y };
    }
    return { left: RIGHT_X, top: BOTTOM_Y - (distance - LEFT_LENGTH - BOTTOM_LENGTH) };
  });
