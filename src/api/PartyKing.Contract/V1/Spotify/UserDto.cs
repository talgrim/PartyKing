namespace PartyKing.Contract.V1.Spotify;

public class UserDto
{
    public required string Id { get; set; }
    public required string AccessToken { get; set; }

    /// <summary>The session this login claimed.</summary>
    public required Guid SessionId { get; set; }
}
