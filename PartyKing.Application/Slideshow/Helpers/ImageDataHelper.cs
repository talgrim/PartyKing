using Microsoft.AspNetCore.Http;
using PartyKing.Application.Extensions;

namespace PartyKing.Application.Slideshow.Helpers;

public class ImageDataHelper
{
    public static async Task<ImageDataResult> PrepareImageDataAsync(IFormFile photo)
    {
        await using var stream = photo.OpenReadStream();
        var imageData = stream.ToByteArray();

        return new ImageDataResult
        {
            ImageData = imageData,
            FileName = photo.FileName
        };
    }
}

public class ImageDataResult
{
    public required byte[] ImageData { get; set; }
    public required string FileName { get; set; }
}