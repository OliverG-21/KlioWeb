using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Projekt42.Core.Interfaces;
using Projekt42.Core.Models;

namespace Projekt42.Web.Pages.Admin;

public class SettingsModel : PageModel
{
    private readonly IAppSettingsRepository _appSettingsRepository;

    public AppSettings? Settings { get; private set; }
    public string? SuccessMessage { get; private set; }
    public string? ErrorMessage { get; private set; }

    public SettingsModel(IAppSettingsRepository appSettingsRepository)
    {
        _appSettingsRepository = appSettingsRepository;
    }

    public async Task OnGetAsync(string? success = null, string? error = null)
    {
        Settings = await _appSettingsRepository.GetAsync();
        if (success != null) SuccessMessage = success;
        if (error != null) ErrorMessage = error;
    }

    public async Task<IActionResult> OnPostSaveSettingsAsync(
        string storageBasePath,
        string inboxPath,
        int thumbnailWidth,
        int thumbnailHeight)
    {
        var settings = await _appSettingsRepository.GetAsync();

        settings.StorageBasePath = storageBasePath?.Trim() ?? settings.StorageBasePath;
        settings.InboxPath = inboxPath?.Trim() ?? settings.InboxPath;
        settings.ThumbnailWidth = thumbnailWidth > 0 ? thumbnailWidth : settings.ThumbnailWidth;
        settings.ThumbnailHeight = thumbnailHeight > 0 ? thumbnailHeight : settings.ThumbnailHeight;

        await _appSettingsRepository.UpdateAsync(settings);

        return RedirectToPage(new { success = "Einstellungen wurden gespeichert." });
    }

    public async Task<IActionResult> OnPostChangePasswordAsync(
        string currentPassword,
        string newPassword,
        string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(currentPassword) ||
            string.IsNullOrWhiteSpace(newPassword) ||
            string.IsNullOrWhiteSpace(confirmPassword))
        {
            return RedirectToPage(new { error = "Alle Passwortfelder m\u00fcssen ausgef\u00fcllt werden." });
        }

        if (newPassword != confirmPassword)
        {
            return RedirectToPage(new { error = "Das neue Passwort und die Best\u00e4tigung stimmen nicht \u00fcberein." });
        }

        if (newPassword.Length < 8)
        {
            return RedirectToPage(new { error = "Das neue Passwort muss mindestens 8 Zeichen lang sein." });
        }

        var settings = await _appSettingsRepository.GetAsync();

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, settings.AdminPasswordHash))
        {
            return RedirectToPage(new { error = "Das aktuelle Passwort ist falsch." });
        }

        settings.AdminPasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _appSettingsRepository.UpdateAsync(settings);

        return RedirectToPage(new { success = "Passwort wurde erfolgreich ge\u00e4ndert." });
    }
}
