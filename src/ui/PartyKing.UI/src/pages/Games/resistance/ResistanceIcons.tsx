import { KeyboardEvent, ReactNode } from 'react';
import { ResistanceMissionTeamInfo } from './ResistanceMissionTeamInfo';

type IconProps = {
  className?: string;
  /** Accessible name; the icon is decorative (aria-hidden) when omitted. */
  title?: string;
};

/** Makes an icon behave as a toggle button: click, Enter and Space activate it, unless it is disabled. */
type ButtonProps = {
  onClick?: () => void;
  pressed?: boolean;
  disabled?: boolean;
};

/**
 * Sizes used when the caller passes no `className`, keyed by the icon's viewBox. They follow the viewport width
 * (clamped), so the cards, tokens and boards grow and shrink with the screen; a caller can still pin a size.
 */
const DEFAULT_SIZE: Record<string, string> = {
  '0 0 100 140': 'h-[clamp(6rem,22vw,12rem)] w-auto',
  '0 0 100 100': 'size-[clamp(1rem,3vw,1.75rem)]',
  '0 0 560 190': 'w-full h-auto',
};

const COLORS = {
  resistance: '#1d4ed8',
  resistanceDark: '#172554',
  spy: '#b91c1c',
  spyDark: '#450a0a',
  success: '#15803d',
  fail: '#b91c1c',
  gold: '#f5c542',
  paper: '#f1f5f9',
  ink: '#0f172a',
  muted: '#64748b',
} as const;

const Svg = ({
  viewBox,
  className,
  title,
  onClick,
  pressed,
  disabled,
  children,
}: IconProps & ButtonProps & { viewBox: string; children: ReactNode }): ReactNode => {
  const interactive = onClick !== undefined;
  const onKeyDown = (event: KeyboardEvent<SVGSVGElement>) => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      if (!disabled) onClick?.();
    }
  };

  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      viewBox={viewBox}
      className={className ?? DEFAULT_SIZE[viewBox]}
      role={interactive ? 'button' : title ? 'img' : undefined}
      aria-label={title}
      aria-hidden={title || interactive ? undefined : true}
      aria-pressed={interactive ? pressed : undefined}
      aria-disabled={interactive && disabled ? true : undefined}
      tabIndex={interactive ? (disabled ? -1 : 0) : undefined}
      onClick={interactive && !disabled ? onClick : undefined}
      onKeyDown={interactive ? onKeyDown : undefined}
    >
      {children}
    </svg>
  );
};

/** A 100x140 playing card frame; `fill` is the card face, `stroke` its border. */
const CardFrame = ({ fill, stroke }: { fill: string; stroke: string }): ReactNode => (
  <>
    <rect x="2" y="2" width="96" height="136" rx="9" fill={fill} stroke={stroke} strokeWidth="3" />
    <rect x="8" y="8" width="84" height="124" rx="5" fill="none" stroke={stroke} strokeOpacity="0.45" strokeWidth="1.5" />
  </>
);

/** Head and shoulders, used on the identity cards and the team token. */
const Person = ({ x, y, scale = 1, fill }: { x: number; y: number; scale?: number; fill: string }): ReactNode => (
  <g transform={`translate(${x} ${y}) scale(${scale})`} fill={fill}>
    <circle cx="0" cy="-14" r="10" />
    <path d="M-22 22 C-22 2 -12 -3 0 -3 C12 -3 22 2 22 22 Z" />
  </g>
);

const Check = ({ cx, cy, size, stroke }: { cx: number; cy: number; size: number; stroke: string }): ReactNode => (
  <path
    d={`M${cx - size} ${cy} L${cx - size / 3} ${cy + size * 0.7} L${cx + size} ${cy - size * 0.7}`}
    fill="none"
    stroke={stroke}
    strokeWidth={size / 2.2}
    strokeLinecap="round"
    strokeLinejoin="round"
  />
);

