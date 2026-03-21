using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Projekt42.Core.Interfaces;
using Projekt42.Core.Models;

namespace Projekt42.Web.Pages.Admin;

[RequestFormLimits(MultipartBodyLengthLimit = 1_073_741_824)] // 1 GB
[RequestSizeLimit(1_073_741_824)]                              // 1 GB
public class UploadModel : PageModel
{
    private readonly IAppSettingsRepository _appSettingsRepository;
    private readonly IProcessingQueue _processingQueue;
    private readonly ITopicRepository _topicRepository;
    private readonly IAlbumFolderRepository _folderRepository;

    public string? SuccessMessage { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? InboxPath { get; private set; }

    // Albums for the "existing" dropdown: (topicId, display label)
    public List<(int Id, string Label)> Albums { get; private set; } = [];

    // Leaf folders for the "new album" folder dropdown: (folderId?, display label)
    public List<(int? Id, string Label)> LeafFolders { get; private set; } = [];

    public UploadModel(
        IAppSettingsRepository appSettingsRepository,
        IProcessingQueue processingQueue,
        ITopicRepository topicRepository,
        IAlbumFolderRepository folderRepository)
    {
        _appSettingsRepository = appSettingsRepository;
        _processingQueue = processingQueue;
        _topicRepository = topicRepository;
        _folderRepository = folderRepository;
    }

    public int? PreselectTopicId { get; private set; }

    public async Task OnGetAsync(int? topicId)
    {
        PreselectTopicId = topicId;
        var settings = await _appSettingsRepository.GetAsync();
        InboxPath = settings.InboxPath;
        await LoadDropdownsAsync();
    }

    public async Task<IActionResult> OnPostAsync(
        IFormFileCollection files,
        string? topicMode,       // "existing" | "new" | "none"
        int? topicId,
        string? newAlbumName,
        int? newAlbumFolderId)
    {
        var settings = await _appSettingsRepository.GetAsync();
        InboxPath = settings.InboxPath;
        await LoadDropdownsAsync();

        if (files == null || files.Count == 0)
        {
            ErrorMessage = "Bitte mindestens eine Datei auswählen.";
            return Page();
        }

        int? effectiveTopicId = null;
        string albumLabel = "";

        if (topicMode == "existing" && topicId.HasValue)
        {
            effectiveTopicId = topicId.Value;
            albumLabel = Albums.FirstOrDefault(a => a.Id == effectiveTopicId).Label ?? "";
        }
        else if (topicMode == "new")
        {
            if (string.IsNullOrWhiteSpace(newAlbumName))
            {
                ErrorMessage = "Bitte einen Albumnamen eingeben.";
                return Page();
            }
            var newTopic = new Topic
            {
                Name = newAlbumName.Trim(),
                Description = string.Empty,
                FolderId = newAlbumFolderId,
                CreatedAt = DateTime.UtcNow
            };
            await _topicRepository.AddAsync(newTopic);
            effectiveTopicId = newTopic.Id;
            albumLabel = newAlbumName.Trim();
        }

        if (!Directory.Exists(settings.InboxPath))
        {
            try { Directory.CreateDirectory(settings.InboxPath); }
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
                var uniqueName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{safeFileName}";
                var destPath = Path.Combine(settings.InboxPath, uniqueName);

                await using var stream = new FileStream(destPath, FileMode.Create);
                await file.CopyToAsync(stream);

                _processingQueue.Enqueue(destPath, effectiveTopicId);
                savedCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"{file.FileName}: {ex.Message}");
            }
        }

        if (errors.Any())
            ErrorMessage = $"Fehler bei {errors.Count} Datei(en): {string.Join("; ", errors)}";

        if (savedCount > 0)
        {
            var albumInfo = !string.IsNullOrEmpty(albumLabel) ? $" → {albumLabel}" : "";
            SuccessMessage = $"{savedCount} Datei(en) hochgeladen und zur Verarbeitung eingereicht.{albumInfo}";
        }

        return Page();
    }

    private async Task LoadDropdownsAsync()
    {
        var allFolders = (await _folderRepository.GetAllAsync()).ToList();
        var allTopics  = (await _topicRepository.GetAllAsync()).OrderBy(t => t.Name).ToList();

        // Albums with full path label
        foreach (var topic in allTopics)
            Albums.Add((topic.Id, BuildFolderPath(topic.FolderId, allFolders) + topic.Name));

        // Leaf folders (no sub-folders) as targets for new album
        var foldersWithSubs = allFolders
            .Where(f => f.ParentFolderId.HasValue)
            .Select(f => f.ParentFolderId!.Value)
            .ToHashSet();

        LeafFolders.Add((null, "— Root (kein Ordner) —"));
        foreach (var f in allFolders.Where(f => !foldersWithSubs.Contains(f.Id)).OrderBy(f => f.Name))
            LeafFolders.Add((f.Id, BuildFolderPath(f.ParentFolderId, allFolders) + f.Name));
    }

    private static string BuildFolderPath(int? folderId, List<AlbumFolder> allFolders)
    {
        if (folderId == null) return "";
        var parts = new List<string>();
        var current = allFolders.FirstOrDefault(f => f.Id == folderId);
        while (current != null)
        {
            parts.Insert(0, current.Name);
            current = current.ParentFolderId.HasValue
                ? allFolders.FirstOrDefault(f => f.Id == current.ParentFolderId!.Value)
                : null;
        }
        return parts.Count > 0 ? string.Join(" / ", parts) + " / " : "";
    }
}
