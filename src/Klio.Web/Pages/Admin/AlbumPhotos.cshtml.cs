using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Klio.Core.Interfaces;
using Klio.Core.Models;

namespace Klio.Web.Pages.Admin;

public class AlbumPhotosModel : PageModel
{
    private readonly IImageRepository _imageRepository;
    private readonly ITopicRepository _topicRepository;
    private readonly IAlbumFolderRepository _folderRepository;
    private readonly IImageTagRepository _tagRepository;

    public Topic? Album { get; private set; }
    public IReadOnlyList<Image> Images { get; private set; } = [];
    public int? CoverImageId { get; private set; }
    public IReadOnlyList<string> AllTags { get; private set; } = [];

    // All albums as move targets: (topicId?, label)
    public List<(int? Id, string Label)> MoveTargets { get; private set; } = [];

    [TempData] public string? SuccessMessage { get; set; }
    [TempData] public string? ErrorMessage { get; set; }

    public AlbumPhotosModel(
        IImageRepository imageRepository,
        ITopicRepository topicRepository,
        IAlbumFolderRepository folderRepository,
        IImageTagRepository tagRepository)
    {
        _imageRepository = imageRepository;
        _topicRepository = topicRepository;
        _folderRepository = folderRepository;
        _tagRepository = tagRepository;
    }

    public async Task<IActionResult> OnGetAsync(int topicId)
    {
        Album = await _topicRepository.GetByIdAsync(topicId);
        if (Album == null) return NotFound();

        Images = (await _imageRepository.GetByTopicIdAsync(topicId)).ToList();
        CoverImageId = Album.CoverImageId;
        AllTags = await _tagRepository.GetAllDistinctTagsAsync();
        await LoadMoveTargetsAsync(topicId);
        return Page();
    }

    public async Task<IActionResult> OnPostAddTagToAlbumAsync(int topicId, string tag)
    {
        tag = tag.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(tag))
            return RedirectWithMsg(topicId, error: "Tag darf nicht leer sein.");

        var count = await _tagRepository.AddTagToTopicAsync(topicId, tag);
        return count == 0
            ? RedirectWithMsg(topicId, error: $"Tag \"{tag}\" war bereits bei allen Fotos vorhanden.")
            : RedirectWithMsg(topicId, success: $"Tag \"{tag}\" wurde {count} Foto(s) hinzugefügt.");
    }

    public async Task<IActionResult> OnPostSetCoverAsync(int topicId, int imageId)
    {
        var album = await _topicRepository.GetByIdAsync(topicId);
        if (album == null) return RedirectWithMsg(topicId, error: "Album nicht gefunden.");

        album.CoverImageId = imageId;
        await _topicRepository.UpdateAsync(album);

        return RedirectWithMsg(topicId, success: "Titelbild gesetzt.");
    }

    public async Task<IActionResult> OnPostMovePhotoAsync(int topicId, int imageId, string targetTopicId)
    {
        var image = await _imageRepository.GetByIdAsync(imageId);
        if (image == null)
            return RedirectWithMsg(topicId, error: "Foto nicht gefunden.");

        image.TopicId = string.IsNullOrEmpty(targetTopicId) ? null : int.Parse(targetTopicId);
        await _imageRepository.UpdateAsync(image);

        return RedirectWithMsg(topicId, success: "Foto wurde verschoben.");
    }

    public async Task<IActionResult> OnPostMoveAllAsync(int topicId, int[] imageIds, string targetTopicId)
    {
        if (imageIds.Length == 0)
            return RedirectWithMsg(topicId, error: "Keine Fotos ausgewählt.");

        int? newTopicId = string.IsNullOrEmpty(targetTopicId) ? null : int.Parse(targetTopicId);
        var moved = 0;

        foreach (var id in imageIds)
        {
            var image = await _imageRepository.GetByIdAsync(id);
            if (image == null) continue;
            image.TopicId = newTopicId;
            await _imageRepository.UpdateAsync(image);
            moved++;
        }

        return RedirectWithMsg(topicId, success: $"{moved} Foto(s) verschoben.");
    }

    private async Task LoadMoveTargetsAsync(int currentTopicId)
    {
        var allFolders = (await _folderRepository.GetAllAsync()).ToList();
        var allTopics  = (await _topicRepository.GetAllAsync()).OrderBy(t => t.Name).ToList();

        MoveTargets.Add((null, "— Kein Album (aus Album entfernen) —"));
        foreach (var t in allTopics.Where(t => t.Id != currentTopicId))
            MoveTargets.Add((t.Id, BuildPath(t.FolderId, allFolders) + t.Name));
    }

    private static string BuildPath(int? folderId, List<AlbumFolder> allFolders)
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

    private IActionResult RedirectWithMsg(int topicId, string? success = null, string? error = null)
    {
        if (success != null) SuccessMessage = success;
        if (error != null) ErrorMessage = error;
        return RedirectToPage(new { topicId });
    }
}
