using Microsoft.AspNetCore.Mvc.RazorPages;
using Projekt42.Core.Interfaces;
using Projekt42.Core.Models;

namespace Projekt42.Web.Pages.Admin;

public class IndexModel : PageModel
{
    private readonly IImageRepository _imageRepository;
    private readonly ITopicRepository _topicRepository;
    private readonly IDuplicateLogRepository _duplicateLogRepository;
    private readonly IProcessingQueue _processingQueue;

    public int TotalImages { get; private set; }
    public int TotalVideos { get; private set; }
    public int TotalAlben { get; private set; }
    public int TotalDuplicates { get; private set; }
    public int QueueCount { get; private set; }
    public List<Image> RecentImages { get; private set; } = new();

    public IndexModel(
        IImageRepository imageRepository,
        ITopicRepository topicRepository,
        IDuplicateLogRepository duplicateLogRepository,
        IProcessingQueue processingQueue)
    {
        _imageRepository = imageRepository;
        _topicRepository = topicRepository;
        _duplicateLogRepository = duplicateLogRepository;
        _processingQueue = processingQueue;
    }

    public async Task OnGetAsync()
    {
        var images = (await _imageRepository.GetAllAsync())
            .OrderByDescending(i => i.ProcessedAt)
            .ToList();

        TotalImages = images.Count(i => !i.IsVideo);
        TotalVideos = images.Count(i => i.IsVideo);
        RecentImages = images.Take(10).ToList();

        TotalAlben = (await _topicRepository.GetAllAsync()).Count();
        TotalDuplicates = (await _duplicateLogRepository.GetAllAsync()).Count();
        QueueCount = _processingQueue.Count;
    }
}
