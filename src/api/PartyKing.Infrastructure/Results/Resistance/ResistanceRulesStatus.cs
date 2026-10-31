namespace PartyKing.Infrastructure.Results;

public enum ResistanceRulesStatus
{
    NoGame,

    /// <summary>The game is already open to guests or started.</summary>
    NotEditable,
    Refreshed,
}
