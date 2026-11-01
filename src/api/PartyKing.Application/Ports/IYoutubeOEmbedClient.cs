using PartyKing.Domain.Results;

namespace PartyKing.Application.Ports;

public interface IYoutubeOEmbedClient
{
    /// <summary>Looks the video up through YouTube's keyless oEmbed endpoint.</summary>
    Task<YoutubeVideoLookupResult> GetVideoAsync(string videoId, CancellationToken cancellationToken);
}
