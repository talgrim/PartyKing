namespace PartyKing.API.Cookies;

/// <summary>Names of the cookies the API sets; the values are signed or plain as described on <see cref="ISignedCookies"/>.</summary>
public static class CookieNames
{
    public const string User = "cookie.partyKing.user";
    public const string Auth = "cookie.partyKing.auth";
    public const string Admin = "cookie.partyKing.admin";
    public const string SessionAccess = "cookie.partyKing.sessions";
    public const string RingoPlayer = "cookie.partyKing.ringo";
    public const string ResistancePlayer = "cookie.partyKing.resistance";
}
