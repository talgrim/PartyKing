using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.Domain.Events.Games.Base;

/// <summary>The game of the session was ended and deleted, with its players.</summary>
public sealed record LobbyGameEnded(Guid SessionId, GameKind Kind) : IDomainEvent;

/// <summary>An admin opened the game to guests.</summary>
public sealed record LobbyOpened(Guid SessionId, GameKind Kind) : IDomainEvent;

/// <summary>A player joined the lobby.</summary>
public sealed record LobbyPlayerJoined(Guid SessionId, GameKind Kind, Guid PlayerId) : IDomainEvent;

/// <summary>A player was removed from the lobby.</summary>
public sealed record LobbyPlayerKicked(Guid SessionId, GameKind Kind, Guid PlayerId) : IDomainEvent;

/// <summary>The player order was set.</summary>
public sealed record LobbyOrderChanged(Guid SessionId, GameKind Kind) : IDomainEvent;
