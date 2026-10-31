import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { setLanguage } from '@/i18n';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { ResistanceMissionCards } from './ResistanceMissionCards';
import { ResistancePlotLog } from './ResistancePlotLog';
import { ResistanceViewSwitch } from './ResistanceViewSwitch';
import { PLOT_CARDS } from './resistancePlotCards';
import { resistancePhaseDescription } from './resistancePhase';
import { resistanceRules } from './resistanceRules';

const closedGame = { isOpen: false, players: [], startedAt: null } as unknown as ResistanceGameDto;

describe('Resistance in Polish', () => {
  it('names the plot cards in Polish and in English', async () => {
    expect(PLOT_CARDS.NoConfidence.name).toBe('No Confidence');

    await setLanguage('pl');

    expect(PLOT_CARDS.NoConfidence.name).toBe('Nie ufajcie mu');
    expect(PLOT_CARDS.StrongLeader.name).toBe('Charyzmatyczny Przywódca');
  });

  it('writes the phase line in Polish', async () => {
    await setLanguage('pl');

    expect(resistancePhaseDescription(closedGame, { canManage: true, isAdmin: true }, 2)).toMatch(/Otwórz ją dla gości/);
  });

  it('translates the rules dialog sections', async () => {
    const english = resistanceRules().map((section) => section.title);

    await setLanguage('pl');

    const polish = resistanceRules().map((section) => section.title);
    expect(polish).toHaveLength(english.length);
    expect(polish).not.toEqual(english);
  });

  it('labels the view switch and the mission cards', async () => {
    await setLanguage('pl');
    render(
      <>
        <ResistanceViewSwitch showBoard={false} onChange={() => undefined} />
        <ResistanceMissionCards cardCount={2} failCount={1} />
      </>,
    );

    expect(screen.getByRole('button', { name: 'Gracz' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Plansza' })).toBeInTheDocument();
    expect(screen.getByText('Karty misji')).toBeInTheDocument();
    expect(screen.getAllByRole('img', { name: 'Porażka' })).toHaveLength(1);
    expect(screen.getAllByRole('img', { name: 'Sukces' })).toHaveLength(1);
  });

  it('logs a played card in Polish', async () => {
    await setLanguage('pl');
    render(
      <ResistancePlotLog
        players={[
          { id: 'p1', name: 'Ala' },
          { id: 'p2', name: 'Bartek' },
        ]}
        cards={[
          {
            id: 1,
            type: 'OpenUp',
            holderPlayerId: 'p1',
            targetPlayerId: 'p2',
            missionNumber: 2,
            isConfirmed: false,
          } as never,
        ]}
      />,
    );

    const item = screen.getByRole('listitem');
    expect(item).toHaveTextContent('Misja 2:');
    expect(item).toHaveTextContent('Ala zagrał/a „Tylko tobie zaufam”');
    expect(item).toHaveTextContent('(czeka na potwierdzenie)');
  });
});