const Cross = ({ cx, cy, size, stroke }: { cx: number; cy: number; size: number; stroke: string }): ReactNode => (
  <path
    d={`M${cx - size} ${cy - size} L${cx + size} ${cy + size} M${cx + size} ${cy - size} L${cx - size} ${cy + size}`}
    fill="none"
    stroke={stroke}
    strokeWidth={size / 1.8}
    strokeLinecap="round"
  />
);

/** Fist inside a diamond: the Resistance emblem, shared by the card backs. */
const Emblem = ({ cx, cy, scale = 1, color }: { cx: number; cy: number; scale?: number; color: string }): ReactNode => (
  <g transform={`translate(${cx} ${cy}) scale(${scale})`}>
    <path d="M0 -34 L34 0 L0 34 L-34 0 Z" fill="none" stroke={color} strokeWidth="4" strokeLinejoin="round" />
    <path
      d="M-12 14 L-12 -4 Q-12 -9 -7 -9 L-6 -9 L-6 -13 Q-6 -17 -2 -17 L0 -17 Q4 -17 4 -13 Q4 -17 8 -17 Q12 -17 12 -13 L12 -1 Q12 10 5 14 L5 20 L-9 20 L-9 14 Z"
      fill={color}
    />
  </g>
);

/** Back of every card that is dealt face down (role, plot). */
export const CardBackIcon = ({ className, title }: IconProps): ReactNode => (
  <Svg viewBox="0 0 100 140" className={className} title={title}>
    <CardFrame fill={COLORS.resistanceDark} stroke={COLORS.gold} />
    <Emblem cx={50} cy={70} scale={1.15} color={COLORS.gold} />
  </Svg>
);

/** Identity card: a spy. */
export const SpyCardIcon = ({ className, title }: IconProps): ReactNode => (
  <Svg viewBox="0 0 100 140" className={className} title={title}>
    <CardFrame fill={COLORS.spyDark} stroke={COLORS.spy} />
    <circle cx="50" cy="60" r="34" fill={COLORS.spy} fillOpacity="0.25" />
    {/* Fedora, sunglasses and a raised collar. */}
    <path d="M22 50 Q50 44 78 50 L74 56 Q50 51 26 56 Z" fill={COLORS.ink} />
    <path d="M34 50 Q34 28 50 28 Q66 28 66 50 Z" fill={COLORS.ink} />
    <rect x="34" y="50" width="32" height="26" rx="12" fill={COLORS.paper} />
    <rect x="36" y="56" width="12" height="7" rx="2" fill={COLORS.ink} />
    <rect x="52" y="56" width="12" height="7" rx="2" fill={COLORS.ink} />
    <rect x="47" y="58" width="6" height="2" fill={COLORS.ink} />
    <path d="M16 112 C16 90 30 82 50 82 C70 82 84 90 84 112 Z" fill={COLORS.ink} />
    <path d="M50 82 L40 98 L50 112 L60 98 Z" fill={COLORS.paper} />
    <text x="50" y="128" textAnchor="middle" fontSize="11" fontWeight="700" fill={COLORS.paper} fontFamily="sans-serif">
      SZPIEG
    </text>
  </Svg>
);

/** Identity card: a Resistance operative. */
export const OperativeCardIcon = ({ className, title }: IconProps): ReactNode => (
  <Svg viewBox="0 0 100 140" className={className} title={title}>
    <CardFrame fill={COLORS.resistanceDark} stroke={COLORS.resistance} />
    <circle cx="50" cy="60" r="34" fill={COLORS.resistance} fillOpacity="0.3" />
    <Person x={50} y={70} scale={1.35} fill={COLORS.paper} />
    {/* Beret with a star. */}
    <path d="M32 40 Q50 22 68 40 Q50 36 32 40 Z" fill={COLORS.resistance} />
    <path d="M50 30 l2 4.5 5 .5 -3.7 3.3 1.1 4.9 -4.4 -2.6 -4.4 2.6 1.1 -4.9 -3.7 -3.3 5 -.5 Z" fill={COLORS.gold} />
    <text x="50" y="128" textAnchor="middle" fontSize="9.5" fontWeight="700" fill={COLORS.paper} fontFamily="sans-serif">
      RUCH OPORU
    </text>
  </Svg>
);

