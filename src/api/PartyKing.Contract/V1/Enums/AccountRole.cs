using System.Text.Json.Serialization;

namespace PartyKing.Contract.V1.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AccountRole
{
    User,
    Admin
}
