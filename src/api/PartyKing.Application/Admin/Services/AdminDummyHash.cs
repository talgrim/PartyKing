namespace PartyKing.Application.Admin.Services;

/// <summary>
/// A password hash nobody knows the password of. Logging in with an unknown username verifies against it, so a wrong
/// username and a wrong password cost the same time (it has the iteration count of a real admin hash). Singleton: the
/// hash is deliberately slow to compute, so it is computed once, on first use. <see cref="AdminService"/> is scoped
/// and the admin cookie handler builds it on every request that asks who the caller is, so computing it there cost
/// every game fetch a full hash (about 55 ms of CPU).
/// </summary>
internal sealed class AdminDummyHash
{
    private readonly Lazy<string> _value;

    public AdminDummyHash(IPasswordHasher passwordHasher)
    {
        _value = new Lazy<string>(() => passwordHasher.Hash(Guid.NewGuid().ToString()));
    }

    public string Value => _value.Value;
}
