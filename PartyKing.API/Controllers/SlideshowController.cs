using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace PartyKing.API.Controllers;

public class SlideshowController : CoreController
{
    public SlideshowController(IHttpContextAccessor httpContextAccessor) : base(httpContextAccessor)
    {
    }

    [HttpPut("UploadPhoto")]
    public async Task<IActionResult> UploadPhotoAsync([Required] IFormFile file, CancellationToken cancellationToken)
    {
        return Ok();
    }
}