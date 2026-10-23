namespace PartyKing.Contract.V1.Sessions;

/// <param name="IsHost">The viewer is logged in as the host of this session.</param>
/// <param name="IsAdmin">The viewer is logged in as an admin.</param>
public record ViewerRoleDto(bool IsHost, bool IsAdmin);
