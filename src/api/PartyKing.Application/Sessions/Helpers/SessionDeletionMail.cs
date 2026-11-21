using System.Globalization;
using PartyKing.Application.Ports;
using PartyKing.Domain.Entities.Sessions;

namespace PartyKing.Application.Sessions.Helpers;

/// <summary>
/// The mail that tells the operator a guest asked for a session to be deleted. English only, since the reader is the
/// operator. It names the session but carries no password, token or other secret.
/// </summary>
internal static class SessionDeletionMail
{
    public static MailMessage Compose(string recipient, Session session, DateTime deletionScheduledAt)
    {
        var when = deletionScheduledAt.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

        // The name is typed by a user: a line break in it must not turn into a header or a second line of the subject
        var subject = $"PartyKing: deletion requested for session \"{OneLine(session.Name)}\" ({session.Code})";

        var body = $"""
            A guest asked for this PartyKing session to be deleted.

            Name: {session.Name}
            Code: {session.Code}
            Id: {session.Id}
            Deleted automatically on: {when}

            The session and its photos are removed at that time. An admin can cancel the deletion in the admin panel until then.
            """;

        return new MailMessage(recipient, subject, body);
    }

    private static string OneLine(string value)
    {
        return string.Concat(value.Select(character => char.IsControl(character) ? ' ' : character));
    }
}
