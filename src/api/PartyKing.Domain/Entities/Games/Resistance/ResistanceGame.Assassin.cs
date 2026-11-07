using PartyKing.Domain.Events.Games.Resistance;

namespace PartyKing.Domain.Entities.Games.Resistance;

// The Assassin module (Hidden Agenda): see ResistanceDealer.Deal for the dealing, ResistanceFlow.ResolveMission for
// where the third success opens the Assassination and ResistanceKnowledge.KnowsSpy for the Commander's knowledge.
public partial class ResistanceGame
{
    /// <summary>
    /// The player the Assassin named as the Commander, once they did (public from then on: the game is over). Null
    /// while the Assassination is pending and without the module.
    /// </summary>
    public Guid? AssassinationTargetPlayerId { get; internal set; }

    /// <summary>
    /// Switches the Assassin module on or off in the rules and, while it is on, the optional characters (Body Guard,
    /// Deep Cover, Blind Spy, False Commander; an omitted one keeps its choice). Switching the module off switches them
    /// off too. Allowed until the game starts, open or not. It cannot be switched on together with the Defector, the
    /// Reverser or Blind Spies. Whether the characters fit the number of players is checked at the start.
    /// </summary>
    public ResistanceOutcome SetAssassin(
        bool enabled,
        bool? bodyGuard = null,
        bool? deepCover = null,
        bool? blindSpy = null,
        bool? falseCommander = null)
    {
        return SwitchModule(
            ResistanceModule.Assassin,
            enabled,
            x =>
            {
                // Switching the module off already switched its characters off (see AssassinModuleBehavior); an omitted choice keeps its value
                if (enabled)
                {
                    x.BodyGuardEnabled = bodyGuard ?? x.BodyGuardEnabled;
                    x.DeepCoverEnabled = deepCover ?? x.DeepCoverEnabled;
                    x.BlindSpyEnabled = blindSpy ?? x.BlindSpyEnabled;
                    x.FalseCommanderEnabled = falseCommander ?? x.FalseCommanderEnabled;
                }
            });
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
