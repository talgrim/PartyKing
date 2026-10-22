using System.Security.Claims;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Contract.V1.Slideshow;

namespace PartyKing.API.Controllers;

[ApiController]
public abstract class CoreController : ControllerBase
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    protected CoreController(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected HttpContext GetContext()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
        {
            throw new InvalidOperationException("HttpContext is null");
        }

        return context;
    }

    protected string GetSpotifyAccessToken()
    {
        return User.Claims.First(x => x.Type == ClaimTypes.Authentication).Value;
    }

    protected IActionResult Problem(List<Error> errors)
    {
        if (errors.Count is 0)
        {
            return Problem();
        }

        if (errors.All(error => error.Type == ErrorType.Validation))
        {
            return ValidationProblem(errors);
        }

        return Problem(errors[0]);
    }

    private IActionResult Problem(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status500InternalServerError,
        };

        return Problem(statusCode: statusCode, title: error.Description);
    }

    private IActionResult ValidationProblem(List<Error> errors)
    {
        var modelStateDictionary = new ModelStateDictionary();

        foreach (var error in errors)
        {
            modelStateDictionary.AddModelError(
                error.Code,
                error.Description);
        }

        return ValidationProblem(modelStateDictionary);
    }
}