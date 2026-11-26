import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { PrivacyFooter } from './PrivacyFooter';
import { setLanguage } from '@/i18n';

const renderAt = (path = '/') =>
  render(
    <MemoryRouter initialEntries={[path]}>
      <PrivacyFooter />
    </MemoryRouter>,
  );

describe('PrivacyFooter', () => {
  it('links to the privacy policy of the sites', () => {
    renderAt();

    expect(screen.getByRole('contentinfo').querySelector('a')).toHaveAttribute('href', 'https://talgrim.dev/privacy');
    expect(screen.getByRole('link', { name: 'Privacy policy' })).toHaveAttribute('target', '_blank');
  });

  it('links to the Polish policy for a Polish page', async () => {
    await setLanguage('pl');
    renderAt();

    expect(screen.getByRole('link', { name: 'Polityka prywatności' })).toHaveAttribute(
      'href',
      'https://talgrim.dev/pl/privacy',
    );
  });

  it.each(['/s/s1/games/ringo', '/s/s1/games/resistance'])('is left out of the full-screen board %s', (path) => {
    renderAt(path);

    expect(screen.queryByRole('contentinfo')).not.toBeInTheDocument();
  });

  it('stays on the pages around the boards', () => {
    renderAt('/s/s1/games');

    expect(screen.getByRole('contentinfo')).toBeInTheDocument();
  });
});
