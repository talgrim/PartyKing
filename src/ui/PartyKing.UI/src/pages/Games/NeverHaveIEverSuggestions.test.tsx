import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { NeverHaveIEverSuggestions } from './NeverHaveIEverSuggestions';

const mocks = vi.hoisted(() => ({ getSuggestions: vi.fn() }));

vi.mock('@/service/endpoints/games/gamesApi', () => ({
  gamesApi: { getNeverHaveIEverSuggestions: mocks.getSuggestions },
}));

const clickSuggest = async () => {
  render(<NeverHaveIEverSuggestions sessionId="session-1" />);
  await userEvent.click(screen.getByRole('button', { name: 'Suggest statements' }));
};

describe('NeverHaveIEverSuggestions', () => {
  beforeEach(() => {
    mocks.getSuggestions.mockReset();
  });

  it('shows the statements of the model', async () => {
    mocks.getSuggestions.mockResolvedValue({
      suggestions: ['Nigdy w życiu nie A.', 'Nigdy w życiu nie B.'],
      fromFallback: false,
    });

    await clickSuggest();

    expect(await screen.findByText('Nigdy w życiu nie A.')).toBeInTheDocument();
    expect(screen.getByText('Nigdy w życiu nie B.')).toBeInTheDocument();
    expect(screen.queryByText(/built-in list/)).not.toBeInTheDocument();
    expect(mocks.getSuggestions).toHaveBeenCalledWith('session-1');
  });

  it('tells the host when the statements come from the built-in list', async () => {
    mocks.getSuggestions.mockResolvedValue({ suggestions: ['Nigdy w życiu nie C.'], fromFallback: true });

    await clickSuggest();

    expect(await screen.findByText(/built-in list/)).toBeInTheDocument();
    expect(screen.getByText('Nigdy w życiu nie C.')).toBeInTheDocument();
  });

  it('asks the host to wait when requests are rate limited', async () => {
    mocks.getSuggestions.mockRejectedValue({ status: 429, message: 'Too many' });

    await clickSuggest();

    expect(await screen.findByText(/Too many requests/)).toBeInTheDocument();
  });

  it('says so when the suggestions could not be fetched', async () => {
    mocks.getSuggestions.mockRejectedValue({ status: 500, message: 'Boom' });

    await clickSuggest();

    expect(await screen.findByText('Could not get suggestions')).toBeInTheDocument();
  });

  it('disables the button while waiting for the answer', async () => {
    mocks.getSuggestions.mockReturnValue(new Promise(() => undefined));

    await clickSuggest();

    expect(screen.getByRole('button', { name: 'Suggest statements' })).toBeDisabled();
  });
});
