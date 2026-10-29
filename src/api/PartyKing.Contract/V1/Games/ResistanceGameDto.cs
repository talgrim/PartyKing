using System.Text.Json.Serialization;

namespace PartyKing.Contract.V1.Games;

/// <summary>
/// The session's Resistance game. <see cref="CreatedAt"/> tells one game from the next, <see cref="IsOpen"/> is whether
/// guests may join, <see cref="Players"/> the players in seating order and <see cref="YourPlayerId"/> the caller's own
/// player id (null when the caller only watches). <see cref="MaxPlayers"/> is how many may join.
/// Once <see cref="StartedAt"/> is set the players are locked and <see cref="LeaderPlayerId"/> leads the mission.
/// The roles are secret: <see cref="YourRole"/> is set only for a player of a started game, and
/// <see cref="FellowSpies"/> (the other spies' player ids) only for a spy. Observers get neither.
/// The leader picks <see cref="RequiredTeamSize"/> players for mission <see cref="MissionNumber"/> (null before the
/// start); <see cref="TeamPlayerIds"/> is the submitted team, empty until then, and public.
/// </summary>
public record ResistanceGameDto(
    DateTimeOffset CreatedAt,
    bool IsOpen,
    IReadOnlyList<ResistancePlayerDto> Players,
    Guid? YourPlayerId,
    int MaxPlayers,
    DateTimeOffset? StartedAt,
    Guid? LeaderPlayerId,
    ResistanceRoleDto? YourRole,
    IReadOnlyList<Guid>? FellowSpies,
    int MissionNumber,
    int? RequiredTeamSize,
    IReadOnlyList<Guid> TeamPlayerIds);

/// <summary>Serialized by name ("Resistance", "Spy"), which the UI compares against.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResistanceRoleDto
{
    Resistance,
    Spy,
}

/// <summary>A player of the game; <see cref="Name"/> carries the "(host)" suffix for the session host.</summary>
public record ResistancePlayerDto(Guid Id, string Name);

/// <summary>
/// What any guest may know of the game: whether it is open to join, whether it started (nobody can join then) and
/// whether the caller already joined.
/// </summary>
public record ResistanceLobbyDto(DateTimeOffset CreatedAt, bool IsOpen, bool IsStarted, bool Joined);

public record ResistanceJoinRequest(string? Name);

/// <summary>The ids of all the players in the new seating order.</summary>
public record ResistancePlayersOrderRequest(IReadOnlyList<Guid>? PlayerIds);

/// <summary>The ids of the players the leader sends on the mission.</summary>
public record ResistanceTeamRequest(IReadOnlyList<Guid>? PlayerIds);
