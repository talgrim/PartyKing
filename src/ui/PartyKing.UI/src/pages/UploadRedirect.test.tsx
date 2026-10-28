import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { UploadRedirect } from './UploadRedirect';

const Where = () => {
  const { pathname, search } = useLocation();
  return <div>{`${pathname}${search}`}</div>;
};

describe('UploadRedirect', () => {
  it('sends the old upload address to the dialog on the session page', () => {
    render(
      <MemoryRouter initialEntries={['/s/abc/upload']}>
        <Routes>
          <Route path="/s/:sessionId/upload" element={<UploadRedirect />} />
          <Route path="/s/:sessionId" element={<Where />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(screen.getByText('/s/abc?upload=1')).toBeVisible();
  });
});
