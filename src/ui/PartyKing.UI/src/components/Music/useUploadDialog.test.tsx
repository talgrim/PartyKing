import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { useUploadDialog } from './useUploadDialog';

const Probe = () => {
  const { open, show, close } = useUploadDialog();
  const location = useLocation();
  return (
    <>
      <div data-testid="state">{open ? 'open' : 'closed'}</div>
      <div data-testid="search">{location.search}</div>
      <button onClick={show}>show</button>
      <button onClick={close}>close</button>
    </>
  );
};

const renderProbe = (initialEntries: string[], initialIndex?: number) =>
  render(
    <MemoryRouter initialEntries={initialEntries} initialIndex={initialIndex}>
      <Probe />
    </MemoryRouter>,
  );

describe('useUploadDialog', () => {
  it('is open exactly when the address says ?upload=1', () => {
    renderProbe(['/s/1?upload=1']);

    expect(screen.getByTestId('state')).toHaveTextContent('open');
  });

  it('opens with a new history entry and goes back to the previous one when closed', async () => {
    const user = userEvent.setup();
    renderProbe(['/s/1']);

    await user.click(screen.getByText('show'));
    expect(screen.getByTestId('state')).toHaveTextContent('open');
    expect(screen.getByTestId('search')).toHaveTextContent('?upload=1');

    await user.click(screen.getByText('close'));
    expect(screen.getByTestId('state')).toHaveTextContent('closed');
    expect(screen.getByTestId('search')).toBeEmptyDOMElement();
  });

  it('closes a dialog that came with the address by dropping the parameter', async () => {
    const user = userEvent.setup();
    renderProbe(['/before', '/s/1?upload=1'], 1);

    await user.click(screen.getByText('close'));

    expect(screen.getByTestId('state')).toHaveTextContent('closed');
    expect(screen.getByTestId('search')).toBeEmptyDOMElement();
  });
});