/** Small marker for a fellow spy in the list a spy sees (a fedora and sunglasses on a red disc). */
export const SpyMarkerIcon = ({ className, title }: IconProps): ReactNode => (
  <Svg viewBox="0 0 100 100" className={className} title={title}>
    <circle cx="50" cy="50" r="46" fill={COLORS.spyDark} stroke={COLORS.spy} strokeWidth="5" />
    <path d="M18 46 Q50 38 82 46 L77 54 Q50 47 23 54 Z" fill={COLORS.paper} />
    <path d="M32 46 Q32 20 50 20 Q68 20 68 46 Z" fill={COLORS.paper} />
    <rect x="30" y="56" width="18" height="11" rx="3" fill={COLORS.paper} />
    <rect x="52" y="56" width="18" height="11" rx="3" fill={COLORS.paper} />
    <rect x="46" y="59" width="8" height="3" fill={COLORS.paper} />
  </Svg>
);

/** Token held by the current mission leader (a crown). */
export const LeaderTokenIcon = ({ className, title }: IconProps): ReactNode => (
  <Svg viewBox="0 0 100 100" className={className} title={title}>
    <circle cx="50" cy="50" r="46" fill={COLORS.resistanceDark} stroke={COLORS.gold} strokeWidth="4" />
    <path d="M24 68 L20 36 L38 50 L50 28 L62 50 L80 36 L76 68 Z" fill={COLORS.gold} stroke={COLORS.gold} strokeLinejoin="round" strokeWidth="3" />
    <rect x="24" y="68" width="52" height="8" rx="2" fill={COLORS.gold} />
    <circle cx="20" cy="36" r="4" fill={COLORS.gold} />
    <circle cx="50" cy="28" r="4" fill={COLORS.gold} />
    <circle cx="80" cy="36" r="4" fill={COLORS.gold} />
  </Svg>
);

/** Token marking a player chosen for the mission team. */
export const TeamTokenIcon = ({ className, title }: IconProps): ReactNode => (
  <Svg viewBox="0 0 100 100" className={className} title={title}>
    <circle cx="50" cy="50" r="46" fill={COLORS.resistance} stroke={COLORS.paper} strokeWidth="4" />
    <Person x={32} y={58} scale={0.8} fill={COLORS.paper} />
    <Person x={68} y={58} scale={0.8} fill={COLORS.paper} />
    <Person x={50} y={64} scale={1} fill="#e2e8f0" />
  </Svg>
);

/** Vote card: approve the proposed team. */
export const VoteApproveCardIcon = ({ className, title, ...button }: IconProps & ButtonProps): ReactNode => (
  <Svg viewBox="0 0 100 140" className={className} title={title} {...button}>
    <CardFrame fill={COLORS.paper} stroke={COLORS.success} />
    <circle cx="50" cy="62" r="30" fill={COLORS.success} />
    <Check cx={50} cy={62} size={16} stroke={COLORS.paper} />
    <text x="50" y="118" textAnchor="middle" fontSize="12" fontWeight="700" fill={COLORS.success} fontFamily="sans-serif">
      ZATWIERDŹ
    </text>
  </Svg>
);

/** Vote card: reject the proposed team. */
export const VoteRejectCardIcon = ({ className, title, ...button }: IconProps & ButtonProps): ReactNode => (
  <Svg viewBox="0 0 100 140" className={className} title={title} {...button}>
    <CardFrame fill={COLORS.paper} stroke={COLORS.fail} />
    <circle cx="50" cy="62" r="30" fill={COLORS.fail} />
    <Cross cx={50} cy={62} size={14} stroke={COLORS.paper} />
    <text x="50" y="118" textAnchor="middle" fontSize="12" fontWeight="700" fill={COLORS.fail} fontFamily="sans-serif">
      ODRZUĆ
    </text>
  </Svg>
);

