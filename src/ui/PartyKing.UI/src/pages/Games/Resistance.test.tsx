import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { Resistance } from './Resistance';

const mocks = vi.hoisted(() => ({ role: { isHost: false, isAdmin: true } }));

vi.mock('@/service/endpoints/sessions/useViewerRole', () => ({ useViewerRole: () => mocks.role }));

const renderPage = () =>
  render(
    <MemoryRouter initialEntries={['/s/session-1/games/resistance']}>
      <Routes>
        <Route path="/s/:sessionId/games/resistance" element={<Resistance />} />
      </Routes>
    </MemoryRouter>,
  );

describe('Resistance', () => {
  beforeEach(() => {
    mocks.role = { isHost: false, isAdmin: true };
  });

  it('shows the mission board', () => {
    renderPage();

    expect(screen.getByRole('img', { name: 'Mission board' })).toBeInTheDocument();
  });

  it('opens the rules from the info button', async () => {
    renderPage();

    await userEvent.click(screen.getByRole('button', { name: 'Rules' }));

    expect(await screen.findByText('Zasady gry Resistance')).toBeInTheDocument();
  });

  it('shows the admin a disabled Open button and links back to the games', () => {
    renderPage();

    expect(screen.getByRole('button', { name: 'Open the game' })).toBeDisabled();
    expect(screen.getByRole('link', { name: 'Back to the games' })).toHaveAttribute('href', '/s/session-1/games');
  });

  it('gives guests no Open button and links back to the session', () => {
    mocks.role = { isHost: false, isAdmin: false };
    renderPage();

    expect(screen.queryByRole('button', { name: 'Open the game' })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Back to the session' })).toHaveAttribute('href', '/s/session-1');
  });
});
