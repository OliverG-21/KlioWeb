using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Projekt42.Core.Interfaces;

namespace Projekt42.Web.Pages;

public class IndexModel : PageModel
{
    private readonly IImageRepository _imageRepository;
    private readonly IImageTagRepository _tagRepository;

    public IndexModel(IImageRepository imageRepository, IImageTagRepository tagRepository)
    {
        _imageRepository = imageRepository;
        _tagRepository   = tagRepository;
    }

    public void OnGet() { }

    public async Task<IActionResult> OnGetImageAsync(int id)
    {
        var i = await _imageRepository.GetByIdAsync(id);
        if (i == null) return NotFound();
        return new JsonResult(new
        {
            id = i.Id,
            hasThumbnail = !string.IsNullOrEmpty(i.ThumbnailPath),
            date = (i.DateTaken ?? i.CreatedAt).ToString("dd.MM.yyyy"),
            filename = i.OriginalFilename,
            camera = i.CameraModel ?? "",
            lat = i.GpsLatitude,
            lon = i.GpsLongitude
        });
    }

    public async Task<IActionResult> OnGetAvailableTagsAsync()
    {
        var tags = await _tagRepository.GetAllDistinctTagsAsync();
        return new JsonResult(tags);
    }

    public async Task<IActionResult> OnGetFeedAsync(int skip = 0, int take = 60, string? tags = null)
    {
        var tagList = string.IsNullOrWhiteSpace(tags)
            ? null
            : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .Select(t => t.ToLowerInvariant())
                  .Distinct()
                  .ToList();

        var images = tagList is { Count: > 0 }
            ? await _imageRepository.GetPagedByTagsAsync(tagList, skip, take)
            : await _imageRepository.GetPagedAsync(skip, take);

        return new JsonResult(images.Select(i => new
        {
            id = i.Id,
            hasThumbnail = !string.IsNullOrEmpty(i.ThumbnailPath),
            date = (i.DateTaken ?? i.CreatedAt).ToString("dd.MM.yyyy"),
            filename = i.OriginalFilename,
            topicId = i.TopicId ?? 0,
            topic = i.Topic?.Name ?? "",
            camera = i.CameraModel ?? "",
            lat = i.GpsLatitude,
            lon = i.GpsLongitude,
            isVideo = i.IsVideo,
            mimeType = i.MimeType
        }));
    }
}
