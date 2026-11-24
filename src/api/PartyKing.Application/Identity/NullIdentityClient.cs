using PartyKing.Application.Ports;
using PartyKing.Contract.V1.Enums;

namespace PartyKing.Application.Identity;

/// <summary>
/// Used while <c>Identity:Enabled</c> is off (the demo has no accounts): nobody is ever signed in and nothing is called.
/// The identity service's registration replaces it when it is on.
/// </summary>
internal sealed class NullIdentityClient : IIdentityClient
{
    public Task<IdentityResult> GetCallerAsync(
        string? cookieHeader,
        AccountRole? requiredRole,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IdentityResult>(IdentityResult.Anonymous.Instance);
    }
}
