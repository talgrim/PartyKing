namespace PartyKing.Application.Slideshow.Metadata;

internal static class StrippedImage
{
    /// <summary>
    /// A read-only stream over the bytes written to <paramref name="written"/>, without copying them: the buffer of
    /// the writer is the buffer of the result.
    /// </summary>
    public static MemoryStream Of(MemoryStream written)
    {
        return new MemoryStream(written.GetBuffer(), 0, (int)written.Length, writable: false);
    }
}
