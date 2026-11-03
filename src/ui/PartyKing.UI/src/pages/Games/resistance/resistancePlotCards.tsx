import { ComponentType } from 'react';
import { i18n } from '@/i18n';
import { ResistancePlotCardType } from '@/service/endpoints/games/types';
import {
  PlotCloseEyeCardIcon,
  PlotEstablishConfidenceCardIcon,
  PlotNoConfidenceCardIcon,
  PlotOpenUpCardIcon,
  PlotOpinionMakerCardIcon,
  PlotOverheardConversationCardIcon,
  PlotSpotlightCardIcon,
  PlotStrongLeaderCardIcon,
  PlotTakeResponsibilityCardIcon,
} from './ResistanceIcons';

export type PlotCardInfo = {
  /** Name of the card, in the UI language. */
  readonly name: string;
  /** Short description of the effect, in the UI language. */
  readonly effect: string;
  Icon: ComponentType<{ className?: string; title?: string }>;
};

/** The texts are looked up when they are read, so a language switch shows up the next time a component renders. */
const plotCard = (type: ResistancePlotCardType, Icon: PlotCardInfo['Icon']): PlotCardInfo => ({
  get name() {
    return i18n.t(`resistance.plot.${type}.name`);
  },
  get effect() {
    return i18n.t(`resistance.plot.${type}.effect`);
  },
  Icon,
});

export const PLOT_CARDS: Record<ResistancePlotCardType, PlotCardInfo> = {
  OverheardConversation: plotCard('OverheardConversation', PlotOverheardConversationCardIcon),
  OpinionMaker: plotCard('OpinionMaker', PlotOpinionMakerCardIcon),
  EstablishConfidence: plotCard('EstablishConfidence', PlotEstablishConfidenceCardIcon),
  OpenUp: plotCard('OpenUp', PlotOpenUpCardIcon),
  NoConfidence: plotCard('NoConfidence', PlotNoConfidenceCardIcon),
  KeepingCloseEye: plotCard('KeepingCloseEye', PlotCloseEyeCardIcon),
  StrongLeader: plotCard('StrongLeader', PlotStrongLeaderCardIcon),
  InTheSpotlight: plotCard('InTheSpotlight', PlotSpotlightCardIcon),
  TakeResponsibility: plotCard('TakeResponsibility', PlotTakeResponsibilityCardIcon),
};

/** Played the moment they are dealt; their receiver confirms what they saw. */
export const IMMEDIATE_PLOT_CARDS: ResistancePlotCardType[] = ['OverheardConversation', 'EstablishConfidence', 'OpenUp'];
