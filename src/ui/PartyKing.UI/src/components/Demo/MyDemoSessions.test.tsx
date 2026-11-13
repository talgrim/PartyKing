import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { MyDemoSessions } from './MyDemoSessions';

const mocks = vi.hoisted(() => ({ getConfig: vi.fn(), listMine: vi.fn(), resume: vi.fn() }));

vi.mock('@/service/endpoints/demo/demoApi', () => ({
  demoApi: { getConfig: mocks.getConfig, listMine: mocks.listMine, resume: mocks.resume },
}));

const inHours = (hours: number) => new Date(Date.now() + hours * 3600 * 1000).toISOString();

const answer = (...sessions: { sessionId: string; name: string; expiresAt: string }[]) => ({
  sessions,
  serverTime: new Date().toISOString(),
});

const renderList = () =>
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
      <MemoryRouter>
        <Routes>
          <Route path="/" element={<MyDemoSessions />} />
          <Route path="/s/:id" element={<p>The party page</p>} />
        </Routes>
      </MemoryRouter>
    </SWRConfig>,
  );

describe('MyDemoSessions', () => {
  beforeEach(() => {
    mocks.getConfig.mockReset().mockResolvedValue({ enabled: true, serverTime: new Date().toISOString() });
    mocks.listMine.mockReset();
    mocks.resume.mockReset();
  });

  it('lists the visitor\'s sessions with the time each has left', async () => {
    mocks.listMine.mockResolvedValue(
      answer(
        { sessionId: 's2', name: 'Demo party', expiresAt: inHours(2.5) },
        { sessionId: 's1', name: 'Birthday', expiresAt: inHours(0.5) },
      ),
    );

    renderList();

    expect(await screen.findByText('Birthday')).toBeVisible();
    expect(screen.getByText('Demo party')).toBeVisible();
    expect(screen.getByText(/^Ends in 2:\d\d:\d\d$/)).toBeVisible();
    expect(screen.getByText(/^Ends in \d\d:\d\d$/)).toBeVisible();
    expect(screen.getAllByRole('button', { name: 'Back to the party' })).toHaveLength(2);
  });

  it('shows nothing without a session, so the landing page is just the start button', async () => {
    mocks.listMine.mockResolvedValue(answer());

    renderList();

    await vi.waitFor(() => expect(mocks.listMine).toHaveBeenCalled());
    expect(screen.queryByRole('heading', { name: 'Your demo parties' })).not.toBeInTheDocument();
  });

  it('asks nothing on a server that is not a demo', async () => {
    mocks.getConfig.mockResolvedValue({ enabled: false, serverTime: new Date().toISOString() });

    renderList();

    await vi.waitFor(() => expect(mocks.getConfig).toHaveBeenCalled());
    expect(mocks.listMine).not.toHaveBeenCalled();
  });

  it('makes the visitor the host again and opens the session', async () => {
    mocks.listMine.mockResolvedValue(answer({ sessionId: 's1', name: 'Birthday', expiresAt: inHours(1) }));
    mocks.resume.mockResolvedValue({ sessionId: 's1', expiresAt: inHours(1) });
    renderList();

    await userEvent.click(await screen.findByRole('button', { name: 'Back to the party' }));

    expect(mocks.resume).toHaveBeenCalledWith('s1');
    expect(await screen.findByText('The party page')).toBeVisible();
  });

  it('says so, and asks the list again, when the session has ended meanwhile', async () => {
    mocks.listMine
      .mockResolvedValueOnce(answer({ sessionId: 's1', name: 'Birthday', expiresAt: inHours(1) }))
      .mockResolvedValue(answer());
    mocks.resume.mockRejectedValue({ status: 404, message: 'Not found' });
    renderList();

    await userEvent.click(await screen.findByRole('button', { name: 'Back to the party' }));

    expect(await screen.findByText('That party has ended.')).toBeVisible();
    await vi.waitFor(() => expect(mocks.listMine).toHaveBeenCalledTimes(2));
  });

  it('hides a session once its time is up', async () => {
    mocks.listMine.mockResolvedValue(
      answer({ sessionId: 's1', name: 'Birthday', expiresAt: new Date(Date.now() - 1000).toISOString() }),
    );

    renderList();

    await vi.waitFor(() => expect(mocks.listMine).toHaveBeenCalled());
    expect(screen.queryByText('Birthday')).not.toBeInTheDocument();
  });
});
