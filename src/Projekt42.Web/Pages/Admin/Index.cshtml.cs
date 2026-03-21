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
    public string ImageStorageSize { get; private set; } = "0 B";
    public string VideoStorageSize { get; private set; } = "0 B";
    public List<Image> RecentImages { get; private set; } = new();

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_099_511_627_776L) return $"{bytes / 1_099_511_627_776.0:F1} TB";
        if (bytes >= 1_073_741_824L)     return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576L)         return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024L)             return $"{bytes / 1_024.0:F1} KB";
        return $"{bytes} B";
    }

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
        ImageStorageSize = FormatBytes(images.Where(i => !i.IsVideo).Sum(i => i.FileSizeBytes));
        VideoStorageSize = FormatBytes(images.Where(i => i.IsVideo).Sum(i => i.FileSizeBytes));
        RecentImages = images.Take(10).ToList();

        TotalAlben = (await _topicRepository.GetAllAsync()).Count();
        TotalDuplicates = (await _duplicateLogRepository.GetAllAsync()).Count();
        QueueCount = _processingQueue.Count;
    }
}
