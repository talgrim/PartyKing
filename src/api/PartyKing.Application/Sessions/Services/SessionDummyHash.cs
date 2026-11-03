using PartyKing.Application.Admin.Services;

namespace PartyKing.Application.Sessions.Services;

/// <summary>
/// A password hash nobody knows the password of. Joining an unknown session verifies against it, so a wrong
/// password and an unknown session cost the same time. Singleton: the hash is deliberately slow to compute, so
/// it is computed once, on first use, not for every request that builds a scoped service.
/// </summary>
internal sealed class SessionDummyHash
{
    private readonly Lazy<string> _value;

    public SessionDummyHash(IPasswordHasher passwordHasher)
    {
        _value = new Lazy<string>(
            () => passwordHasher.Hash(Guid.NewGuid().ToString(), PasswordHasher.SessionIterations));
    }

    public string Value => _value.Value;
}
