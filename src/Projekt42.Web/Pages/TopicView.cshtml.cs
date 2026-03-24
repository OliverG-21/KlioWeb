using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Projekt42.Core.Interfaces;
using Projekt42.Core.Models;

namespace Projekt42.Web.Pages;

public class TopicViewModel : PageModel
{
    private readonly IImageRepository _imageRepository;
    private readonly ITopicRepository _topicRepository;
    private readonly IImageTagRepository _tagRepository;

    public TopicViewModel(IImageRepository imageRepository, ITopicRepository topicRepository, IImageTagRepository tagRepository)
    {
        _imageRepository = imageRepository;
        _topicRepository = topicRepository;
        _tagRepository   = tagRepository;
    }

    public Topic? Topic { get; private set; }
    public IReadOnlyList<Image> Images { get; private set; } = [];
    public string InitialSelJson { get; private set; } = "null";

    public async Task<IActionResult> OnGetAsync(int topicId, int? openImage)
    {
        Topic = await _topicRepository.GetByIdAsync(topicId);
        if (Topic == null)
            return NotFound();

        Images = (await _imageRepository.GetByTopicIdAsync(topicId)).ToList();

        if (openImage.HasValue)
        {
            var img = Images.FirstOrDefault(i => i.Id == openImage.Value);
            if (img != null)
            {
                InitialSelJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    id = img.Id,
                    date = (img.DateTaken ?? img.CreatedAt).ToString("dd.MM.yyyy HH:mm"),
                    camera = img.CameraModel ?? "",
                    lat = img.GpsLatitude,
                    lon = img.GpsLongitude
                });
            }
        }

        return Page();
    }

    // GET ?handler=AlbumTags&topicId=5
    public async Task<IActionResult> OnGetAlbumTagsAsync(int topicId)
    {
        var images  = (await _imageRepository.GetByTopicIdAsync(topicId)).ToList();
        var tagMap  = await _tagRepository.GetTagMapForImagesAsync(images.Select(i => i.Id));
        var allTags = tagMap.Values.SelectMany(t => t).Distinct().OrderBy(t => t).ToList();
        return new JsonResult(new { tagMap, allTags });
    }
}
