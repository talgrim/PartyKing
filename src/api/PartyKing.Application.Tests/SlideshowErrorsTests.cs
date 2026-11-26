using ErrorOr;
using PartyKing.Application.Slideshow.Helpers;

namespace PartyKing.Application.Tests;

public class SlideshowErrorsTests
{
    public static TheoryData<Error, string> Errors => new()
    {
        {
            SlideshowErrors.UnsupportedExtension("a.html"),
            "'a.html' is not supported. Allowed formats: jpg, jpeg, png, gif, webp."
        },
        { SlideshowErrors.InvalidSize("a.jpg"), "'a.jpg' must be between 1 byte and 20 MB." },
        { SlideshowErrors.NotAnImage("a.png"), "'a.png' is not a valid image." },
        { SlideshowErrors.NotSupported("a.jpg"), "'a.jpg' is not supported." },
        { SlideshowErrors.Damaged("a.jpg"), "'a.jpg' is damaged and could not be processed." }
    };

    [Theory]
    [MemberData(nameof(Errors))]
    public void EveryRefusal_IsAValidationError_WithTheSameCodeAndItsOwnMessage(Error error, string description)
    {
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("Slideshow.InvalidUpload", error.Code);
        Assert.Equal(description, error.Description);
    }
}