/** Mission card: Success (the only card a Resistance member may play). */
export const MissionSuccessCardIcon = ({ className, title, ...button }: IconProps & ButtonProps): ReactNode => (
  <Svg viewBox="0 0 100 140" className={className} title={title} {...button}>
    <CardFrame fill={COLORS.resistanceDark} stroke={COLORS.success} />
    <path d="M50 22 L82 34 L82 70 Q82 96 50 112 Q18 96 18 70 L18 34 Z" fill={COLORS.success} stroke={COLORS.paper} strokeWidth="3" strokeLinejoin="round" />
    <Check cx={50} cy={66} size={16} stroke={COLORS.paper} />
    <text x="50" y="130" textAnchor="middle" fontSize="11" fontWeight="700" fill={COLORS.paper} fontFamily="sans-serif">
      SUKCES
    </text>
  </Svg>
);

/** Mission card: Fail (sabotage, only a spy may play it). */
export const MissionFailCardIcon = ({ className, title, ...button }: IconProps & ButtonProps): ReactNode => (
  <Svg viewBox="0 0 100 140" className={className} title={title} {...button}>
    <CardFrame fill={COLORS.spyDark} stroke={COLORS.fail} />
    <path d="M50 22 L82 34 L82 70 Q82 96 50 112 Q18 96 18 70 L18 34 Z" fill={COLORS.fail} stroke={COLORS.paper} strokeWidth="3" strokeLinejoin="round" />
    <path d="M50 38 L42 62 L54 62 L46 94 L64 56 L52 56 L60 38 Z" fill={COLORS.gold} />
    <text x="50" y="130" textAnchor="middle" fontSize="11" fontWeight="700" fill={COLORS.paper} fontFamily="sans-serif">
      PORAŻKA
    </text>
  </Svg>
);

/** Generic plot card (expansion): a sealed scroll with a question mark. */
export const PlotCardIcon = ({ className, title }: IconProps): ReactNode => (
  <Svg viewBox="0 0 100 140" className={className} title={title}>
    <CardFrame fill="#3b2f1a" stroke={COLORS.gold} />
    <rect x="24" y="30" width="52" height="64" rx="6" fill="#e7d9b0" stroke={COLORS.gold} strokeWidth="2" />
    <ellipse cx="50" cy="30" rx="26" ry="6" fill="#d4c28a" stroke={COLORS.gold} strokeWidth="2" />
    <ellipse cx="50" cy="94" rx="26" ry="6" fill="#d4c28a" stroke={COLORS.gold} strokeWidth="2" />
    <text x="50" y="76" textAnchor="middle" fontSize="40" fontWeight="700" fill="#3b2f1a" fontFamily="serif">
      ?
    </text>
    <text x="50" y="124" textAnchor="middle" fontSize="11" fontWeight="700" fill={COLORS.gold} fontFamily="sans-serif">
      INTRYGA
    </text>
  </Svg>
);

/** Shared look of the plot cards: the brown face with a gold border, a glyph in the upper part and a Polish name (one or two lines) below. */
const PlotCardFace = ({
  className,
  title,
  label,
  children,
}: IconProps & { label: string[]; children: ReactNode }): ReactNode => (
  <Svg viewBox="0 0 100 140" className={className} title={title}>
    <CardFrame fill="#3b2f1a" stroke={COLORS.gold} />
    <circle cx="50" cy="56" r="34" fill={COLORS.gold} fillOpacity="0.12" />
    {children}
    {label.map((line, index) => (
      <text
        key={line}
        x="50"
        y={label.length === 1 ? 124 : 117 + index * 10}
        textAnchor="middle"
        fontSize="8.5"
        fontWeight="700"
        fill={COLORS.gold}
        fontFamily="sans-serif"
      >
        {line}
      </text>
    ))}
  </Svg>
);

/** Plot card "Keeping a Close Eye on You": look at one played mission card (an eye). */
export const PlotCloseEyeCardIcon = ({ className, title }: IconProps): ReactNode => (
  <PlotCardFace className={className} title={title} label={['MAM CIĘ', 'NA OKU']}>
    <path d="M16 56 Q50 18 84 56 Q50 94 16 56 Z" fill={COLORS.paper} stroke={COLORS.gold} strokeWidth="3" strokeLinejoin="round" />
    <circle cx="50" cy="56" r="17" fill={COLORS.resistance} />
    <circle cx="50" cy="56" r="8" fill={COLORS.ink} />
    <circle cx="55" cy="51" r="3" fill={COLORS.paper} />
  </PlotCardFace>
);

