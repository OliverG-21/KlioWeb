using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Projekt42.Core.Interfaces;

namespace Projekt42.Worker;

public class ImageProcessorService : BackgroundService
{
    private readonly IProcessingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ImageProcessorService> _logger;

    public ImageProcessorService(
        IProcessingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ImageProcessorService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ImageProcessorService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            string filePath;
            try
            {
                filePath = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("File no longer exists, skipping: {FilePath}", filePath);
                continue;
            }

            _logger.LogInformation("Processing queued file: {FilePath}", filePath);

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var organizer = scope.ServiceProvider.GetRequiredService<IFileOrganizerService>();
                var image = await organizer.OrganizeAsync(filePath);
                _logger.LogInformation("Successfully processed image: {StoredFilename}", image.StoredFilename);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file: {FilePath}", filePath);
            }
        }

        _logger.LogInformation("ImageProcessorService stopped");
    }
}
