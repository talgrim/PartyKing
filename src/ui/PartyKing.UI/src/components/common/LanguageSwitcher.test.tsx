import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Layout } from '@/components/Layout';
import { LanguageSwitcher } from '@/components/common/LanguageSwitcher';
import { i18n } from '@/i18n';
import { LANGUAGE_STORAGE_KEY } from '@/i18n/languages';

describe('LanguageSwitcher', () => {
  it('shows English as selected by default', () => {
    render(<LanguageSwitcher />);

    expect(screen.getByRole('group', { name: 'Language' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'English' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: 'Polski' })).toHaveAttribute('aria-pressed', 'false');
  });

  it('switches to Polish, remembers it and relabels itself', async () => {
    render(<LanguageSwitcher />);

    await userEvent.click(screen.getByRole('button', { name: 'Polski' }));

    expect(i18n.language).toBe('pl');
    expect(localStorage.getItem(LANGUAGE_STORAGE_KEY)).toBe('pl');
    expect(screen.getByRole('group', { name: 'Język' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Polski' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('switches back to English', async () => {
    render(<LanguageSwitcher />);
    await userEvent.click(screen.getByRole('button', { name: 'Polski' }));

    await userEvent.click(screen.getByRole('button', { name: 'English' }));

    expect(i18n.language).toBe('en');
    expect(screen.getByRole('group', { name: 'Language' })).toBeInTheDocument();
  });

  it('is part of every page through the layout', () => {
    render(
      <Layout>
        <p>page</p>
      </Layout>,
    );

    expect(screen.getByRole('group', { name: 'Language' })).toBeInTheDocument();
    expect(screen.getByText('page')).toBeInTheDocument();
  });
});