/** Plot card "Establish Confidence": the leader shows their identity card (a person on an ID card). */
export const PlotEstablishConfidenceCardIcon = ({ className, title }: IconProps): ReactNode => (
  <PlotCardFace className={className} title={title} label={['ZDOBĄDŹ', 'ZAUFANIE']}>
    <rect x="20" y="32" width="60" height="44" rx="5" fill={COLORS.paper} stroke={COLORS.gold} strokeWidth="2.5" />
    <Person x={40} y={58} scale={0.7} fill={COLORS.resistance} />
    <rect x="54" y="42" width="20" height="4" rx="2" fill={COLORS.muted} />
    <rect x="54" y="52" width="20" height="4" rx="2" fill={COLORS.muted} />
    <circle cx="68" cy="74" r="12" fill={COLORS.success} stroke={COLORS.paper} strokeWidth="2.5" />
    <Check cx={68} cy={74} size={6} stroke={COLORS.paper} />
  </PlotCardFace>
);

/** Plot card "Strong Leader": become the leader (a crown with an upward arrow). */
export const PlotStrongLeaderCardIcon = ({ className, title }: IconProps): ReactNode => (
  <PlotCardFace className={className} title={title} label={['SILNY', 'LIDER']}>
    <path d="M24 74 L20 44 L38 58 L50 36 L62 58 L80 44 L76 74 Z" fill={COLORS.gold} stroke={COLORS.gold} strokeLinejoin="round" strokeWidth="3" />
    <rect x="24" y="74" width="52" height="8" rx="2" fill={COLORS.gold} />
    <circle cx="20" cy="44" r="4" fill={COLORS.gold} />
    <circle cx="50" cy="36" r="4" fill={COLORS.gold} />
    <circle cx="80" cy="44" r="4" fill={COLORS.gold} />
    <path d="M50 30 L50 14 M43 21 L50 14 L57 21" fill="none" stroke={COLORS.paper} strokeWidth="4" strokeLinecap="round" strokeLinejoin="round" />
  </PlotCardFace>
);

/** Plot card "Overheard Conversation": peek at a neighbour's identity card (an ear with sound waves). */
export const PlotOverheardConversationCardIcon = ({ className, title }: IconProps): ReactNode => (
  <PlotCardFace className={className} title={title} label={['PODSŁUCHANA', 'ROZMOWA']}>
    <path
      d="M56 24 C38 24 30 38 32 52 C33 62 38 66 40 74 C42 82 50 86 58 82 C68 78 68 68 62 64 C70 58 74 50 74 42 C74 30 66 24 56 24 Z"
      fill={COLORS.paper}
      stroke={COLORS.gold}
      strokeWidth="3"
      strokeLinejoin="round"
    />
    <path d="M50 38 C44 38 42 46 46 50 C50 54 54 56 54 62" fill="none" stroke={COLORS.muted} strokeWidth="3" strokeLinecap="round" />
    <path d="M18 40 Q10 56 18 72 M26 46 Q21 56 26 66" fill="none" stroke={COLORS.gold} strokeWidth="3" strokeLinecap="round" />
  </PlotCardFace>
);

/** Plot card "No Confidence": nullifies an approved vote (the approve check struck out). */
export const PlotNoConfidenceCardIcon = ({ className, title }: IconProps): ReactNode => (
  <PlotCardFace className={className} title={title} label={['WOTUM', 'NIEUFNOŚCI']}>
    <circle cx="50" cy="56" r="30" fill={COLORS.success} />
    <Check cx={50} cy={56} size={15} stroke={COLORS.paper} />
    <circle cx="50" cy="56" r="34" fill="none" stroke={COLORS.fail} strokeWidth="6" />
    <path d="M26 32 L74 80" stroke={COLORS.fail} strokeWidth="6" strokeLinecap="round" />
  </PlotCardFace>
);

