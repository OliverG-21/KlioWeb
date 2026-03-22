using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Projekt42.Core.Interfaces;
using Projekt42.Core.Models;

namespace Projekt42.Web.Pages.Admin;

public class ImportModel : PageModel
{
    private readonly IAppSettingsRepository _appSettingsRepository;
    private readonly IFolderImportService _folderImportService;

    public string FolderInboxPath { get; private set; } = string.Empty;
    public List<FolderInboxEntry> InboxEntries { get; private set; } = [];
    public FolderImportResult? LastResult { get; private set; }
    public string? ErrorMessage { get; private set; }

    public ImportModel(IAppSettingsRepository appSettingsRepository, IFolderImportService folderImportService)
    {
        _appSettingsRepository = appSettingsRepository;
        _folderImportService = folderImportService;
    }

    public async Task OnGetAsync()
    {
        var settings = await _appSettingsRepository.GetAsync();
        FolderInboxPath = settings.FolderInboxPath;
        LoadInboxEntries();
    }

    public async Task<IActionResult> OnPostSavePathAsync(string folderInboxPath)
    {
        var settings = await _appSettingsRepository.GetAsync();
        settings.FolderInboxPath = folderInboxPath?.Trim() ?? settings.FolderInboxPath;
        await _appSettingsRepository.UpdateAsync(settings);
        FolderInboxPath = settings.FolderInboxPath;
        LoadInboxEntries();
        return Page();
    }

    public async Task<IActionResult> OnPostStartImportAsync()
    {
        var settings = await _appSettingsRepository.GetAsync();
        FolderInboxPath = settings.FolderInboxPath;

        if (!Directory.Exists(FolderInboxPath))
        {
            try { Directory.CreateDirectory(FolderInboxPath); }
            catch (Exception ex)
            {
                ErrorMessage = $"FolderInbox konnte nicht erstellt werden: {ex.Message}";
                LoadInboxEntries();
                return Page();
            }
        }

        LastResult = await _folderImportService.ImportAsync(FolderInboxPath);
        LoadInboxEntries();
        return Page();
    }

    public async Task<IActionResult> OnPostClearInboxAsync()
    {
        var settings = await _appSettingsRepository.GetAsync();
        FolderInboxPath = settings.FolderInboxPath;
        await _folderImportService.ClearFolderInboxAsync(FolderInboxPath);
        LoadInboxEntries();
        return Page();
    }

    private void LoadInboxEntries()
    {
        InboxEntries = [];
        if (!Directory.Exists(FolderInboxPath))
            return;

        foreach (var dir in Directory.GetDirectories(FolderInboxPath))
        {
            if (Path.GetFileName(dir).StartsWith(".tmp_")) continue;
            InboxEntries.Add(new FolderInboxEntry(Path.GetFileName(dir), FolderInboxEntryType.Directory,
                CountFiles(dir)));
        }

        foreach (var file in Directory.GetFiles(FolderInboxPath))
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            var type = ext == ".zip" ? FolderInboxEntryType.Zip : FolderInboxEntryType.File;
            InboxEntries.Add(new FolderInboxEntry(Path.GetFileName(file), type, null));
        }
    }

    private static int CountFiles(string dir)
    {
        try { return Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length; }
        catch { return 0; }
    }
}

public record FolderInboxEntry(string Name, FolderInboxEntryType Type, int? FileCount);

public enum FolderInboxEntryType { Directory, Zip, File }
