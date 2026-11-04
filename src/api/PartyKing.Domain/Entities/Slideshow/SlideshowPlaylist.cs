namespace PartyKing.Domain.Entities.Slideshow;

/// <summary>What <see cref="SlideshowPlaylist.Advance"/> found: the next image (null when there is none) and whether it had to start over.</summary>
public sealed record SlideshowStep(SlideshowImage? Image, bool Repeated);

/// <summary>
/// The photos of one session in the order they are shown: the rule of "which image comes next". It is not a table of its
/// own: the repository builds it from the session's <see cref="SlideshowImage"/> rows (tracked), runs one
/// <see cref="Advance"/> and saves what changed, so leaving an image and choosing the next one are one save.
/// </summary>
public sealed class SlideshowPlaylist
{
    private readonly List<SlideshowImage> _images;

    private SlideshowPlaylist(Guid sessionId, List<SlideshowImage> images)
    {
        SessionId = sessionId;
        _images = images;
    }

    public Guid SessionId { get; }

    /// <summary>Builds the playlist from the session's rows. The order is the one of the ids, which is upload order (GUIDv7).</summary>
    public static SlideshowPlaylist Of(Guid sessionId, IEnumerable<SlideshowImage> images)
    {
        // Compared as text, like the database sorts the stored ids, so the order does not depend on how a Guid compares
        var own = images.OrderBy(x => x.Id.ToString(), StringComparer.Ordinal).ToList();

        if (own.Any(x => x.SessionId != sessionId))
        {
            throw new ArgumentException("The image belongs to another session.", nameof(images));
        }

        return new SlideshowPlaylist(sessionId, own);
    }

    /// <summary>
    /// Leaves the image on display (<paramref name="currentId"/>, none for the first call): marks it as presented, if it
    /// is still there, and picks the one that follows it. With <see cref="SlideshowSettings.StartFromBeginning"/> every
    /// image is a candidate, otherwise only those not presented yet. At the end of the list it starts over when
    /// <see cref="SlideshowSettings.AutoRepeat"/> is on (a first call that finds nothing does not repeat), else the step
    /// has no image. The caller saves the change of the marked image.
    /// </summary>
    public SlideshowStep Advance(Guid? currentId, SlideshowSettings settings)
    {
        if (currentId is { } leaving)
        {
            _images.FirstOrDefault(x => x.Id == leaving)?.MarkPresented();
        }

        var candidates = _images.Where(x => settings.StartFromBeginning || !x.WasPresented).ToList();

        var next = candidates.FirstOrDefault(x => IsAfter(x.Id, currentId));
        if (next is not null)
        {
            return new SlideshowStep(next, Repeated: false);
        }

        if (currentId.HasValue && settings.AutoRepeat)
        {
            return new SlideshowStep(candidates.FirstOrDefault(), Repeated: true);
        }

        return new SlideshowStep(null, Repeated: false);
    }

    private static bool IsAfter(Guid id, Guid? currentId)
    {
        if (currentId is null)
        {
            return true;
        }

        return string.CompareOrdinal(id.ToString(), currentId.Value.ToString()) > 0;
    }
}
