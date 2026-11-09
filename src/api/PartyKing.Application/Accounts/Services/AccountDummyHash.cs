namespace PartyKing.Application.Accounts.Services;

/// <summary>
/// A password hash nobody knows the password of. Logging in with an unknown username verifies against it, so a wrong
/// username and a wrong password cost the same time (it has the iteration count of a user hash; an admin's hash costs
/// more, so the time of a login tells which usernames are admins, which is accepted). Singleton: the hash is
/// deliberately slow to compute, so it is computed once, on first use. <see cref="AccountService"/> is scoped and the
/// account cookie handler builds it on every request that asks who the caller is, so computing it there would cost
/// every such request a full hash.
/// </summary>
internal sealed class AccountDummyHash
{
    private readonly Lazy<string> _value;

    public AccountDummyHash(IPasswordHasher passwordHasher)
    {
        _value = new Lazy<string>(() => passwordHasher.Hash(Guid.NewGuid().ToString(), PasswordHasher.UserIterations));
    }

    public string Value => _value.Value;
}
