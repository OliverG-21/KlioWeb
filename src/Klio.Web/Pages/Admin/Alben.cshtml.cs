using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Klio.Core.Interfaces;
using Klio.Core.Models;

namespace Klio.Web.Pages.Admin;

public class AlbenModel : PageModel
{
    private readonly ITopicRepository _topicRepository;
    private readonly IImageRepository _imageRepository;
    private readonly IAlbumFolderRepository _folderRepository;

    // Current folder (null = root)
    public AlbumFolder? CurrentFolder { get; private set; }

    // Breadcrumb: root → … → parent of current
    public List<AlbumFolder> Breadcrumb { get; private set; } = new();

    // Contents of current level
    public List<AlbumFolder> SubFolders { get; private set; } = new();
    public List<Topic> Albums { get; private set; } = new();

    // Image stats per album
    public Dictionary<int, int> ImageCounts { get; private set; } = new();
    public Dictionary<int, int> CoverImageIds { get; private set; } = new();

    // All leaf folders available as move targets (loaded on demand)
    public List<(int? Id, string Label)> MoveTargets { get; private set; } = new();

    // All folders for tree rendering
    public List<AlbumFolder> AllFolders { get; private set; } = new();

    [TempData] public string? SuccessMessage { get; set; }
    [TempData] public string? ErrorMessage { get; set; }

    public AlbenModel(ITopicRepository topicRepository, IImageRepository imageRepository,
        IAlbumFolderRepository folderRepository)
    {
        _topicRepository = topicRepository;
        _imageRepository = imageRepository;
        _folderRepository = folderRepository;
    }

    public async Task OnGetAsync(int? folderId)
    {
        await LoadAsync(folderId);
    }

    private async Task LoadAsync(int? folderId)
    {
        if (folderId.HasValue)
        {
            CurrentFolder = await _folderRepository.GetByIdAsync(folderId.Value);
            if (CurrentFolder != null)
                Breadcrumb = await _folderRepository.GetAncestorsAsync(folderId.Value);
        }

        AllFolders = (await _folderRepository.GetAllAsync())
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Name).ToList();

