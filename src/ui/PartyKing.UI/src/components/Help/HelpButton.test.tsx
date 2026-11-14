import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { HELP_SEEN_KEY, HelpButton } from './HelpButton';
import { helpTarget } from './helpSteps';

const mocks = vi.hoisted(() => ({ getDemoConfig: vi.fn() }));

vi.mock('@/service/endpoints/demo/demoApi', () => ({ demoApi: { getConfig: mocks.getDemoConfig } }));

const renderButton = (canManage = true) =>
  render(
    <SWRConfig value={{ provider: () => new Map() }}>
      <div {...helpTarget('games')}>Games</div>
      <HelpButton canManage={canManage} />
    </SWRConfig>,
  );

const demo = (enabled: boolean) => mocks.getDemoConfig.mockResolvedValue({ enabled, serverTime: '2026-11-13T12:00:00Z' });

describe('HelpButton', () => {
  beforeEach(() => localStorage.clear());

  it('is not there outside a demo', async () => {
    demo(false);
    renderButton();

    await waitFor(() => expect(mocks.getDemoConfig).toHaveBeenCalled());
    expect(screen.queryByRole('button', { name: 'How this demo works' })).not.toBeInTheDocument();
  });

  it('is there in a demo, with a turning border until it was clicked', async () => {
    demo(true);
    renderButton();

    expect(await screen.findByRole('button', { name: 'How this demo works' })).toHaveClass('help-attention');
  });

  it('stops turning for a visitor who has used it before', async () => {
    demo(true);
    localStorage.setItem(HELP_SEEN_KEY, 'true');
    renderButton();

    expect(await screen.findByRole('button', { name: 'How this demo works' })).not.toHaveClass('help-attention');
  });

  it('starts the tour on a click and remembers it, so the border stops turning', async () => {
    demo(true);
    renderButton();

    const button = await screen.findByRole('button', { name: 'How this demo works' });
    await userEvent.click(button);

    expect(await screen.findByRole('dialog')).toBeVisible();
    expect(localStorage.getItem(HELP_SEEN_KEY)).toBe('true');
    expect(button).not.toHaveClass('help-attention');
  });

  it('gives the host a longer tour than a guest', async () => {
    demo(true);
    const stepCount = async (canManage: boolean) => {
      const { unmount } = renderButton(canManage);
      await userEvent.click(await screen.findByRole('button', { name: 'How this demo works' }));
      const counter = (await screen.findByText(/^\d+ of \d+$/)).textContent ?? '';
      unmount();
      return Number(counter.split(' of ')[1]);
    };

    expect(await stepCount(true)).toBeGreaterThan(await stepCount(false));
  });
});
