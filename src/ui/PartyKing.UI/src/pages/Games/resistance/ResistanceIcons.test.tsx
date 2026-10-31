import userEvent from '@testing-library/user-event';
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
  PlotCloseEyeCardIcon,
  PlotEstablishConfidenceCardIcon,
  PlotNoConfidenceCardIcon,
  PlotOpenUpCardIcon,
  PlotOpinionMakerCardIcon,
  PlotOverheardConversationCardIcon,
  PlotSpotlightCardIcon,
  PlotStrongLeaderCardIcon,
  PlotTakeResponsibilityCardIcon,
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
  PlotCloseEyeCardIcon,
  PlotEstablishConfidenceCardIcon,
  PlotStrongLeaderCardIcon,
  PlotOverheardConversationCardIcon,
  PlotNoConfidenceCardIcon,
  PlotSpotlightCardIcon,
  PlotOpinionMakerCardIcon,
  PlotOpenUpCardIcon,
  PlotTakeResponsibilityCardIcon,
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

  it('shows an info badge with the team only for finished missions that have one', async () => {
    render(
      <MissionBoardIcon
        title="plansza"
        teamSizes={[2, 3, 3, 4, 4]}
        results={['success', 'fail', null, null, null]}
        teams={[['Ala', 'Bob'], [], null, null, null]}
        currentMission={2}
        rejectedTeams={0}
        fourthMissionNeedsTwoFails={false}
      />,
    );
    const badges = screen.getAllByRole('img', { name: /Drużyna misji/ });
    expect(badges).toHaveLength(1);
    expect(badges[0]).toHaveAccessibleName('Drużyna misji 1: Ala, Bob');

    await userEvent.hover(badges[0]);
    expect(await screen.findByText('Bob')).toBeInTheDocument();
    expect(screen.getByText('udana')).toBeInTheDocument();
  });
});
