namespace PartyKing.Contract.V1.Spotify;

/// <param name="Code">Authorization code from the Spotify redirect.</param>
/// <param name="State">The state value from the Spotify redirect; it identifies the session being claimed.</param>
public record AuthenticateRequestDto(string Code, string State);