        SubFolders = (await _folderRepository.GetByParentIdAsync(folderId))
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Name).ToList();

        Albums = (await _topicRepository.GetByFolderIdAsync(folderId)).ToList();

        if (Albums.Any())
        {
            var counts   = await _imageRepository.GetCountsByTopicAsync();
            var firstIds = await _imageRepository.GetFirstImageIdPerTopicAsync();
            ImageCounts = counts;
            CoverImageIds = Albums.ToDictionary(
                a => a.Id,
                a => a.CoverImageId > 0 ? a.CoverImageId!.Value
                    : firstIds.GetValueOrDefault(a.Id));
        }

        await BuildMoveTargetsAsync(folderId);
    }

    private Task BuildMoveTargetsAsync(int? currentFolderId)
    {
        MoveTargets.Add((null, "Root"));

        foreach (var folder in AllFolders)
        {
            if (folder.Id == currentFolderId) continue;
            MoveTargets.Add((folder.Id, BuildFolderLabel(folder, AllFolders)));
        }

        MoveTargets = MoveTargets.OrderBy(t => t.Label).ToList();
        return Task.CompletedTask;
    }

    private static string BuildFolderLabel(AlbumFolder folder, List<AlbumFolder> allFolders)
    {
        var parts = new List<string> { folder.Name };
        var current = folder;
        while (current.ParentFolderId.HasValue)
        {
            current = allFolders.FirstOrDefault(f => f.Id == current.ParentFolderId.Value)!;
            if (current == null) break;
            parts.Insert(0, current.Name);
        }
        return string.Join(" / ", parts);
    }

    // ── Folder handlers ──────────────────────────────────────────────────────

    public async Task<IActionResult> OnPostCreateFolderAsync(int? folderId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return RedirectWithMsg(folderId, error: "Ordnername ist erforderlich.");

        await _folderRepository.AddAsync(new AlbumFolder
        {
            Name = name.Trim(),
            ParentFolderId = folderId,
            CreatedAt = DateTime.UtcNow
        });

        return RedirectWithMsg(folderId, success: $"Ordner \"{name.Trim()}\" wurde erstellt.");
    }

    public async Task<IActionResult> OnPostEditFolderAsync(int id, int? folderId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return RedirectWithMsg(folderId, error: "Ordnername ist erforderlich.");

        var folder = await _folderRepository.GetByIdAsync(id);
        if (folder == null)
            return RedirectWithMsg(folderId, error: "Ordner nicht gefunden.");

        folder.Name = name.Trim();
        await _folderRepository.UpdateAsync(folder);
        return RedirectWithMsg(folderId, success: $"Ordner \"{name.Trim()}\" wurde umbenannt.");
    }

    public async Task<IActionResult> OnPostDeleteFolderAsync(int id, int? folderId)
    {
        if (await _folderRepository.HasSubFoldersAsync(id) || await _folderRepository.HasAlbumsAsync(id))
            return RedirectWithMsg(folderId, error: "Ordner kann nicht gelöscht werden, da er noch Inhalt hat.");

        await _folderRepository.DeleteAsync(id);
        return RedirectWithMsg(folderId, success: "Ordner wurde gelöscht.");
    }

    // ── Album handlers ───────────────────────────────────────────────────────

    public async Task<IActionResult> OnPostCreateAlbumAsync(int? folderId, string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            return RedirectWithMsg(folderId, error: "Albumname ist erforderlich.");

        await _topicRepository.AddAsync(new Topic
        {
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            FolderId = folderId,
            CreatedAt = DateTime.UtcNow
        });

        return RedirectWithMsg(folderId, success: $"Album \"{name.Trim()}\" wurde erstellt.");
    }

    public async Task<IActionResult> OnPostEditAlbumAsync(int id, int? folderId, string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            return RedirectWithMsg(folderId, error: "Albumname ist erforderlich.");

        var topic = await _topicRepository.GetByIdAsync(id);
        if (topic == null)
            return RedirectWithMsg(folderId, error: "Album nicht gefunden.");

        topic.Name = name.Trim();
        topic.Description = description?.Trim() ?? string.Empty;
        await _topicRepository.UpdateAsync(topic);
        return RedirectWithMsg(folderId, success: $"Album \"{name.Trim()}\" wurde gespeichert.");
    }

    public async Task<IActionResult> OnPostMoveAlbumAsync(int id, int? folderId, int? targetFolderId)
    {
        var topic = await _topicRepository.GetByIdAsync(id);
        if (topic == null)
            return RedirectWithMsg(folderId, error: "Album nicht gefunden.");

        topic.FolderId = targetFolderId;
        await _topicRepository.UpdateAsync(topic);
        return RedirectWithMsg(targetFolderId, success: $"Album \"{topic.Name}\" wurde verschoben.");
    }

    public async Task<IActionResult> OnPostDeleteAlbumAsync(int id, int? folderId)
    {
        var imageCount = (await _imageRepository.GetAllAsync()).Count(i => i.TopicId == id);
        if (imageCount > 0)
            return RedirectWithMsg(folderId, error: $"Album kann nicht gelöscht werden – es enthält noch {imageCount} Foto{(imageCount != 1 ? "s" : "")}.");

        await _topicRepository.DeleteAsync(id);
        return RedirectWithMsg(folderId, success: "Album wurde gelöscht.");
    }

    // ── Sort Order ───────────────────────────────────────────────────────────

    public async Task<IActionResult> OnPostSaveFolderOrderAsync(int? folderId, int[] ids)
    {
        await _folderRepository.UpdateSortOrdersAsync(ids);
        return RedirectWithMsg(folderId, success: "Reihenfolge gespeichert.");
    }

    public async Task<IActionResult> OnPostSaveAlbumOrderAsync(int? folderId, int[] ids)
    {
        await _topicRepository.UpdateSortOrdersAsync(ids);
        return RedirectWithMsg(folderId, success: "Reihenfolge gespeichert.");
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private IActionResult RedirectWithMsg(int? folderId, string? success = null, string? error = null)
    {
        if (success != null) SuccessMessage = success;
        if (error != null) ErrorMessage = error;
        return RedirectToPage(new { folderId });
    }
}
