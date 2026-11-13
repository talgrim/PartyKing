import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { StartDemoButton } from './StartDemoButton';

const mocks = vi.hoisted(() => ({ start: vi.fn() }));

vi.mock('@/service/endpoints/demo/demoApi', () => ({ demoApi: { start: mocks.start } }));

const renderButton = () =>
  render(
    <MemoryRouter initialEntries={['/']}>
      <Routes>
        <Route path="/" element={<StartDemoButton />} />
        <Route path="/s/:sessionId" element={<div>the session</div>} />
      </Routes>
    </MemoryRouter>,
  );

describe('StartDemoButton', () => {
  // A block body: vitest runs a function returned from beforeEach as a cleanup, and that is the mock
  beforeEach(() => {
    mocks.start.mockReset();
  });

  it('starts a demo and opens its session', async () => {
    mocks.start.mockResolvedValue({ sessionId: 'abc', expiresAt: '2026-11-13T15:00:00Z' });
    renderButton();

    await userEvent.click(screen.getByRole('button', { name: 'Start the demo' }));

    expect(await screen.findByText('the session')).toBeVisible();
  });

  it.each([
    [409, /demo is full/],
    [429, /Too many demos/],
    [500, /Could not start the demo/],
    [undefined, /Could not start the demo/],
  ])('says why it could not start on status %s', async (status, message) => {
    mocks.start.mockRejectedValue({ status, message: 'server text' });
    renderButton();

    await userEvent.click(screen.getByRole('button', { name: 'Start the demo' }));

    expect(await screen.findByText(message)).toBeVisible();
    expect(screen.getByRole('button', { name: 'Start the demo' })).toBeEnabled();
  });
});
