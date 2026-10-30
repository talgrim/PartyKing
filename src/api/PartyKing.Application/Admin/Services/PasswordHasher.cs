using System.Security.Cryptography;

namespace PartyKing.Application.Admin.Services;

public interface IPasswordHasher
{
    string Hash(string password, int iterations = PasswordHasher.AdminIterations);
    bool Verify(string password, string hash);

    /// <summary>True when the value has the format produced by <see cref="Hash"/>.</summary>
    bool IsHash(string value);
}

/// <summary>PBKDF2-SHA256 with a random salt, stored as <c>pbkdf2$iterations$salt$hash</c>.</summary>
public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;

    /// <summary>The default, for admin passwords, which are rare and worth the cost.</summary>
    public const int AdminIterations = 600_000;

    /// <summary>
    /// For session passwords: every guest joining a session pays one hash on a small shared CPU, and a party
    /// password guards only a slideshow and a queue. The iteration count is stored in the hash, so old hashes still verify.
    /// </summary>
    public const int SessionIterations = 100_000;

    public string Hash(string password, int iterations = AdminIterations)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashSize);

        return $"pbkdf2${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool IsHash(string value)
    {
        return TryParse(value, out _, out _, out _);
    }

    public bool Verify(string password, string hash)
    {
        if (!TryParse(hash, out var iterations, out var salt, out var expected))
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static bool TryParse(string value, out int iterations, out byte[] salt, out byte[] hash)
    {
        iterations = 0;
        salt = [];
        hash = [];

        var parts = value.Split('$');

        if (parts is not ["pbkdf2", var iterationsPart, var saltPart, var hashPart]
            || !int.TryParse(iterationsPart, out iterations)
            || iterations <= 0)
        {
            return false;
        }

        try
        {
            salt = Convert.FromBase64String(saltPart);
            hash = Convert.FromBase64String(hashPart);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length > 0 && hash.Length > 0;
    }
}
