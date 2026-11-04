namespace PartyKing.Application.Slideshow.Models;

/// <summary>A file a guest uploaded: its client-side name, size and a way to read the content.</summary>
public record UploadedFile(string FileName, long Length, Func<Stream> OpenRead);
