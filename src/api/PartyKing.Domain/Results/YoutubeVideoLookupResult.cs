using PartyKing.Domain.Models.Youtube;

namespace PartyKing.Domain.Results;

public enum YoutubeVideoStatus
{
    Found,
    NotFound,

    /// <summary>The owner of the video does not allow it to be played outside YouTube.</summary>
    NotEmbeddable,

    /// <summary>YouTube could not be reached or answered unexpectedly, so nothing is known about the video.</summary>
    Unavailable
}

public record YoutubeVideoLookupResult(YoutubeVideoStatus Status, YoutubeVideo? Video = null);
