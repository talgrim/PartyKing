import { ReactNode } from 'react';
import {
  CardBackIcon,
  LeaderTokenIcon,
  MissionBoardIcon,
  MissionFailCardIcon,
  MissionSuccessCardIcon,
  OperativeCardIcon,
  PlotCardIcon,
  SpyCardIcon,
  TeamTokenIcon,
  VoteApproveCardIcon,
  VoteRejectCardIcon,
} from './ResistanceIcons';

const CARDS: [string, typeof SpyCardIcon, string][] = [
  ['Card back', CardBackIcon, 'w-32'],
  ['Spy', SpyCardIcon, 'w-32'],
  ['Operative', OperativeCardIcon, 'w-32'],
  ['Vote approve', VoteApproveCardIcon, 'w-32'],
  ['Vote reject', VoteRejectCardIcon, 'w-32'],
  ['Mission success', MissionSuccessCardIcon, 'w-32'],
  ['Mission fail', MissionFailCardIcon, 'w-32'],
  ['Plot', PlotCardIcon, 'w-32'],
  ['Leader token', LeaderTokenIcon, 'w-24'],
  ['Team token', TeamTokenIcon, 'w-24'],
];

/** Throwaway page for eyeballing the Resistance icons; dev builds only (see routes.tsx). */
export const ResistanceIconsPreview = (): ReactNode => (
  <div className="p-6 space-y-8 bg-slate-800 min-h-screen text-slate-100">
    <h1 className="text-2xl font-bold">Resistance icons (preview)</h1>
    <div className="flex flex-wrap gap-6">
      {CARDS.map(([label, Icon, size]) => (
        <figure key={label} className="flex flex-col items-center gap-2">
          <Icon className={size} title={label} />
          <figcaption className="text-sm">{label}</figcaption>
        </figure>
      ))}
    </div>
    <div className="space-y-4">
      <MissionBoardIcon
        className="max-w-xl"
        title="Start of the game (8 players)"
        teamSizes={[3, 4, 4, 5, 5]}
        results={[null, null, null, null, null]}
        currentMission={0}
        rejectedTeams={0}
        fourthMissionNeedsTwoFails
      />
      <MissionBoardIcon
        className="max-w-xl"
        title="Mid game (5 players)"
        teamSizes={[2, 3, 2, 3, 3]}
        results={['success', 'fail', null, null, null]}
        currentMission={2}
        rejectedTeams={3}
        fourthMissionNeedsTwoFails={false}
      />
    </div>
  </div>
);
