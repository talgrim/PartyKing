namespace PartyKing.Contract.V1.Games.Resistance;

/// <summary>
/// The player Take Responsibility takes a card from. <paramref name="TakeOpinionMaker"/> picks the face-up Opinion
/// Maker (true) or a random card from the hand (false); needed only when the target has both.
/// </summary>
public record ResistanceTakeResponsibilityRequest(Guid TargetPlayerId, bool? TakeOpinionMaker);
