import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { PRIVACY_NOTICE_KEY, PrivacyNotice } from './PrivacyNotice';
import { setLanguage } from '@/i18n';

const renderAt = (path = '/') =>
  render(
    <MemoryRouter initialEntries={[path]}>
      <PrivacyNotice />
    </MemoryRouter>,
  );

describe('PrivacyNotice', () => {
  beforeEach(() => localStorage.clear());

  it('tells a new visitor what PartyKing keeps and links to the policy', () => {
    renderAt();

    const notice = screen.getByRole('region', { name: 'Privacy' });
    expect(notice).toHaveTextContent('no analytics');
    expect(screen.getByRole('link', { name: 'Privacy policy' })).toHaveAttribute(
      'href',
      'https://talgrim.dev/privacy#partyking',
    );
    expect(screen.getByRole('link', { name: 'Privacy policy' })).toHaveAttribute('rel', 'noopener');
  });

  it('is gone for good after "Got it"', async () => {
    const user = userEvent.setup();
    const first = renderAt();

    await user.click(screen.getByRole('button', { name: 'Got it' }));

    expect(screen.queryByRole('region', { name: 'Privacy' })).not.toBeInTheDocument();
    expect(localStorage.getItem(PRIVACY_NOTICE_KEY)).toBe('true');

    first.unmount();
    renderAt();

    expect(screen.queryByRole('region', { name: 'Privacy' })).not.toBeInTheDocument();
  });

  it('shows again when the version of the notice changes', () => {
    localStorage.setItem('partyking.privacyNotice.v0', 'true');

    renderAt();

    expect(screen.getByRole('region', { name: 'Privacy' })).toBeInTheDocument();
  });

  it('stays away from the full-screen game boards, which have no room for it', () => {
    renderAt('/s/s1/games/ringo');

    expect(screen.queryByRole('region', { name: 'Privacy' })).not.toBeInTheDocument();
  });

  it('is shown on the join page of a game, which is not a board', () => {
    renderAt('/s/s1/games/ringo/join');

    expect(screen.getByRole('region', { name: 'Privacy' })).toBeInTheDocument();
  });

  it('speaks Polish and links to the Polish policy', async () => {
    await setLanguage('pl');
    renderAt();

    expect(screen.getByRole('region', { name: 'Prywatność' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Polityka prywatności' })).toHaveAttribute(
      'href',
      'https://talgrim.dev/pl/privacy#partyking',
    );
    expect(screen.getByRole('button', { name: 'Rozumiem' })).toBeInTheDocument();
  });
});
