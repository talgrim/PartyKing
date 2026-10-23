using System.Security.Cryptography;

namespace PartyKing.Application.Sessions.Helpers;

public static class SessionCodeGenerator
{
    public const int CodeLength = 5;

    /// <summary>Uppercase letters and digits without the look-alikes 0/O and 1/I.</summary>
    public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Generate()
    {
        return string.Create(CodeLength, 0, static (chars, _) =>
        {
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            }
        });
    }
}
