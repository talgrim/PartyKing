namespace PartyKing.Application.Slideshow.Metadata;

/// <summary>
/// Removes the metadata of one image format without decoding the pixels: the container is walked and only what is
/// needed to render the image is copied. Nothing is trusted in the file: every length is checked against the data.
/// </summary>
internal interface IImageFormatStripper
{
    /// <summary>The extension of the format, as <c>ImageFormat.Extension</c> names it (".jpg").</summary>
    string Extension { get; }

    /// <summary>
    /// The image without its metadata, as a stream over the one buffer it was written to (no further copy); null when
    /// the data is not a well-formed image of this format.
    /// </summary>
    MemoryStream? Strip(ReadOnlySpan<byte> data);
}
