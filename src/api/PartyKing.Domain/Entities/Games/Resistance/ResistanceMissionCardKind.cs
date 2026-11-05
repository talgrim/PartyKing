namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>
/// The kinds of mission card a team member can play. The numbers are stored and match the old Fail (0) and Success (1)
/// of the boolean this replaced, so existing rows read unchanged. Only append.
/// </summary>
public enum ResistanceMissionCardKind
{
    Fail = 0,
    Success = 1,

    /// <summary>Reverser module: turns the result of the mission around. Only a Reverser plays it.</summary>
    Reverse = 2,
}
