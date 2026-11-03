using System.Security.Cryptography;
using System.Text;

namespace PartyKing.Application.Sessions.Helpers;

internal static class SessionAccessStamp
{
    /// <summary>Changes whenever the password does, which invalidates the unlocks issued for the old one.</summary>
    public static string For(string passwordHash)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash)))[..16];
    }
}
