import userEvent from '@testing-library/user-event';
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import {
  CardBackIcon,
  DefectorCardIcon,
  CommanderCardIcon,
  AssassinCardIcon,
  InquisitorCardIcon,
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
  ReverserCardIcon,
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
  InquisitorCardIcon,
  ReverserCardIcon,
  DefectorCardIcon,
  CommanderCardIcon,
  AssassinCardIcon,
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
    expect(screen.getByText('2 FAILS')).toBeInTheDocument();
    expect(screen.getAllByText('3')).toHaveLength(1);
    expect(screen.getByText('MISSION 5')).toBeInTheDocument();
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
    const badges = screen.getAllByRole('img', { name: /Team of mission/ });
    expect(badges).toHaveLength(1);
    expect(badges[0]).toHaveAccessibleName('Team of mission 1: Ala, Bob');

    await userEvent.hover(badges[0]);
    expect(await screen.findByText('Bob')).toBeInTheDocument();
    expect(screen.getByText('succeeded')).toBeInTheDocument();
  });

  it('draws a flipped switch card on the disc of the mission it was flipped for, and none before mission 3', () => {
    render(
      <MissionBoardIcon
        title="plansza"
        teamSizes={[2, 3, 3, 4, 4]}
        results={['success', 'success', null, null, null]}
        currentMission={2}
        rejectedTeams={0}
        fourthMissionNeedsTwoFails={false}
        defectorFlips={[true, false]}
      />,
    );

    expect(screen.getByRole('img', { name: 'Mission 3: Change Allegiance' })).toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'Mission 4: No Change' })).toBeInTheDocument();
    expect(screen.queryByRole('img', { name: /Mission [125]: / })).not.toBeInTheDocument();
  });

  it('explains a switch card in a tooltip on hover', async () => {
    render(
      <MissionBoardIcon
        title="plansza"
        teamSizes={[2, 3, 3, 4, 4]}
        results={['success', 'success', null, null, null]}
        currentMission={2}
        rejectedTeams={0}
        fourthMissionNeedsTwoFails={false}
        defectorFlips={[true]}
      />,
    );

    await userEvent.hover(screen.getByRole('img', { name: 'Mission 3: Change Allegiance' }));

    expect(await screen.findByText('Switch card: both Defectors changed sides. Nobody is told who they are.')).toBeInTheDocument();
  });

  it('draws no switch card without flips', () => {
    render(
      <MissionBoardIcon
        title="plansza"
        teamSizes={[2, 3, 3, 4, 4]}
        results={[null, null, null, null, null]}
        currentMission={0}
        rejectedTeams={0}
        fourthMissionNeedsTwoFails={false}
      />,
    );

    expect(screen.queryByRole('img', { name: /No Change|Change Allegiance/ })).not.toBeInTheDocument();
  });
});
