using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PartyKing.API.Authorization;
using PartyKing.API.Cookies;
using PartyKing.Application.Accounts.Services;
using PartyKing.Contract.V1.Accounts;

namespace PartyKing.API.Controllers;

public class AccountController : CoreController
{
    public const string LoginRateLimitPolicy = "account-login";
    public const string RegisterRateLimitPolicy = "account-register";

    private readonly IAccountService _accountService;
    private readonly ISignedCookies _cookies;

    public AccountController(
        IHttpContextAccessor httpContextAccessor,
        IAccountService accountService,
        ISignedCookies cookies)
        : base(httpContextAccessor)
    {
        _accountService = accountService;
        _cookies = cookies;
    }

    [HttpPost("account/register")]
    [EnableRateLimiting(RegisterRateLimitPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _accountService.RegisterAsync(request.Username, request.Password, cancellationToken);

        if (result.IsError)
        {
            return Problem(result.Errors);
        }

        _cookies.GrantAccount(result.Value, HttpContext);

        return NoContent();
    }

    [HttpPost("account/login")]
    [EnableRateLimiting(LoginRateLimitPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _accountService.LoginAsync(request.Username, request.Password, cancellationToken);

        if (result.IsError)
        {
            return Problem(result.Errors);
        }

        _cookies.GrantAccount(result.Value, HttpContext);

        return NoContent();
    }

    [HttpPost("account/logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Logout()
    {
        _cookies.RevokeAccount(HttpContext);

        return NoContent();
    }

    /// <summary>Ends every cookie of the account, this browser's too.</summary>
    [HttpPost("account/logout-everywhere")]
    [AuthorizeAccount]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutEverywhere(CancellationToken cancellationToken)
    {
        var result = await _accountService.SignOutEverywhereAsync(GetAccountId(), cancellationToken);

        if (result.IsError)
        {
            return Problem(result.Errors);
        }

        _cookies.RevokeAccount(HttpContext);

        return NoContent();
    }

    /// <summary>Who is signed in; the UI also uses it to check whether the cookie is still valid.</summary>
    [HttpGet("account/me")]
    [AuthorizeAccount]
    [ProducesResponseType<AccountDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var result = await _accountService.GetAsync(GetAccountId(), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Changes the caller's own password. Other browsers are signed out; this one gets a fresh cookie.</summary>
    [HttpPut("account/password")]
    [AuthorizeAccount]
    [EnableRateLimiting(LoginRateLimitPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _accountService.ChangePasswordAsync(
            GetAccountId(),
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);

        if (result.IsError)
        {
            return Problem(result.Errors);
        }

        _cookies.GrantAccount(result.Value, HttpContext);

        return NoContent();
    }

    /// <summary>Deletes the caller's own user account after the password is confirmed.</summary>
    [HttpDelete("account")]
    [AuthorizeAccount]
    [EnableRateLimiting(LoginRateLimitPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(
        [FromBody] DeleteAccountRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _accountService.DeleteAsync(GetAccountId(), request.Password, cancellationToken);

        if (result.IsError)
        {
            return Problem(result.Errors);
        }

        _cookies.RevokeAccount(HttpContext);

        return NoContent();
    }

    private Guid GetAccountId()
    {
        return Guid.Parse(User.Claims.First(x => x.Type == ClaimTypes.NameIdentifier).Value);
    }
}
