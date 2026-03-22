using Microsoft.Extensions.Logging;
using Projekt42.Core.Interfaces;
using Projekt42.Core.Models;

namespace Projekt42.Infrastructure.Services;

public class FileOrganizerService : IFileOrganizerService
{
    private readonly IHashService _hashService;
    private readonly IExifService _exifService;
    private readonly IThumbnailService _thumbnailService;
    private readonly IVideoThumbnailService _videoThumbnailService;
    private readonly IImageRepository _imageRepository;
    private readonly IDuplicateLogRepository _duplicateLogRepository;
    private readonly IAppSettingsRepository _appSettingsRepository;
    private readonly ILogger<FileOrganizerService> _logger;

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".webm", ".mov", ".m4v", ".avi", ".mkv", ".ogv"
    };

    private static readonly HashSet<string> NativeBrowserVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".webm"
    };

    public FileOrganizerService(
        IHashService hashService,
        IExifService exifService,
        IThumbnailService thumbnailService,
        IVideoThumbnailService videoThumbnailService,
        IImageRepository imageRepository,
        IDuplicateLogRepository duplicateLogRepository,
        IAppSettingsRepository appSettingsRepository,
        ILogger<FileOrganizerService> logger)
    {
        _hashService = hashService;
        _exifService = exifService;
        _thumbnailService = thumbnailService;
        _videoThumbnailService = videoThumbnailService;
        _imageRepository = imageRepository;
        _duplicateLogRepository = duplicateLogRepository;
        _appSettingsRepository = appSettingsRepository;
        _logger = logger;
    }

    public async Task<Image> OrganizeAsync(string incomingFilePath, int? topicId = null)
    {
        var settings = await _appSettingsRepository.GetAsync();
        _logger.LogInformation("Processing file: {FilePath}", incomingFilePath);

        var extension = Path.GetExtension(incomingFilePath).ToLowerInvariant();
        if (VideoExtensions.Contains(extension))
            return await OrganizeVideoAsync(incomingFilePath, topicId, settings);

        return await OrganizeImageAsync(incomingFilePath, topicId, settings);
    }

    // ── Image ────────────────────────────────────────────────────────────────

    private async Task<Image> OrganizeImageAsync(string incomingFilePath, int? topicId, AppSettings settings)
    {
        var hash = await _hashService.ComputeAsync(incomingFilePath);

        var existingImage = await _imageRepository.GetByHashAsync(hash);
        if (existingImage != null)
        {
            _logger.LogWarning("Duplicate detected for {FilePath}, hash={Hash}", incomingFilePath, hash);
            await MoveToDuplicatesAsync(incomingFilePath, existingImage, settings);
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
            File.Delete(incomingFilePath);
        else
            File.Move(incomingFilePath, destPath);

        // Thumbnail: for HEIC convert via ffmpeg first, then use ImageSharp
        var thumbnailDir = Path.Combine(settings.StorageBasePath, "thumbnails");
        Directory.CreateDirectory(thumbnailDir);
        var thumbFilename = $"{Path.GetFileNameWithoutExtension(storedFilename)}_thumb.jpg";
        var thumbnailPath = Path.Combine(thumbnailDir, thumbFilename);

        string? generatedThumbPath;
        if (extension is ".heic" or ".heif")
            generatedThumbPath = await GenerateHeicThumbnailAsync(destPath, thumbnailPath, thumbnailDir, settings);
        else
            generatedThumbPath = await _thumbnailService.GenerateAsync(destPath, thumbnailPath, settings.ThumbnailWidth, settings.ThumbnailHeight);

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
            IsVideo = false,
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

    // ── Video ────────────────────────────────────────────────────────────────

    private async Task<Image> OrganizeVideoAsync(string incomingFilePath, int? topicId, AppSettings settings)
    {
        var originalExtension = Path.GetExtension(incomingFilePath).ToLowerInvariant();

        // Hash original file before potential transcoding
        var hash = await _hashService.ComputeAsync(incomingFilePath);

        var existingImage = await _imageRepository.GetByHashAsync(hash);
        if (existingImage != null)
        {
            _logger.LogWarning("Duplicate video detected for {FilePath}, hash={Hash}", incomingFilePath, hash);
            await MoveToDuplicatesAsync(incomingFilePath, existingImage, settings);
            return existingImage;
        }

        var exifData = await _exifService.ExtractAsync(incomingFilePath);
        var originalFilename = Path.GetFileName(incomingFilePath);
        var dateTaken = exifData.DateTaken.HasValue
            ? DateTime.SpecifyKind(exifData.DateTaken.Value, DateTimeKind.Utc)
            : DateTime.UtcNow;

        var videoDir = Path.Combine(settings.StorageBasePath, "videos",
            dateTaken.Year.ToString(), dateTaken.Month.ToString("D2"), dateTaken.Day.ToString("D2"));
        Directory.CreateDirectory(videoDir);

        string destPath;
        string storedFilename;
        string mimeType;

        if (!NativeBrowserVideoExtensions.Contains(originalExtension))
        {
            // Move to storage so ffmpeg container can access it
            var baseName = $"{dateTaken:yyyy-MM-dd_HHmmss}_{hash[..8]}";
            var originalInStorage = Path.Combine(videoDir, baseName + originalExtension);
            File.Move(incomingFilePath, originalInStorage, overwrite: true);

            var mp4Filename = baseName + ".mp4";
            var mp4Path = Path.Combine(videoDir, mp4Filename);

            var transcoded = await _videoThumbnailService.TranscodeToMp4Async(originalInStorage, mp4Path, settings.StorageBasePath);
            if (transcoded != null)
            {
                File.Delete(originalInStorage);
                destPath = mp4Path;
                storedFilename = mp4Filename;
                mimeType = "video/mp4";
            }
            else
            {
                _logger.LogWarning("Transcoding failed for {FilePath}, storing original as fallback", incomingFilePath);
                destPath = originalInStorage;
                storedFilename = baseName + originalExtension;
                mimeType = GetMimeType(originalExtension);
            }
        }
        else
        {
            storedFilename = $"{dateTaken:yyyy-MM-dd_HHmmss}_{hash[..8]}{originalExtension}";
            destPath = Path.Combine(videoDir, storedFilename);
            File.Move(incomingFilePath, destPath, overwrite: true);
            mimeType = GetMimeType(originalExtension);
        }

        // Video thumbnail
        var thumbnailDir = Path.Combine(settings.StorageBasePath, "thumbnails");
        Directory.CreateDirectory(thumbnailDir);
        var thumbFilename = $"{Path.GetFileNameWithoutExtension(storedFilename)}_thumb.jpg";
        var thumbnailPath = Path.Combine(thumbnailDir, thumbFilename);
        var generatedThumb = await _videoThumbnailService.GenerateThumbnailAsync(destPath, thumbnailPath, settings.StorageBasePath);

        var fileInfo = new FileInfo(destPath);
        var image = new Image
        {
            OriginalFilename = originalFilename,
            StoredFilename = storedFilename,
            FilePath = destPath,
            ThumbnailPath = generatedThumb ?? string.Empty,
            FileSizeBytes = fileInfo.Length,
            Hash = hash,
            MimeType = mimeType,
            IsVideo = true,
            DateTaken = exifData.DateTaken.HasValue ? dateTaken : null,
            GpsLatitude = exifData.Latitude,
            GpsLongitude = exifData.Longitude,
            CameraModel = exifData.CameraModel,
            TopicId = topicId,
            ProcessedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await _imageRepository.AddAsync(image);
        _logger.LogInformation("Successfully organized video {StoredFilename} (id={Id})", storedFilename, image.Id);
        return image;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<string?> GenerateHeicThumbnailAsync(
        string heicPath, string thumbnailPath, string thumbnailDir, AppSettings settings)
    {
        var tmpJpeg = Path.Combine(thumbnailDir, Path.GetFileNameWithoutExtension(thumbnailPath) + "_heic_tmp.jpg");
        try
        {
            var converted = await _videoThumbnailService.ConvertToJpegAsync(heicPath, tmpJpeg, settings.StorageBasePath);
            if (converted == null)
            {
                _logger.LogWarning("HEIC conversion failed for {Path}, no thumbnail generated", heicPath);
                return null;
            }
            return await _thumbnailService.GenerateAsync(tmpJpeg, thumbnailPath, settings.ThumbnailWidth, settings.ThumbnailHeight);
        }
        finally
        {
            if (File.Exists(tmpJpeg))
                try { File.Delete(tmpJpeg); } catch { /* ignore */ }
        }
    }

    private async Task MoveToDuplicatesAsync(string incomingFilePath, Image existingImage, AppSettings settings)
    {
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
    }

    private static string GetMimeType(string extension) => extension switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png"            => "image/png",
        ".heic" or ".heif"=> "image/heic",
        ".gif"            => "image/gif",
        ".webp"           => "image/webp",
        ".mp4" or ".m4v"  => "video/mp4",
        ".webm"           => "video/webm",
        ".mov"            => "video/quicktime",
        ".avi"            => "video/x-msvideo",
        ".mkv"            => "video/x-matroska",
        ".ogv"            => "video/ogg",
        _                 => "application/octet-stream"
    };
}
