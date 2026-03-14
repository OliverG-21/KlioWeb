using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Projekt42.Core.Interfaces;

namespace Projekt42.Web.Pages.Admin;

public class UploadModel : PageModel
{
    private readonly IAppSettingsRepository _appSettingsRepository;
    private readonly IProcessingQueue _processingQueue;

    public string? SuccessMessage { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? InboxPath { get; private set; }

    public UploadModel(IAppSettingsRepository appSettingsRepository, IProcessingQueue processingQueue)
    {
        _appSettingsRepository = appSettingsRepository;
        _processingQueue = processingQueue;
    }

    public async Task OnGetAsync()
    {
        var settings = await _appSettingsRepository.GetAsync();
        InboxPath = settings.InboxPath;
    }

    public async Task<IActionResult> OnPostAsync(IFormFileCollection files)
    {
        var settings = await _appSettingsRepository.GetAsync();
        InboxPath = settings.InboxPath;

        if (files == null || files.Count == 0)
        {
            ErrorMessage = "Bitte mindestens eine Datei ausw\u00e4hlen.";
            return Page();
        }

        var inboxDir = settings.InboxPath;

        if (!Directory.Exists(inboxDir))
        {
            try
            {
                Directory.CreateDirectory(inboxDir);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Eingangsordner konnte nicht erstellt werden: {ex.Message}";
                return Page();
            }
        }

        var savedCount = 0;
        var errors = new List<string>();

        foreach (var file in files)
        {
            if (file.Length == 0) continue;

            try
            {
                var safeFileName = Path.GetFileName(file.FileName);
                // Make filename unique to avoid collisions
                var uniqueName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{safeFileName}";
                var destPath = Path.Combine(inboxDir, uniqueName);

                await using var stream = new FileStream(destPath, FileMode.Create);
                await file.CopyToAsync(stream);

                // The FileWatcherService will pick it up automatically, but we can also enqueue
                _processingQueue.Enqueue(destPath);
                savedCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"{file.FileName}: {ex.Message}");
            }
        }

        if (errors.Any())
        {
            ErrorMessage = $"Fehler bei {errors.Count} Datei(en): {string.Join("; ", errors)}";
        }

        if (savedCount > 0)
        {
            SuccessMessage = $"{savedCount} Datei(en) erfolgreich hochgeladen und zur Verarbeitung eingereicht.";
        }

        return Page();
    }
}
