using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>A person who joined a Resistance game by name. The seating order is <see cref="LobbyPlayer.Order"/>.</summary>
public class ResistancePlayer : LobbyPlayer, ILobbyPlayerFactory<ResistancePlayer>
{
    internal ResistancePlayer()
    {
    }

    public static ResistancePlayer Create(Guid playerId, string name, string? profileId, DateTimeOffset joinedAt, bool isAdminSeat = false)
    {
        var player = new ResistancePlayer();
        player.Initialize(playerId, name, profileId, joinedAt, isAdminSeat);

        return player;
    }

    /// <summary>
    /// The side the player is on at the moment (their allegiance), which decides the win checks and which mission
    /// cards they may play. Not the character: see <see cref="Character"/>. Readers that show it to somebody go
    /// through <see cref="ResistanceKnowledge.Loyalty"/>. Null until the game is started.
    /// </summary>
    public ResistanceRole? Role { get; internal set; }

    /// <summary>
    /// Puts the player on the other side (a spy becomes Resistance and the other way round). The character stays. Does
    /// nothing before the game is started, while <see cref="Role"/> is null.
    /// </summary>
    internal void SwitchAllegiance()
    {
        Role = Role switch
        {
            ResistanceRole.Spy => ResistanceRole.Resistance,
            ResistanceRole.Resistance => ResistanceRole.Spy,
            _ => Role,
        };
    }

    /// <summary>Whether the leader put the player on the team of the current mission.</summary>
    public bool IsOnTeam { get; internal set; }

    /// <summary>
    /// The player's vote on the submitted team (true approves); null until they voted. Kept after the reveal so
    /// everybody sees who voted how, and cleared when the next team is picked.
    /// </summary>
    public bool? Vote { get; internal set; }

    /// <summary>
    /// The card the player secretly played on the approved team; null until they played. Never sent to anyone else
    /// (but for the one In the Spotlight shows), and cleared when the next team is picked.
    /// </summary>
    public ResistanceMissionCardKind? MissionCard { get; internal set; }

    /// <summary>
    /// The player's special character, dealt at the start together with the <see cref="Role"/> and secret like it. It never changes during the game,
    /// unlike the allegiance.
    /// <see cref="ResistanceCharacter.Standard"/> unless a module deals characters.
    /// </summary>
    public ResistanceCharacter Character { get; internal set; }

    /// <summary>
    /// Whether the host or an admin released the seat, so a guest may take it over from a device that is gone (see
    /// <see cref="ResistanceGame.ClaimSeat"/>). Cleared by the claim, by the host and when the game restarts.
    /// </summary>
    public bool IsReclaimable { get; internal set; }

    /// <summary>
    /// Inquisitor module: whether the player holds or held the token, so nobody can examine them any more. Public:
    /// the token passes openly. Cleared when the game restarts.
    /// </summary>
    public bool WasInquisitor { get; internal set; }

    /// <summary>Whether the player confirmed in the open <see cref="ResistancePhase.LeaderWindow"/> that they do not play a card.</summary>
    public bool PassedLeaderWindow { get; internal set; }
}
