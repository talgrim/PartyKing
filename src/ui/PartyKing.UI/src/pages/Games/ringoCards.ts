import { RingoFlipDto } from '@/service/endpoints/games/types';
import { getCard } from './ringoLayout';

/** The fourth king ends the game. */
export const KINGS_TO_END = 4;

const SUIT_NAMES: Record<string, string> = { '♠': 'Pik', '♥': 'Kier', '♦': 'Karo', '♣': 'Trefl' };

const RANK_NAMES: Record<string, string> = {
  '2': 'Dwójka',
  '3': 'Trójka',
  '4': 'Czwórka',
  '5': 'Piątka',
  '6': 'Szóstka',
  '7': 'Siódemka',
  '8': 'Ósemka',
  '9': 'Dziewiątka',
  '10': 'Dziesiątka',
  J: 'Walet',
  Q: 'Dama',
  K: 'Król',
  A: 'As',
};

const EFFECTS: Record<string, string> = {
  '2': 'Rozdaj dwa łyki: po jednym dwóm osobom albo dwa jednej. Możesz wybrać siebie.',
  '3': 'Rozdaj trzy łyki: po jednym trzem osobom albo kilka jednej. Możesz wybrać siebie.',
  '4': 'Rozdaj cztery łyki: między dowolne osoby, siebie też możesz wybrać.',
  '5': 'Wszyscy mężczyźni piją po łyku.',
  '6': 'Wszystkie kobiety piją po łyku.',
  '7':
    'Połóż kartę przed sobą. W dowolnej chwili zrób „batmana”: złóż dłonie w okulary i przyłóż do oczu. ' +
    'Ostatnia osoba, która zauważy i powtórzy ruch, pije.',
  '8': 'Wszyscy piją po łyku.',
  '9': 'Rymy. Powiedz słowo, a każda kolejna osoba mówi rym do niego. Kto powtórzy słowo albo nic nie powie, pije.',
  '10': 'Osoby po lewej i po prawej stronie od ciebie piją po łyku.',
  J:
    'Połóż kartę przed sobą. W dowolnej chwili oprzyj pięść o jakąś powierzchnię. ' +
    'Ostatnia osoba, która zrobi ten gest albo go nie zauważy, pije łyk.',
  Q: 'Połóż kartę przed sobą. Pierwsza osoba, która odpowie na jakiekolwiek twoje pytanie, pije.',
  A:
    'Nowa zasada gry. Pełna dowolność: kary x2, faceci piją podwójnie, rozmowa tylko pytaniami, ' +
    'zakaz przeklinania, sky is the limit.',
};

export const cardName = (cardId: number): string => {
  const { rank, suit } = getCard(cardId);
  return `${RANK_NAMES[rank]} ${SUIT_NAMES[suit]}`;
};

/** Which king (1-4) of the game the card is, counted in flip order; 0 for a card that isn't a king. */
export const kingNumber = (flips: RingoFlipDto[], cardId: number): number => {
  const kings = flips.filter((flip) => getCard(flip.cardId).rank === 'K');
  return kings.findIndex((flip) => flip.cardId === cardId) + 1;
};

/** The flip of the fourth king, which ends the game; undefined while the game goes on. */
export const endingFlip = (flips: RingoFlipDto[]): RingoFlipDto | undefined =>
  flips.filter((flip) => getCard(flip.cardId).rank === 'K')[KINGS_TO_END - 1];

export const cardEffect = (cardId: number, flips: RingoFlipDto[]): string => {
  const { rank } = getCard(cardId);
  if (rank !== 'K') return EFFECTS[rank];

  const number = kingNumber(flips, cardId);
  return number >= KINGS_TO_END
    ? 'Czwarty król! Koniec gry. Wypij duszkiem cały pozostały alkohol.'
    : `To ${number}. król z ${KINGS_TO_END}. Nic się nie dzieje, ale czwarty król kończy grę.`;
};

/** The cards that already left the table into the kings or aces pile (every flipped card but the pending one). */
export const pileCards = (flips: RingoFlipDto[], pendingCardId: number | null) => {
  const gone = flips.map((flip) => getCard(flip.cardId)).filter((card) => card.id !== pendingCardId);
  return {
    kings: gone.filter((card) => card.rank === 'K'),
    aces: gone.filter((card) => card.rank === 'A'),
  };
};
