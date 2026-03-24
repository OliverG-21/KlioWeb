using Microsoft.Extensions.Logging;
using Klio.Core.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Klio.Infrastructure.Services;

public class ThumbnailService : IThumbnailService
{
    private readonly ILogger<ThumbnailService> _logger;

    public ThumbnailService(ILogger<ThumbnailService> logger)
    {
        _logger = logger;
    }

    public async Task<string?> GenerateAsync(string sourceFilePath, string thumbnailFilePath, int maxWidth, int maxHeight)
    {
        try
        {
            var extension = Path.GetExtension(sourceFilePath).ToLowerInvariant();
            if (extension == ".heic" || extension == ".heif")
            {
                _logger.LogWarning("HEIC/HEIF thumbnail generation is not supported natively. Skipping thumbnail for {FilePath}", sourceFilePath);
                return null;
            }

            var directory = Path.GetDirectoryName(thumbnailFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var image = await Image.LoadAsync(sourceFilePath);
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(maxWidth, maxHeight),
                Mode = ResizeMode.Max
            }));

            await image.SaveAsJpegAsync(thumbnailFilePath);
            return thumbnailFilePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate thumbnail for {FilePath}", sourceFilePath);
            return null;
        }
    }
}
