import { ComponentType } from 'react';
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
  /** Polish name from the physical card. */
  name: string;
  /** Short Polish description of the effect. */
  effect: string;
  Icon: ComponentType<{ className?: string; title?: string }>;
};

export const PLOT_CARDS: Record<ResistancePlotCardType, PlotCardInfo> = {
  OverheardConversation: {
    name: 'Jesteś naszym okiem i uchem',
    effect: 'Zagraj natychmiast: obejrzyj kartę Tożsamości jednego z graczy siedzących obok ciebie.',
    Icon: PlotOverheardConversationCardIcon,
  },
  OpinionMaker: {
    name: 'Liczymy się z twoim zdaniem',
    effect: 'Działa do końca gry: wybierasz i ujawniasz kartę Głosowania przed pozostałymi.',
    Icon: PlotOpinionMakerCardIcon,
  },
  EstablishConfidence: {
    name: 'To kwestia zaufania',
    effect: 'Zagraj natychmiast: Przywódca ujawnia swoją kartę Tożsamości innemu graczowi.',
    Icon: PlotEstablishConfidenceCardIcon,
  },
  OpenUp: {
    name: 'Tylko tobie zaufam',
    effect: 'Zagraj natychmiast: ujawnij swoją kartę Tożsamości innemu graczowi (może to być Przywódca).',
    Icon: PlotOpenUpCardIcon,
  },
  NoConfidence: {
    name: 'Nie ufajcie mu',
    effect: 'Jednorazowa: odrzuć zatwierdzoną drużynę, głosowanie liczy się jako nieudane.',
    Icon: PlotNoConfidenceCardIcon,
  },
  KeepingCloseEye: {
    name: 'Mam cię na oku',
    effect: 'Jednorazowa: po zagraniu kart Misji podejrzyj jedną zagraną kartę.',
    Icon: PlotCloseEyeCardIcon,
  },
  StrongLeader: {
    name: 'Charyzmatyczny Przywódca',
    effect: 'Jednorazowa: przejmij Przywództwo, zanim Przywódca cokolwiek zrobi.',
    Icon: PlotStrongLeaderCardIcon,
  },
  InTheSpotlight: {
    name: 'Pod obserwacją',
    effect: 'Jednorazowa: zmuś innego gracza do zagrania karty Misji jawnie.',
    Icon: PlotSpotlightCardIcon,
  },
  TakeResponsibility: {
    name: 'Przejmujesz odpowiedzialność',
    effect: 'Jednorazowa: weź kartę Intrygi innego gracza.',
    Icon: PlotTakeResponsibilityCardIcon,
  },
};