/** Plot card "In the Spotlight": a team member plays their mission card face up (a spotlight on a person). */
export const PlotSpotlightCardIcon = ({ className, title }: IconProps): ReactNode => (
  <PlotCardFace className={className} title={title} label={['W ŚWIETLE', 'REFLEKTORÓW']}>
    <path d="M42 14 L58 14 L84 92 L16 92 Z" fill={COLORS.gold} fillOpacity="0.35" />
    <rect x="38" y="8" width="24" height="10" rx="3" fill={COLORS.muted} stroke={COLORS.gold} strokeWidth="2" />
    <Person x={50} y={76} scale={1.1} fill={COLORS.paper} />
  </PlotCardFace>
);

/** Plot card "Opinion Maker": votes first and in public (a megaphone). */
export const PlotOpinionMakerCardIcon = ({ className, title }: IconProps): ReactNode => (
  <PlotCardFace className={className} title={title} label={['KREATOR', 'OPINII']}>
    <path d="M22 46 L22 66 L36 66 L62 84 L62 28 L36 46 Z" fill={COLORS.gold} stroke={COLORS.paper} strokeWidth="2.5" strokeLinejoin="round" />
    <rect x="30" y="64" width="10" height="16" rx="3" fill={COLORS.paper} stroke={COLORS.gold} strokeWidth="2" />
    <path d="M70 40 Q78 56 70 72 M78 32 Q92 56 78 80" fill="none" stroke={COLORS.paper} strokeWidth="3.5" strokeLinecap="round" />
  </PlotCardFace>
);

/** Plot card "Open Up": the holder shows their identity card to another player (an open card with an arrow). */
export const PlotOpenUpCardIcon = ({ className, title }: IconProps): ReactNode => (
  <PlotCardFace className={className} title={title} label={['OTWÓRZ SIĘ']}>
    <rect x="22" y="34" width="46" height="34" rx="5" fill={COLORS.paper} stroke={COLORS.gold} strokeWidth="2.5" />
    <Person x={37} y={56} scale={0.6} fill={COLORS.resistance} />
    <rect x="48" y="44" width="14" height="4" rx="2" fill={COLORS.muted} />
    <rect x="48" y="54" width="14" height="4" rx="2" fill={COLORS.muted} />
    <path d="M60 82 L84 82 M76 74 L84 82 L76 90" fill="none" stroke={COLORS.paper} strokeWidth="4" strokeLinecap="round" strokeLinejoin="round" />
  </PlotCardFace>
);

/** Plot card "Take Responsibility": take a plot card from another player (a hand grabbing a card). */
export const PlotTakeResponsibilityCardIcon = ({ className, title }: IconProps): ReactNode => (
  <PlotCardFace className={className} title={title} label={['PRZEJMUJESZ', 'ODPOWIEDZIALNOŚĆ']}>
    <rect x="34" y="24" width="32" height="44" rx="4" fill="#e7d9b0" stroke={COLORS.gold} strokeWidth="2.5" />
    <text x="50" y="54" textAnchor="middle" fontSize="22" fontWeight="700" fill="#3b2f1a" fontFamily="serif">
      ?
    </text>
    <path d="M24 90 L24 66 Q24 58 32 58 L50 58 Q58 58 58 66 L58 90 Z" fill={COLORS.gold} stroke={COLORS.paper} strokeWidth="2.5" strokeLinejoin="round" />
    <path d="M76 70 L58 70 M66 62 L58 70 L66 78" fill="none" stroke={COLORS.paper} strokeWidth="4" strokeLinecap="round" strokeLinejoin="round" />
  </PlotCardFace>
);

export type MissionResult = 'success' | 'fail' | null;

