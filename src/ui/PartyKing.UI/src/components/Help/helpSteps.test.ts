import en from '@/i18n/locales/en.json';
import pl from '@/i18n/locales/pl.json';
import { HELP_STEPS, helpTarget, stepsFor } from './helpSteps';

describe('helpSteps', () => {
  it('gives a host the player, the QR code and the settings, which a guest has no use for', () => {
    const host = stepsFor(true).map((step) => step.target);
    const guest = stepsFor(false).map((step) => step.target);

    expect(host).toEqual(expect.arrayContaining(['player', 'qr', 'settings']));
    expect(guest).not.toEqual(expect.arrayContaining(['player']));
    expect(guest).not.toContain('qr');
    expect(guest).not.toContain('settings');
  });

  it('has one text per target for each kind of visitor', () => {
    for (const isHost of [true, false]) {
      const targets = stepsFor(isHost).map((step) => step.target);
      expect(new Set(targets).size).toBe(targets.length);
    }
  });

  it('keeps the steps that point into the drawer marked, so the tour opens it', () => {
    expect(HELP_STEPS.filter((step) => step.target === 'slideshow').every((step) => step.needsDrawer)).toBe(true);
  });

  it.each([
    ['English', en],
    ['Polish', pl],
  ])('has a title and a text in %s for every step', (_, locale) => {
    for (const step of HELP_STEPS) {
      const entry = locale.help.steps[step.text];
      expect(entry.title).toBeTruthy();
      expect(entry.text).toBeTruthy();
    }
  });

  it('marks an element with its name', () => {
    expect(helpTarget('games')).toEqual({ 'data-help': 'games' });
  });
});
