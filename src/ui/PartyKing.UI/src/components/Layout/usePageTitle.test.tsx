import { render } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { usePageTitle } from './usePageTitle';
import { setLanguage } from '@/i18n';

const Probe = () => {
  usePageTitle();
  return null;
};

const titleAt = (path: string) => {
  render(
    <MemoryRouter initialEntries={[path]}>
      <Probe />
    </MemoryRouter>,
  );

  return document.title;
};

describe('the tab title', () => {
  it.each([
    ['/', 'PartyKing · talgrim.dev'],
    ['/callback', 'PartyKing · talgrim.dev'],
    ['/admin', 'Admin · PartyKing · talgrim.dev'],
    ['/s/abc', 'Music queue · PartyKing · talgrim.dev'],
    ['/s/abc/games', 'Games · PartyKing · talgrim.dev'],
    ['/s/abc/games/ringo', 'Ringo · PartyKing · talgrim.dev'],
    ['/s/abc/games/ringo/join', 'Join the Ringo game · PartyKing · talgrim.dev'],
    ['/s/abc/games/resistance', 'Resistance · PartyKing · talgrim.dev'],
    ['/s/abc/games/resistance/join', 'Join the Resistance game · PartyKing · talgrim.dev'],
    ['/s/abc/games/resistance/icons', 'Icon guide · PartyKing · talgrim.dev'],
  ])('on %s is %s', (path, title) => {
    expect(titleAt(path)).toBe(title);
  });

  it('is in Polish when the language is', async () => {
    await setLanguage('pl');

    expect(titleAt('/s/abc/games')).toBe('Gry · PartyKing · talgrim.dev');
  });
});
