using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Klio.Core.Interfaces;
using Klio.Core.Models;

namespace Klio.Web.Pages.Admin;

public class UnassignedPhotosModel : PageModel
{
    private readonly IImageRepository _imageRepository;
    private readonly ITopicRepository _topicRepository;
    private readonly IAlbumFolderRepository _folderRepository;

    public IReadOnlyList<Image> Images { get; private set; } = [];
    public List<(int? Id, string Label)> MoveTargets { get; private set; } = [];

    [TempData] public string? SuccessMessage { get; set; }
    [TempData] public string? ErrorMessage { get; set; }

    public UnassignedPhotosModel(
        IImageRepository imageRepository,
        ITopicRepository topicRepository,
        IAlbumFolderRepository folderRepository)
    {
        _imageRepository = imageRepository;
        _topicRepository = topicRepository;
        _folderRepository = folderRepository;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var all = await _imageRepository.GetAllAsync();
        Images = all.Where(i => i.TopicId == null).ToList();
        await LoadMoveTargetsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostMovePhotoAsync(int imageId, string targetTopicId)
    {
        var image = await _imageRepository.GetByIdAsync(imageId);
        if (image == null)
            return RedirectWithMsg(error: "Foto nicht gefunden.");

        image.TopicId = string.IsNullOrEmpty(targetTopicId) ? null : int.Parse(targetTopicId);
        await _imageRepository.UpdateAsync(image);

        return RedirectWithMsg(success: "Foto wurde verschoben.");
    }

    public async Task<IActionResult> OnPostMoveAllAsync(int[] imageIds, string targetTopicId)
    {
        if (imageIds.Length == 0)
            return RedirectWithMsg(error: "Keine Fotos ausgewählt.");

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

        return RedirectWithMsg(success: $"{moved} Foto(s) verschoben.");
    }

    private async Task LoadMoveTargetsAsync()
    {
        var allFolders = (await _folderRepository.GetAllAsync()).ToList();
        var allTopics  = (await _topicRepository.GetAllAsync()).OrderBy(t => t.Name).ToList();

        foreach (var t in allTopics)
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

    private IActionResult RedirectWithMsg(string? success = null, string? error = null)
    {
        if (success != null) SuccessMessage = success;
        if (error != null) ErrorMessage = error;
        return RedirectToPage();
    }
}
