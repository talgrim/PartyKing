using PartyKing.Domain.Entities.Games;

namespace PartyKing.Infrastructure.Results;

public enum ResistanceTeamStatus
{
    NoGame,
    NotStarted,
    GameOver,

    /// <summary>The caller is not the player leading the mission.</summary>
    NotLeader,
    AlreadySubmitted,

    /// <summary>The leader still has to hand out the plot cards.</summary>
    CardsNotDealt,

    /// <summary>Not exactly <see cref="ResistanceRules.TeamSize"/> distinct players.</summary>
    WrongSize,
    UnknownPlayer,
    Selected,
}
