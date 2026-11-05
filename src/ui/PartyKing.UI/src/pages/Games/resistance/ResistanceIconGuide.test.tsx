import { render, screen, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { PLOT_CARDS } from './resistancePlotCards';
import { ResistanceIconGuide } from './ResistanceIconGuide';

const renderGuide = () =>
  render(
    <MemoryRouter initialEntries={['/s/session-1/games/resistance/icons']}>
      <Routes>
        <Route path="/s/:sessionId/games/resistance/icons" element={<ResistanceIconGuide />} />
        <Route path="/s/:sessionId/games/resistance" element={<p>Game page</p>} />
      </Routes>
    </MemoryRouter>,
  );

const group = (name: string) => screen.getByRole('region', { name });

describe('ResistanceIconGuide', () => {
  it('groups the icons by the base game and every module', () => {
    renderGuide();

    expect(screen.getAllByRole('heading', { level: 2 }).map((heading) => heading.textContent)).toEqual([
      'Base game',
      'Plot cards (add-on)',
      'Inquisitor (module)',
      'Reverser (module)',
      'Defectors (module)',
    ]);
  });

  it('lists every Plot card with its effect', () => {
    renderGuide();

    const plot = within(group('Plot cards (add-on)'));
    const cards = Object.values(PLOT_CARDS);
    expect(plot.getAllByRole('listitem')).toHaveLength(cards.length);
    cards.forEach((card) => {
      expect(plot.getByRole('heading', { name: card.name })).toBeInTheDocument();
      expect(plot.getByText(card.effect)).toBeInTheDocument();
    });
  });

  it('explains the characters and their cards of each module', () => {
    renderGuide();

    expect(within(group('Reverser (module)')).getByText(/odd number of Reverse cards/)).toBeInTheDocument();
    expect(within(group('Reverser (module)')).getByRole('heading', { name: 'Reverser' })).toBeInTheDocument();
    expect(within(group('Defectors (module)')).getByRole('heading', { name: 'Switch card: Change Allegiance' })).toBeInTheDocument();
    expect(within(group('Defectors (module)')).getByRole('heading', { name: 'Switch card: No Change' })).toBeInTheDocument();
    expect(within(group('Inquisitor (module)')).getByRole('heading', { name: 'Inquisitor' })).toBeInTheDocument();
  });

  it('describes the cards of the base game', () => {
    renderGuide();

    const base = within(group('Base game'));
    ['Card back', 'Spy', 'Resistance', 'Fellow spy', 'Leader', 'Approve', 'Reject', 'Success', 'Fail'].forEach((name) => {
      expect(base.getByRole('heading', { name })).toBeInTheDocument();
    });
  });

  it('links back to the game', () => {
    renderGuide();

    expect(screen.getByRole('link', { name: 'Back to the game' })).toHaveAttribute('href', '/s/session-1/games/resistance');
  });
});
