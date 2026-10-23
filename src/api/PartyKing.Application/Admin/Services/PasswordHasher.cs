using System.Security.Cryptography;

namespace PartyKing.Application.Admin.Services;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

/// <summary>PBKDF2-SHA256 with a random salt, stored as <c>pbkdf2$iterations$salt$hash</c>.</summary>
public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 600_000;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        return $"pbkdf2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string hash)
    {
        var parts = hash.Split('$');

        if (parts is not ["pbkdf2", var iterationsPart, var saltPart, var hashPart]
            || !int.TryParse(iterationsPart, out var iterations))
        {
            return false;
        }

        var salt = Convert.FromBase64String(saltPart);
        var expected = Convert.FromBase64String(hashPart);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