type MissionBoardProps = {
  /** Team size of each of the 5 missions. */
  teamSizes: readonly number[];
  /** Outcome of each finished mission, null while it hasn't been played. */
  results: readonly MissionResult[];
  /** Names of the team of each finished mission; a mission with a team gets an (i) badge showing it. */
  teams?: readonly (readonly string[] | null)[];
  /** Index (0-4) of the mission being played, or null when the game is over. */
  currentMission: number | null;
  /** Teams rejected in a row during the current mission (0-5). */
  rejectedTeams: number;
  /** Mission 4 needs two Fail cards (7 or more players). */
  fourthMissionNeedsTwoFails: boolean;
  /** Draws the team sizes faded: they are only a guess until enough players joined. */
  dimmed?: boolean;
  className?: string;
  title?: string;
};

const MISSIONS = 5;
const MAX_REJECTIONS = 5;

/** The mission track (five discs with team size and outcome) plus the rejected-teams tracker. */
export const MissionBoardIcon = ({
  teamSizes,
  results,
  teams = [],
  currentMission,
  rejectedTeams,
  fourthMissionNeedsTwoFails,
  dimmed = false,
  className,
  title,
}: MissionBoardProps): ReactNode => (
  <Svg viewBox="0 0 560 190" className={className} title={title}>
    <rect x="2" y="2" width="556" height="186" rx="14" fill={COLORS.resistanceDark} stroke={COLORS.gold} strokeWidth="3" />
    {Array.from({ length: MISSIONS }, (_, i) => {
      const cx = 62 + i * 109;
      const result = results[i] ?? null;
      const fill = result === 'success' ? COLORS.success : result === 'fail' ? COLORS.fail : '#1e293b';
      const isCurrent = currentMission === i;
      return (
        <g key={i}>
          <circle
            cx={cx}
            cy={70}
            r={42}
            fill={fill}
            stroke={isCurrent ? COLORS.gold : COLORS.muted}
            strokeWidth={isCurrent ? 6 : 3}
          />
          {result === 'success' && <Check cx={cx} cy={70} size={18} stroke={COLORS.paper} />}
          {result === 'fail' && <Cross cx={cx} cy={70} size={16} stroke={COLORS.paper} />}
          {result === null && (
            <text
              x={cx}
              y={84}
              textAnchor="middle"
              fontSize="40"
              fontWeight="700"
              fill={COLORS.paper}
              fontFamily="sans-serif"
              opacity={dimmed ? 0.35 : 1}
            >
              {teamSizes[i]}
            </text>
          )}
          <text x={cx} y={132} textAnchor="middle" fontSize="13" fontWeight="700" letterSpacing="1.5" fill={COLORS.paper} fontFamily="sans-serif">
            {`MISJA ${i + 1}`}
          </text>
          {i === 3 && fourthMissionNeedsTwoFails && (
            <text x={cx} y={150} textAnchor="middle" fontSize="12" fontWeight="700" fill={COLORS.gold} fontFamily="sans-serif">
              2 PORAŻKI
            </text>
          )}
          {result !== null && (teams[i]?.length ?? 0) > 0 && (
            <ResistanceMissionTeamInfo cx={cx + 34} cy={34} missionNumber={i + 1} succeeded={result === 'success'} names={teams[i] ?? []} />
          )}
        </g>
      );
    })}
    {/* Rejected teams tracker: five steps, the fifth is a spy win. The label and the steps share the row's centre line. */}
    <rect x="18" y="154" width="524" height="28" rx="14" fill={COLORS.ink} opacity="0.4" />
    {Array.from({ length: MAX_REJECTIONS }, (_, i) => {
      const cx = 262 + i * 56;
      const filled = i < rejectedTeams;
      const isLast = i === MAX_REJECTIONS - 1;
      return (
        <circle
          key={i}
          cx={cx}
          cy={168}
          r={9}
          fill={filled ? COLORS.fail : 'none'}
          stroke={isLast ? COLORS.fail : COLORS.muted}
          strokeWidth="3"
        />
      );
    })}
    <text
      x="38"
      y="168"
      dominantBaseline="central"
      fontSize="13"
      fontWeight="700"
      letterSpacing="1.5"
      fill={COLORS.paper}
      fontFamily="sans-serif"
    >
      ODRZUCONE DRUŻYNY
    </text>
  </Svg>
);
