using System.Text.Json;
using Identity.Client.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PartyKing.Application.Playlists.Services;

namespace PartyKing.API.Controllers;

/// <summary>
/// The integration events of the identity service, delivered as signed webhooks. It is called by the service, not by a
/// browser: public but signed (see <see cref="IIdentityWebhookVerifier"/>), and left out of Swagger. Behind nginx the
/// path is <c>/api/internal/events</c>; the signature covers the body, so the prefix does not matter.
/// </summary>
[ApiExplorerSettings(IgnoreApi = true)]
public class InternalEventsController : CoreController
{
    public const string RateLimitPolicy = "identity-webhook";
    public const string AccountDeletedType = "account.deleted";
    public const int MaxBodyBytes = 16 * 1024;

    private readonly IIdentityWebhookVerifier _verifier;
    private readonly ISavedPlaylistService _savedPlaylistService;

    public InternalEventsController(
        IHttpContextAccessor httpContextAccessor,
        IIdentityWebhookVerifier verifier,
        ISavedPlaylistService savedPlaylistService)
        : base(httpContextAccessor)
    {
        _verifier = verifier;
        _savedPlaylistService = savedPlaylistService;
    }

    /// <summary>
    /// 401 for a call that is not signed or is too old, 400 for a signed body that is not an event, 204 once the event
    /// is handled (also when there was nothing to do, and for a type this app does not know: any other status makes the
    /// identity service try again). <c>account.deleted</c> deletes that owner's saved playlists, which is idempotent, so
    /// a repeated event is harmless.
    /// </summary>
    [HttpPost("internal/events")]
    [EnableRateLimiting(RateLimitPolicy)]
    [RequestSizeLimit(MaxBodyBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync(cancellationToken);

        if (body is null || !_verifier.IsSigned(Request, body))
        {
            return Unauthorized();
        }

        if (!TryReadEvent(body, out var type, out var accountId))
        {
            return BadRequest();
        }

        if (type != AccountDeletedType)
        {
            return NoContent();
        }

        var result = await _savedPlaylistService.DeleteByOwnerAsync(accountId, cancellationToken);

        return result.Match(_ => NoContent(), Problem);
    }

    // The signature covers the exact bytes, so they are read before anything is parsed
    private async Task<byte[]?> ReadBodyAsync(CancellationToken cancellationToken)
    {
        if (Request.ContentLength > MaxBodyBytes)
        {
            return null;
        }

        using var buffer = new MemoryStream();

        await Request.Body.CopyToAsync(buffer, cancellationToken);

        return buffer.Length > MaxBodyBytes ? null : buffer.ToArray();
    }

    private static bool TryReadEvent(byte[] body, out string type, out Guid accountId)
    {
        type = string.Empty;
        accountId = Guid.Empty;

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("type", out var typeElement)
                || typeElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            type = typeElement.GetString()!;

            // Only the types this app handles need their data to be well formed
            if (type != AccountDeletedType)
            {
                return true;
            }

            return root.TryGetProperty("data", out var data)
                   && data.ValueKind == JsonValueKind.Object
                   && data.TryGetProperty("accountId", out var id)
                   && id.TryGetGuid(out accountId)
                   && accountId != Guid.Empty;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
