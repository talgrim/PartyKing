import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RingoRuleSuggestions } from './RingoRuleSuggestions';

const mocks = vi.hoisted(() => ({ getSuggestions: vi.fn() }));

vi.mock('@/service/endpoints/games/gamesApi', () => ({
  gamesApi: { getRingoRuleSuggestions: mocks.getSuggestions },
}));

const onSuggested = vi.fn();

const renderAndClick = async (suggestedRules: string[] = []) => {
  render(<RingoRuleSuggestions sessionId="session-1" suggestedRules={suggestedRules} onSuggested={onSuggested} />);
  await userEvent.click(screen.getByRole('button', { name: 'Zaproponuj zasady' }));
};

describe('RingoRuleSuggestions', () => {
  beforeEach(() => {
    mocks.getSuggestions.mockReset();
    onSuggested.mockReset();
  });

  it('shows the rules of the model and reports them', async () => {
    mocks.getSuggestions.mockResolvedValue({ suggestions: ['Reguła A.', 'Reguła B.'], fromFallback: false });

    await renderAndClick();

    expect(await screen.findByText('Reguła A.')).toBeInTheDocument();
    expect(screen.getByText('Reguła B.')).toBeInTheDocument();
    expect(screen.queryByText(/wbudowanej listy/)).not.toBeInTheDocument();
    expect(onSuggested).toHaveBeenCalledWith(['Reguła A.', 'Reguła B.']);
  });

  it('sends the rules suggested so far', async () => {
    mocks.getSuggestions.mockResolvedValue({ suggestions: ['Reguła C.'], fromFallback: false });

    await renderAndClick(['Reguła A.']);

    expect(mocks.getSuggestions).toHaveBeenCalledWith('session-1', ['Reguła A.']);
  });

  it('tells the host when the rules come from the built-in list', async () => {
    mocks.getSuggestions.mockResolvedValue({ suggestions: ['Reguła D.'], fromFallback: true });

    await renderAndClick();

    expect(await screen.findByText(/wbudowanej listy/)).toBeInTheDocument();
  });

  it('asks the host to wait when requests are rate limited', async () => {
    mocks.getSuggestions.mockRejectedValue({ status: 429, message: 'Too many' });

    await renderAndClick();

    expect(await screen.findByText(/Za dużo zapytań/)).toBeInTheDocument();
    expect(onSuggested).not.toHaveBeenCalled();
  });

  it('says so when the rules could not be fetched', async () => {
    mocks.getSuggestions.mockRejectedValue({ status: 500, message: 'Boom' });

    await renderAndClick();

    expect(await screen.findByText('Nie udało się pobrać propozycji')).toBeInTheDocument();
  });

  it('disables the button while waiting for the answer', async () => {
    mocks.getSuggestions.mockReturnValue(new Promise(() => undefined));

    await renderAndClick();

    expect(screen.getByRole('button', { name: 'Zaproponuj zasady' })).toBeDisabled();
  });
});
