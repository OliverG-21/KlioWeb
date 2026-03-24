using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Klio.Core.Interfaces;
using Klio.Core.Models;

namespace Klio.Web.Pages.Admin;

public class TaggingModel : PageModel
{
    private readonly IImageRepository _imageRepository;
    private readonly IImageTagRepository _tagRepository;

    public List<Image> Images { get; private set; } = new();
    public int TotalImages { get; private set; }
    public int Page { get; private set; } = 1;
    public const int PageSize = 60;

    public TaggingModel(IImageRepository imageRepository, IImageTagRepository tagRepository)
    {
        _imageRepository = imageRepository;
        _tagRepository = tagRepository;
    }

    public async Task OnGetAsync(int page = 1)
    {
        Page = Math.Max(1, page);
        var all = (await _imageRepository.GetAllAsync())
            .OrderByDescending(i => i.ProcessedAt)
            .ToList();
        TotalImages = all.Count;
        Images = all.Skip((Page - 1) * PageSize).Take(PageSize).ToList();
    }

    // GET ?handler=ImageTags&imageId=5
    public async Task<IActionResult> OnGetImageTagsAsync(int imageId)
    {
        var tags = await _tagRepository.GetByImageIdAsync(imageId);
        return new JsonResult(tags.Select(t => new { t.Id, t.Tag, t.Source }));
    }

    // GET ?handler=Suggestions
    public async Task<IActionResult> OnGetSuggestionsAsync()
    {
        var tags = await _tagRepository.GetAllDistinctTagsAsync();
        return new JsonResult(tags);
    }

    // GET ?handler=AllTags  – for tag management panel
    public async Task<IActionResult> OnGetAllTagsAsync()
    {
        var tagCounts = await _tagRepository.GetTagsWithCountsAsync();
        return new JsonResult(tagCounts.Select(kv => new { tag = kv.Key, count = kv.Value }));
    }

    // POST ?handler=DeleteTag  body: { tag }
    public async Task<IActionResult> OnPostDeleteTagAsync([FromBody] TagNameRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Tag)) return BadRequest();
        await _tagRepository.DeleteByTagNameAsync(req.Tag.Trim().ToLowerInvariant());
        return new JsonResult(new { ok = true });
    }

    // POST ?handler=MergeTags  body: { sourceTag, targetTag }
    public async Task<IActionResult> OnPostMergeTagsAsync([FromBody] MergeTagsRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.SourceTag) || string.IsNullOrWhiteSpace(req.TargetTag))
            return BadRequest();
        await _tagRepository.MergeTagsAsync(
            req.SourceTag.Trim().ToLowerInvariant(),
            req.TargetTag.Trim().ToLowerInvariant());
        return new JsonResult(new { ok = true });
    }

    public record TagNameRequest(string Tag);
    public record MergeTagsRequest(string SourceTag, string TargetTag);

    // POST ?handler=AddTag
    public async Task<IActionResult> OnPostAddTagAsync([FromBody] AddTagRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Tag) || req.ImageId <= 0)
            return BadRequest();

        var tag = req.Tag.Trim().ToLowerInvariant();

        // Prevent duplicates
        var existing = await _tagRepository.GetByImageIdAsync(req.ImageId);
        if (existing.Any(t => string.Equals(t.Tag, tag, StringComparison.OrdinalIgnoreCase)))
            return new JsonResult(new { error = "exists" }) { StatusCode = 409 };

        var imageTag = new ImageTag
        {
            ImageId = req.ImageId,
            Tag = tag,
            Source = TagSource.Manual,
            CreatedAt = DateTime.UtcNow
        };
        await _tagRepository.AddRangeAsync([imageTag]);
        return new JsonResult(new { imageTag.Id, imageTag.Tag, imageTag.Source });
    }

    // POST ?handler=RemoveTag&tagId=3
    public async Task<IActionResult> OnPostRemoveTagAsync(int tagId)
    {
        await _tagRepository.DeleteByIdAsync(tagId);
        return new JsonResult(new { ok = true });
    }

    public record AddTagRequest(int ImageId, string Tag);
}
