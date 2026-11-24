using PartyKing.Application.System;

namespace PartyKing.Application.Tests;

/// <summary>The real hasher takes 100 000+ PBKDF2 iterations a hash, which a unit test of the flow does not need.</summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    private const string Prefix = "fake$";

    public int Hashes { get; private set; }
    public List<int> Iterations { get; } = [];

    public static string HashOf(string password) => Prefix + password;

    public string Hash(string password, int iterations = PasswordHasher.SessionIterations)
    {
        Hashes++;
        Iterations.Add(iterations);

        return HashOf(password);
    }

    public bool Verify(string password, string hash) => hash == HashOf(password);

    public bool IsHash(string value) => value.StartsWith(Prefix, StringComparison.Ordinal);
}
