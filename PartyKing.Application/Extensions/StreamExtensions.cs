namespace PartyKing.Application.Extensions;

public static class StreamExtensions
{
    public static byte[] ToByteArray(this Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
