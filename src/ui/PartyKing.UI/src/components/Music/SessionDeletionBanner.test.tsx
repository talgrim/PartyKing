import { render, screen } from '@testing-library/react';
import { SessionDeletionBanner } from './SessionDeletionBanner';
import { setLanguage } from '@/i18n';

describe('SessionDeletionBanner', () => {
  afterEach(() => setLanguage('en'));

  it('tells everyone when the session will be deleted, and offers nothing to press', () => {
    render(<SessionDeletionBanner deletionScheduledAt="2026-11-28T14:05:00Z" />);

    expect(screen.getByText('Scheduled for deletion')).toBeVisible();
    expect(screen.getByText(/This session will be deleted on .*November.*2026.*, together with its queue, games and photos\./)).toBeVisible();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('writes the date in the selected language', async () => {
    await setLanguage('pl');

    render(<SessionDeletionBanner deletionScheduledAt="2026-11-28T14:05:00Z" />);

    expect(screen.getByText(/Ta sesja zostanie usunięta .*listopada.*2026/)).toBeVisible();
  });
});
