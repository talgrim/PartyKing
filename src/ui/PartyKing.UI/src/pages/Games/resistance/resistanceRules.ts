import { i18n } from '@/i18n';

/** Summary of the rules shown in the info dialog of the Resistance page. */
export type RuleSection = {
  title: string;
  text: string;
  /** Shown as a list under the text, each with a bold lead-in. */
  items?: { name: string; text: string }[];
  /** A closing note after the list. */
  footer?: string;
};

const PLOT_ITEMS = ['makers', 'immediate', 'strongLeader', 'noConfidence', 'spotlight', 'take', 'closeEye'] as const;

/** The rule sections in the UI language. */
export const resistanceRules = (): RuleSection[] => [
  { title: i18n.t('resistance.rules.goal.title'), text: i18n.t('resistance.rules.goal.text') },
  { title: i18n.t('resistance.rules.round.title'), text: i18n.t('resistance.rules.round.text') },
  { title: i18n.t('resistance.rules.mission.title'), text: i18n.t('resistance.rules.mission.text') },
  {
    title: i18n.t('resistance.rules.plot.title'),
    text: i18n.t('resistance.rules.plot.text'),
    items: PLOT_ITEMS.map((item) => ({
      name: i18n.t(`resistance.rules.plot.items.${item}.name`),
      text: i18n.t(`resistance.rules.plot.items.${item}.text`),
    })),
    footer: i18n.t('resistance.rules.plot.footer'),
  },
  { title: i18n.t('resistance.rules.end.title'), text: i18n.t('resistance.rules.end.text') },
];
