using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Projekt42.Core.Interfaces;
using Projekt42.Core.Models;
using Projekt42.Infrastructure.Data;

namespace Projekt42.Web.Pages.Admin;

public class SettingsModel : PageModel
{
    private readonly IAppSettingsRepository _appSettingsRepository;
    private readonly IImageRepository _imageRepository;
    private readonly IExifService _exifService;
    private readonly AppDbContext _db;

    public AppSettings? Settings { get; private set; }
    public string? SuccessMessage { get; private set; }
    public string? ErrorMessage { get; private set; }

    public SettingsModel(IAppSettingsRepository appSettingsRepository, IImageRepository imageRepository, IExifService exifService, AppDbContext db)
    {
        _appSettingsRepository = appSettingsRepository;
        _imageRepository = imageRepository;
        _exifService = exifService;
        _db = db;
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
        string folderInboxPath,
        int thumbnailWidth,
        int thumbnailHeight)
    {
        var settings = await _appSettingsRepository.GetAsync();

        settings.StorageBasePath = storageBasePath?.Trim() ?? settings.StorageBasePath;
        settings.InboxPath = inboxPath?.Trim() ?? settings.InboxPath;
        settings.FolderInboxPath = folderInboxPath?.Trim() ?? settings.FolderInboxPath;
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

    public async Task<IActionResult> OnPostReprocessExifAsync()
    {
        var images = await _imageRepository.GetAllAsync();
        int updated = 0;

        foreach (var image in images)
        {
            if (!System.IO.File.Exists(image.FilePath)) continue;
            try
            {
                var exif = await _exifService.ExtractAsync(image.FilePath);
                bool changed = false;

                if (image.CameraModel != exif.CameraModel) { image.CameraModel = exif.CameraModel; changed = true; }
                if (image.GpsLatitude != exif.Latitude)    { image.GpsLatitude  = exif.Latitude;   changed = true; }
                if (image.GpsLongitude != exif.Longitude)  { image.GpsLongitude = exif.Longitude;  changed = true; }
                if (image.DateTaken == null && exif.DateTaken.HasValue) { image.DateTaken = exif.DateTaken; changed = true; }

                if (changed) { await _imageRepository.UpdateAsync(image); updated++; }
            }
            catch { /* skip unreadable files */ }
        }

        return RedirectToPage(new { success = $"EXIF neu gelesen: {updated} Bilder aktualisiert." });
    }

    public async Task<IActionResult> OnPostClearAllAsync()
    {
        var settings = await _appSettingsRepository.GetAsync();

        // Delete all files from storage (except inbox)
        var basePath = settings.StorageBasePath;
        if (Directory.Exists(basePath))
        {
            foreach (var dir in Directory.GetDirectories(basePath))
            {
                var name = Path.GetFileName(dir);
                if (name != "inbox")
                    Directory.Delete(dir, recursive: true);
            }
        }

        // Truncate DB tables in correct order (FK constraints)
        await _db.Database.ExecuteSqlRawAsync(@"
            TRUNCATE TABLE ""Images"", ""DuplicateLogs"", ""Topics"", ""AlbumFolders""
            RESTART IDENTITY CASCADE;");

        return RedirectToPage(new { success = "Alle Bilder, Alben und Ordner wurden gelöscht." });
    }
}
