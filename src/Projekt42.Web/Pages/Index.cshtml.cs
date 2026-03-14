using Microsoft.AspNetCore.Mvc.RazorPages;
using Projekt42.Core.Interfaces;
using Projekt42.Core.Models;

namespace Projekt42.Web.Pages;

public class IndexModel : PageModel
{
    private readonly IImageRepository _imageRepository;
    private readonly ITopicRepository _topicRepository;

    public IndexModel(IImageRepository imageRepository, ITopicRepository topicRepository)
    {
        _imageRepository = imageRepository;
        _topicRepository = topicRepository;
    }

    public IReadOnlyList<Image> Images { get; private set; } = [];
    public IReadOnlyList<Topic> Topics { get; private set; } = [];
    public int SelectedTopicId { get; private set; }

    public async Task OnGetAsync(int? topicId)
    {
        Topics = (await _topicRepository.GetAllAsync()).ToList();
        SelectedTopicId = topicId ?? 0;

        if (SelectedTopicId == 0)
            Images = (await _imageRepository.GetAllAsync()).ToList();
        else
            Images = (await _imageRepository.GetByTopicIdAsync(SelectedTopicId)).ToList();
    }
}
