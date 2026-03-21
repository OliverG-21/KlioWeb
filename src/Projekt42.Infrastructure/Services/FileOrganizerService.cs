using Microsoft.Extensions.Logging;
using Projekt42.Core.Interfaces;
using Projekt42.Core.Models;

namespace Projekt42.Infrastructure.Services;

public class FileOrganizerService : IFileOrganizerService
{
    private readonly IHashService _hashService;
    private readonly IExifService _exifService;
    private readonly IThumbnailService _thumbnailService;
    private readonly IImageRepository _imageRepository;
    private readonly IDuplicateLogRepository _duplicateLogRepository;
    private readonly IAppSettingsRepository _appSettingsRepository;
    private readonly ILogger<FileOrganizerService> _logger;

    public FileOrganizerService(
        IHashService hashService,
        IExifService exifService,
        IThumbnailService thumbnailService,
        IImageRepository imageRepository,
        IDuplicateLogRepository duplicateLogRepository,
        IAppSettingsRepository appSettingsRepository,
        ILogger<FileOrganizerService> logger)
    {
        _hashService = hashService;
        _exifService = exifService;
        _thumbnailService = thumbnailService;
        _imageRepository = imageRepository;
        _duplicateLogRepository = duplicateLogRepository;
        _appSettingsRepository = appSettingsRepository;
        _logger = logger;
    }

    public async Task<Image> OrganizeAsync(string incomingFilePath, int? topicId = null)
    {
        var settings = await _appSettingsRepository.GetAsync();

        _logger.LogInformation("Processing file: {FilePath}", incomingFilePath);

        var hash = await _hashService.ComputeAsync(incomingFilePath);

        var existingImage = await _imageRepository.GetByHashAsync(hash);
        if (existingImage != null)
        {
            _logger.LogWarning("Duplicate detected for {FilePath}, hash={Hash}", incomingFilePath, hash);

            var duplicatesDir = Path.Combine(settings.StorageBasePath, "duplicates");
            Directory.CreateDirectory(duplicatesDir);
            var dupDestPath = Path.Combine(duplicatesDir, Path.GetFileName(incomingFilePath));
            if (File.Exists(dupDestPath))
            {
                dupDestPath = Path.Combine(duplicatesDir,
                    $"{Path.GetFileNameWithoutExtension(incomingFilePath)}_{Guid.NewGuid():N}{Path.GetExtension(incomingFilePath)}");
            }

            File.Move(incomingFilePath, dupDestPath, overwrite: false);

            await _duplicateLogRepository.AddAsync(new DuplicateLog
            {
                IncomingFilename = Path.GetFileName(incomingFilePath),
                DuplicatePath = dupDestPath,
                ExistingImageId = existingImage.Id,
                ExistingImagePath = existingImage.FilePath,
                DetectedAt = DateTime.UtcNow
            });

            return existingImage;
        }

        var exifData = await _exifService.ExtractAsync(incomingFilePath);

        var originalFilename = Path.GetFileName(incomingFilePath);
        var extension = Path.GetExtension(incomingFilePath).ToLowerInvariant();
        var mimeType = GetMimeType(extension);

        string storedFilename;
        string relativePath;

        if (exifData.DateTaken.HasValue)
        {
            var dt = exifData.DateTaken.Value;
            storedFilename = $"{dt:yyyy-MM-dd_HHmmss}_{hash[..8]}{extension}";
            relativePath = Path.Combine(dt.Year.ToString(), dt.Month.ToString("D2"), dt.Day.ToString("D2"), storedFilename);
        }
        else
        {
            storedFilename = $"NODATE_{Guid.NewGuid():N}{extension}";
            var now = DateTime.UtcNow;
            relativePath = Path.Combine("nodate", now.Year.ToString(), storedFilename);
        }

        var destDir = Path.Combine(settings.StorageBasePath, Path.GetDirectoryName(relativePath)!);
        Directory.CreateDirectory(destDir);

        var destPath = Path.Combine(settings.StorageBasePath, relativePath);
        if (File.Exists(destPath))
            File.Delete(incomingFilePath); // already in storage from a previous failed run
        else
            File.Move(incomingFilePath, destPath);

        var thumbnailDir = Path.Combine(settings.StorageBasePath, "thumbnails");
        Directory.CreateDirectory(thumbnailDir);
        var thumbFilename = $"{Path.GetFileNameWithoutExtension(storedFilename)}_thumb.jpg";
        var thumbnailPath = Path.Combine(thumbnailDir, thumbFilename);

        var generatedThumbPath = await _thumbnailService.GenerateAsync(
            destPath, thumbnailPath, settings.ThumbnailWidth, settings.ThumbnailHeight);

        var fileInfo = new FileInfo(destPath);

        var image = new Image
        {
            OriginalFilename = originalFilename,
            StoredFilename = storedFilename,
            FilePath = destPath,
            ThumbnailPath = generatedThumbPath ?? string.Empty,
            FileSizeBytes = fileInfo.Length,
            Hash = hash,
            MimeType = mimeType,
            DateTaken = exifData.DateTaken.HasValue
                ? DateTime.SpecifyKind(exifData.DateTaken.Value, DateTimeKind.Utc)
                : null,
            GpsLatitude = exifData.Latitude,
            GpsLongitude = exifData.Longitude,
            CameraModel = exifData.CameraModel,
            TopicId = topicId,
            ProcessedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await _imageRepository.AddAsync(image);

        _logger.LogInformation("Successfully organized image {StoredFilename} (id={Id})", storedFilename, image.Id);

        return image;
    }

    private static string GetMimeType(string extension) => extension switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".heic" or ".heif" => "image/heic",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };
}
