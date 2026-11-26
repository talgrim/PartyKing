using System.Buffers;
using ErrorOr;
using PartyKing.Application.Slideshow.Helpers;
using PartyKing.Domain.Entities.Slideshow;

namespace PartyKing.Application.Slideshow.Metadata;

/// <summary>
/// Takes the personal data out of an uploaded photo (GPS position, camera, time, thumbnails, XMP, comments) before it
/// is stored. Only the JPEG orientation is kept, because without it phone photos would be shown rotated.
/// </summary>
public interface IImageMetadataStripper
{
    /// <summary>
    /// A seekable stream with the image without its metadata. A validation error, worded for the guest who uploaded
    /// <paramref name="fileName"/>, when the content is not a well-formed image of the format: such a file is never
    /// stored as it is.
    /// </summary>
    Task<ErrorOr<Stream>> StripAsync(
        Stream content,
        ImageFormat format,
        string fileName,
        CancellationToken cancellationToken);
}

internal sealed class ImageMetadataStripper : IImageMetadataStripper
{
    private const int ReadChunkBytes = 81920;

    private readonly Dictionary<string, IImageFormatStripper> _strippers;

    public ImageMetadataStripper(IEnumerable<IImageFormatStripper> strippers)
    {
        _strippers = strippers.ToDictionary(x => x.Extension, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<ErrorOr<Stream>> StripAsync(
        Stream content,
        ImageFormat format,
        string fileName,
        CancellationToken cancellationToken)
    {
        var name = Path.GetFileName(fileName);

        if (!_strippers.TryGetValue(format.Extension, out var stripper))
        {
            return SlideshowErrors.NotSupported(name);
        }

        await using var data = await ReadAsync(content, cancellationToken);
        if (data is null)
        {
            return SlideshowErrors.InvalidSize(name);
        }

        var stripped = stripper.Strip(data.GetBuffer().AsSpan(0, (int)data.Length));
        if (stripped is null)
        {
            return SlideshowErrors.Damaged(name);
        }

        return stripped;
    }

    /// <summary>The whole content (at most <see cref="UploadedImageRules.MaxFileBytes"/>); null when it is longer.</summary>
    private static async Task<MemoryStream?> ReadAsync(Stream content, CancellationToken cancellationToken)
    {
        var capacity = content.CanSeek ? Math.Min(content.Length, UploadedImageRules.MaxFileBytes) : 0;
        var buffer = new MemoryStream((int)capacity);

        // Rented per call: each upload gets an array of its own, and it is only handed out again once it is returned
        var chunk = ArrayPool<byte>.Shared.Rent(ReadChunkBytes);
        try
        {
            int read;
            while ((read = await content.ReadAsync(chunk.AsMemory(0, ReadChunkBytes), cancellationToken)) > 0)
            {
                if (buffer.Length + read > UploadedImageRules.MaxFileBytes)
                {
                    await buffer.DisposeAsync();
                    return null;
                }

                buffer.Write(chunk, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }

        return buffer;
    }
}
