using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Projekt42.Core.Interfaces;
using Projekt42.Core.Models;

namespace Projekt42.Web.Pages.Admin;

public class AlbenModel : PageModel
{
    private readonly ITopicRepository _topicRepository;
    private readonly IImageRepository _imageRepository;

    public List<Topic> Topics { get; private set; } = new();
    public Dictionary<int, int> ImageCounts { get; private set; } = new();
    public Dictionary<int, int> CoverImageIds { get; private set; } = new();
    public string? SuccessMessage { get; private set; }
    public string? ErrorMessage { get; private set; }

    public AlbenModel(ITopicRepository topicRepository, IImageRepository imageRepository)
    {
        _topicRepository = topicRepository;
        _imageRepository = imageRepository;
    }

    public async Task OnGetAsync(string? success = null, string? error = null)
    {
        if (success != null) SuccessMessage = success;
        if (error != null) ErrorMessage = error;
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        Topics = (await _topicRepository.GetAllAsync()).ToList();
        var allImages = (await _imageRepository.GetAllAsync()).ToList();

        ImageCounts = allImages
            .Where(i => i.TopicId.HasValue)
            .GroupBy(i => i.TopicId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        CoverImageIds = allImages
            .Where(i => i.TopicId.HasValue)
            .GroupBy(i => i.TopicId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(i => i.DateTaken ?? i.CreatedAt).First().Id);
    }

    public async Task<IActionResult> OnPostCreateAsync(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            await LoadDataAsync();
            ErrorMessage = "Name ist erforderlich.";
            return Page();
        }

        await _topicRepository.AddAsync(new Topic
        {
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        });

        return RedirectToPage(new { success = "Album wurde erfolgreich erstellt." });
    }

    public async Task<IActionResult> OnPostEditAsync(int id, string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            await LoadDataAsync();
            ErrorMessage = "Name ist erforderlich.";
            return Page();
        }

        var topic = await _topicRepository.GetByIdAsync(id);
        if (topic == null)
        {
            return RedirectToPage(new { error = "Album nicht gefunden." });
        }

        topic.Name = name.Trim();
        topic.Description = description?.Trim() ?? string.Empty;
        await _topicRepository.UpdateAsync(topic);

        return RedirectToPage(new { success = "Album wurde erfolgreich gespeichert." });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var allImages = await _imageRepository.GetAllAsync();
        var count = allImages.Count(i => i.TopicId == id);

        if (count > 0)
        {
            return RedirectToPage(new { error = "Album kann nicht gel\u00f6scht werden, da es noch Fotos enth\u00e4lt." });
        }

        await _topicRepository.DeleteAsync(id);
        return RedirectToPage(new { success = "Album wurde gel\u00f6scht." });
    }
}
