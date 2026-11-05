using PartyKing.Domain.Events.Games.Resistance;

namespace PartyKing.Domain.Entities.Games.Resistance;

// The Assassin module (Hidden Agenda): see ResistanceRoles.Deal for the dealing, ResistanceRoles.ResolveMission for
// where the third success opens the Assassination and ResistanceKnowledge.KnowsSpy for the Commander's knowledge.
public partial class ResistanceGame
{
    /// <summary>
    /// The player the Assassin named as the Commander, once they did (public from then on: the game is over). Null
    /// while the Assassination is pending and without the module.
    /// </summary>
    public Guid? AssassinationTargetPlayerId { get; internal set; }

    /// <summary>
    /// Switches the Assassin module on or off in the rules. Allowed until the game starts, open or not. It cannot be
    /// switched on together with the Defector, the Reverser or Blind Spies.
    /// </summary>
    public ResistanceOutcome SetAssassin(bool enabled)
    {
        if (IsStarted)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.AssassinLocked, this);
        }

        if (enabled && (Rules.DefectorEnabled || Rules.ReverserEnabled || Rules.BlindSpiesEnabled))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.AssassinConflict, this);
        }

        // Rules is a JSON column compared by reference, so assign a copy to have the change detected.
        Rules = Rules.Clone(assassinEnabled: enabled);

        return Succeeded();
    }

    /// <summary>
    /// The Assassin names the player they take for the Commander. The spies win when it is the Commander, the
    /// resistance when it is anybody else. The other spies talk it over outside the app; only the Assassin acts.
    /// </summary>
    public ResistanceOutcome Assassinate(Guid? callerPlayerId, Guid targetPlayerId)
    {
        if (Inactive() is { } inactive)
        {
            return inactive;
        }

        if (Phase != ResistancePhase.Assassination)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotAssassination, this);
        }

        var assassin = Players.FirstOrDefault(x => x.Character == ResistanceCharacter.Assassin);
        if (callerPlayerId is null || assassin is null || assassin.PlayerId != callerPlayerId)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotAssassin, this);
        }

        var target = Players.FirstOrDefault(x => x.PlayerId == targetPlayerId);
        if (target is null || target.PlayerId == assassin.PlayerId)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.InvalidAssassinationTarget, this);
        }

        AssassinationTargetPlayerId = target.PlayerId;
        Phase = ResistancePhase.GameOver;
        Winner = target.Character == ResistanceCharacter.Commander ? ResistanceRole.Spy : ResistanceRole.Resistance;
        Raise(new ResistanceAssassinated(SessionId, assassin.PlayerId, target.PlayerId));
        Raise(new ResistanceGameOver(SessionId, Winner.Value));

        return Succeeded();
    }
}
