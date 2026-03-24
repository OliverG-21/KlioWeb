using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Klio.Core.Interfaces;
using Klio.Core.Models;

namespace Klio.Web.Pages;

public class FolderViewModel : PageModel
{
    private readonly IAlbumFolderRepository _folderRepository;
    private readonly ITopicRepository _topicRepository;
    private readonly IImageRepository _imageRepository;

    public AlbumFolder? Folder { get; private set; }
    public List<AlbumFolder> Breadcrumb { get; private set; } = [];
    public List<AlbumFolder> SubFolders { get; private set; } = [];
    public List<Topic> Albums { get; private set; } = [];
    public Dictionary<int, int> ImageCounts { get; private set; } = new();
    public Dictionary<int, int> CoverImageIds { get; private set; } = new();

    public FolderViewModel(
        IAlbumFolderRepository folderRepository,
        ITopicRepository topicRepository,
        IImageRepository imageRepository)
    {
        _folderRepository = folderRepository;
        _topicRepository = topicRepository;
        _imageRepository = imageRepository;
    }

    public async Task<IActionResult> OnGetAsync(int folderId)
    {
        Folder = await _folderRepository.GetByIdAsync(folderId);
        if (Folder == null) return NotFound();

        Breadcrumb = await _folderRepository.GetAncestorsAsync(folderId);
        SubFolders = (await _folderRepository.GetByParentIdAsync(folderId))
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Name).ToList();

        Albums = (await _topicRepository.GetByFolderIdAsync(folderId)).ToList();

        if (Albums.Any())
        {
            var countsTask   = _imageRepository.GetCountsByTopicAsync();
            var firstIdsTask = _imageRepository.GetFirstImageIdPerTopicAsync();
            await Task.WhenAll(countsTask, firstIdsTask);
            var counts   = countsTask.Result;
            var firstIds = firstIdsTask.Result;
            ImageCounts = counts;
            CoverImageIds = Albums.ToDictionary(
                a => a.Id,
                a => a.CoverImageId > 0 ? a.CoverImageId!.Value
                    : firstIds.GetValueOrDefault(a.Id));
        }

        return Page();
    }
}
