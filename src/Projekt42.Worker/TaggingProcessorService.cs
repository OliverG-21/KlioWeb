using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Projekt42.Core.Interfaces;
using Projekt42.Core.Models;

namespace Projekt42.Worker;

public class TaggingProcessorService : BackgroundService
{
    private readonly ITaggingQueue   _queue;
    private readonly IServiceProvider _services;
    private readonly ILogger<TaggingProcessorService> _logger;

    public TaggingProcessorService(
        ITaggingQueue queue,
        IServiceProvider services,
        ILogger<TaggingProcessorService> logger)
    {
        _queue    = queue;
        _services = services;
        _logger   = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("TaggingProcessorService started");
        while (!ct.IsCancellationRequested)
        {
            int imageId;
            try   { imageId = await _queue.DequeueAsync(ct); }
            catch (OperationCanceledException) { break; }

            await ProcessAsync(imageId, ct);
        }
    }

    private async Task ProcessAsync(int imageId, CancellationToken ct)
    {
        using var scope      = _services.CreateScope();
        var imageRepo        = scope.ServiceProvider.GetRequiredService<IImageRepository>();
        var tagRepo          = scope.ServiceProvider.GetRequiredService<IImageTagRepository>();
        var taggingService   = scope.ServiceProvider.GetRequiredService<ITaggingService>();

        try
        {
            var image = await imageRepo.GetByIdAsync(imageId);
            if (image == null) return;

            // Skip images that already have auto-tags
            var existing = await tagRepo.GetByImageIdAsync(imageId);
            if (existing.Any(t => t.Source == TagSource.Auto)) return;

            _logger.LogInformation("Auto-tagging image {Id}: {File}", imageId, image.StoredFilename);

            var tags = await taggingService.TagImageAsync(image.StoredFilename, ct);
            if (tags.Count == 0) return;

            var imageTags = tags.Select(t => new ImageTag
            {
                ImageId    = imageId,
                Tag        = t,
                Source     = TagSource.Auto,
                Confidence = 1f,
                CreatedAt  = DateTime.UtcNow
            }).ToList();

            await tagRepo.AddRangeAsync(imageTags);
            _logger.LogInformation("Image {Id} tagged: {Tags}", imageId, string.Join(", ", tags));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tagging failed for image {Id}", imageId);
        }
    }
}
