using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Projekt42.Core.Interfaces;
using Projekt42.Core.Models;

namespace Projekt42.Web.Pages;

public class TopicViewModel : PageModel
{
    private readonly IImageRepository _imageRepository;
    private readonly ITopicRepository _topicRepository;

    public TopicViewModel(IImageRepository imageRepository, ITopicRepository topicRepository)
    {
        _imageRepository = imageRepository;
        _topicRepository = topicRepository;
    }

    public Topic? Topic { get; private set; }
    public IReadOnlyList<Image> Images { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int topicId)
    {
        Topic = await _topicRepository.GetByIdAsync(topicId);
        if (Topic == null)
            return NotFound();

        Images = (await _imageRepository.GetByTopicIdAsync(topicId)).ToList();
        return Page();
    }
}
