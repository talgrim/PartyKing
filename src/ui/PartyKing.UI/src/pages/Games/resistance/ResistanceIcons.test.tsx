import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import {
  CardBackIcon,
  LeaderTokenIcon,
  MissionBoardIcon,
  MissionFailCardIcon,
  MissionSuccessCardIcon,
  OperativeCardIcon,
  PlotCardIcon,
  SpyCardIcon,
  TeamTokenIcon,
  VoteApproveCardIcon,
  VoteRejectCardIcon,
} from './ResistanceIcons';

const cards = {
  CardBackIcon,
  SpyCardIcon,
  OperativeCardIcon,
  LeaderTokenIcon,
  TeamTokenIcon,
  VoteApproveCardIcon,
  VoteRejectCardIcon,
  MissionSuccessCardIcon,
  MissionFailCardIcon,
  PlotCardIcon,
};

describe('ResistanceIcons', () => {
  it.each(Object.entries(cards))('%s is exposed with its title', (_, Icon) => {
    render(<Icon title="ikona" />);
    expect(screen.getByRole('img', { name: 'ikona' })).toBeInTheDocument();
  });

  it('hides an icon without a title from assistive technology', () => {
    const { container } = render(<SpyCardIcon />);
    expect(container.querySelector('svg')).toHaveAttribute('aria-hidden', 'true');
  });

  it('shows team sizes on unplayed missions and the two-fails note', () => {
    render(
      <MissionBoardIcon
        title="plansza"
        teamSizes={[2, 3, 3, 4, 4]}
        results={['success', 'fail', null, null, null]}
        currentMission={2}
        rejectedTeams={2}
        fourthMissionNeedsTwoFails
      />,
    );
    expect(screen.getByText('2 PORAŻKI')).toBeInTheDocument();
    expect(screen.getAllByText('3')).toHaveLength(1);
    expect(screen.getByText('MISJA 5')).toBeInTheDocument();
  });
});
