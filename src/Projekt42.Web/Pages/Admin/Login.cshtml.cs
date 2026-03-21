using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Projekt42.Core.Interfaces;
using System.Security.Claims;

namespace Projekt42.Web.Pages.Admin;

public class LoginModel : PageModel
{
    private readonly IAppSettingsRepository _settingsRepository;

    public string ErrorMessage { get; private set; } = string.Empty;

    public LoginModel(IAppSettingsRepository settingsRepository)
    {
        _settingsRepository = settingsRepository;
    }

    public string? ReturnUrl { get; private set; }

    public void OnGet(string? returnUrl)
    {
        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string password, string? returnUrl)
    {
        ReturnUrl = returnUrl;

        if (string.IsNullOrWhiteSpace(password))
        {
            ErrorMessage = "Password is required.";
            return Page();
        }

        var settings = await _settingsRepository.GetAsync();

        if (!BCrypt.Net.BCrypt.Verify(password, settings.AdminPasswordHash))
        {
            ErrorMessage = "Invalid password.";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "admin"),
            new(ClaimTypes.Role, "Admin")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return Redirect("/admin");
    }
}
