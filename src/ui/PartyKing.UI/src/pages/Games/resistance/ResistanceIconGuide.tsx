import { ComponentType, ReactNode } from 'react';
import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { PageNavigation } from '@/components/common/PageNavigation';
import { sessionResistancePath } from '@/router/constants';
import {
  CardBackIcon,
  AssassinCardIcon,
  BlindSpyCardIcon,
  BodyGuardCardIcon,
  CommanderCardIcon,
  DeepCoverCardIcon,
  FalseCommanderCardIcon,
  DefectorCardIcon,
  DefectorSwitchCardIcon,
  InquisitorCardIcon,
  LeaderTokenIcon,
  MissionFailCardIcon,
  MissionReverseCardIcon,
  MissionSuccessCardIcon,
  OperativeCardIcon,
  ReverserCardIcon,
  SpyCardIcon,
  SpyMarkerIcon,
  VoteApproveCardIcon,
  VoteRejectCardIcon,
} from './ResistanceIcons';
import { PLOT_CARDS } from './resistancePlotCards';

type Entry = {
  /** Shown to the left of the text; its size is set by the guide. */
  icon: (className: string) => ReactNode;
  name: string;
  meaning: string;
};

type Group = {
  id: string;
  title: string;
  /** Who switches the group on, shown under the title. */
  note?: string;
  entries: Entry[];
};

type IconComponent = ComponentType<{ className?: string; title?: string }>;

const card = (Icon: IconComponent) => (className: string) => <Icon className={className} />;

const CARD = 'w-20 shrink-0';

/**
 * Every icon the Resistance game draws, grouped by the module that brings it, with what it means. The Plot cards come
 * from `PLOT_CARDS`, so a new card shows up here with its name and effect.
 */
export const ResistanceIconGuide = (): ReactNode => {
  const { t } = useTranslation();
  const { sessionId = '' } = useParams();

  const groups: Group[] = [
    {
      id: 'base',
      title: t('resistance.iconGuide.base.title'),
      entries: [
        { icon: card(CardBackIcon), name: t('resistance.iconGuide.base.cardBack.name'), meaning: t('resistance.iconGuide.base.cardBack.meaning') },
        { icon: card(SpyCardIcon), name: t('resistance.roles.spy'), meaning: t('resistance.iconGuide.base.spy') },
        { icon: card(OperativeCardIcon), name: t('resistance.roles.resistance'), meaning: t('resistance.iconGuide.base.resistance') },
        { icon: card(SpyMarkerIcon), name: t('resistance.iconGuide.base.spyMarker.name'), meaning: t('resistance.iconGuide.base.spyMarker.meaning') },
        { icon: card(LeaderTokenIcon), name: t('resistance.leader'), meaning: t('resistance.iconGuide.base.leader') },
        { icon: card(VoteApproveCardIcon), name: t('resistance.vote.approve'), meaning: t('resistance.iconGuide.base.approve') },
        { icon: card(VoteRejectCardIcon), name: t('resistance.vote.reject'), meaning: t('resistance.iconGuide.base.reject') },
        { icon: card(MissionSuccessCardIcon), name: t('resistance.missionCard.success'), meaning: t('resistance.iconGuide.base.success') },
        { icon: card(MissionFailCardIcon), name: t('resistance.missionCard.fail'), meaning: t('resistance.iconGuide.base.fail') },
      ],
    },
    {
      id: 'plot',
      title: t('resistance.rules.plot.title'),
      note: t('resistance.iconGuide.plot.note'),
      entries: Object.values(PLOT_CARDS).map((plotCard) => ({
        icon: card(plotCard.Icon),
        name: plotCard.name,
        meaning: plotCard.effect,
      })),
    },
    {
      id: 'inquisitor',
      title: t('resistance.rules.inquisitor.title'),
      note: t('resistance.iconGuide.moduleNote'),
      entries: [
        { icon: card(InquisitorCardIcon), name: t('resistance.marks.inquisitor'), meaning: t('resistance.iconGuide.inquisitor.token') },
      ],
    },
    {
      id: 'reverser',
      title: t('resistance.rules.reverser.title'),
      note: t('resistance.iconGuide.moduleNote'),
      entries: [
        { icon: card(ReverserCardIcon), name: t('resistance.characters.reverser'), meaning: t('resistance.iconGuide.reverser.character') },
        { icon: card(MissionReverseCardIcon), name: t('resistance.missionCard.reverse'), meaning: t('resistance.iconGuide.reverser.card') },
      ],
    },
    {
      id: 'defector',
      title: t('resistance.rules.defector.title'),
      note: t('resistance.iconGuide.moduleNote'),
      entries: [
        { icon: card(DefectorCardIcon), name: t('resistance.characters.defector'), meaning: t('resistance.iconGuide.defector.character') },
        {
          icon: (className) => <DefectorSwitchCardIcon className={className} change />,
          name: t('resistance.iconGuide.defector.change.name'),
          meaning: t('resistance.iconGuide.defector.change.meaning'),
        },
        {
          icon: (className) => <DefectorSwitchCardIcon className={className} change={false} />,
          name: t('resistance.iconGuide.defector.noChange.name'),
          meaning: t('resistance.iconGuide.defector.noChange.meaning'),
        },
      ],
    },
    {
      id: 'assassin',
      title: t('resistance.rules.assassin.title'),
      note: t('resistance.iconGuide.moduleNote'),
      entries: [
        { icon: card(CommanderCardIcon), name: t('resistance.characters.commander'), meaning: t('resistance.iconGuide.assassin.commander') },
        { icon: card(AssassinCardIcon), name: t('resistance.characters.assassin'), meaning: t('resistance.iconGuide.assassin.assassin') },
        { icon: card(BodyGuardCardIcon), name: t('resistance.characters.bodyGuard'), meaning: t('resistance.iconGuide.assassin.bodyGuard') },
        { icon: card(DeepCoverCardIcon), name: t('resistance.characters.deepCover'), meaning: t('resistance.iconGuide.assassin.deepCover') },
        { icon: card(BlindSpyCardIcon), name: t('resistance.characters.blindSpy'), meaning: t('resistance.iconGuide.assassin.blindSpy') },
        { icon: card(FalseCommanderCardIcon), name: t('resistance.characters.falseCommander'), meaning: t('resistance.iconGuide.assassin.falseCommander') },
      ],
    },
  ];

  return (
    <div className="flex flex-col gap-4 w-full min-h-screen p-3 pt-10 bg-slate-800 text-slate-400">
      <PageNavigation links={[{ to: sessionResistancePath(sessionId), label: t('resistance.iconGuide.back') }]} />
      <h1 className="text-xl font-semibold text-slate-100">{t('resistance.iconGuide.title')}</h1>
      <p className="text-sm">{t('resistance.iconGuide.intro')}</p>
      {groups.map((group) => (
        <section key={group.id} aria-labelledby={`icon-guide-${group.id}`} className="flex flex-col gap-2">
          <h2 id={`icon-guide-${group.id}`} className="text-lg text-slate-200">
            {group.title}
          </h2>
          {group.note && <p className="text-sm">{group.note}</p>}
          <ul className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
            {group.entries.map((entry) => (
              <li key={entry.name} className="flex items-center gap-3 rounded-lg bg-slate-900 p-3">
                {entry.icon(CARD)}
                <div>
                  <h3 className="font-medium text-slate-200">{entry.name}</h3>
                  <p className="text-sm">{entry.meaning}</p>
                </div>
              </li>
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
};
