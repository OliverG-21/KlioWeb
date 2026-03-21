using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Projekt42.Core.Interfaces;
using Projekt42.Core.Models;

namespace Projekt42.Web.Pages.Admin;

[RequestFormLimits(MultipartBodyLengthLimit = 1_073_741_824)] // 1 GB
[RequestSizeLimit(1_073_741_824)]                              // 1 GB
public class UploadModel : PageModel
{
    private readonly IAppSettingsRepository _appSettingsRepository;
    private readonly IProcessingQueue _processingQueue;
    private readonly ITopicRepository _topicRepository;
    private readonly IAlbumFolderRepository _folderRepository;
    private readonly IVideoThumbnailService _videoThumbnailService;
    private readonly IImageRepository _imageRepository;

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".webm", ".mov", ".m4v", ".avi", ".mkv", ".ogv" };

    // Formats natively supported by all major browsers — no transcoding needed
    private static readonly HashSet<string> NativeBrowserVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".webm" };

    private static readonly Dictionary<string, string> VideoMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp4"]  = "video/mp4",
        [".webm"] = "video/webm",
        [".mov"]  = "video/quicktime",
        [".m4v"]  = "video/mp4",
        [".avi"]  = "video/x-msvideo",
        [".mkv"]  = "video/x-matroska",
        [".ogv"]  = "video/ogg"
    };

    public string? SuccessMessage { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? InboxPath { get; private set; }

    // Albums for the "existing" dropdown: (topicId, display label)
    public List<(int Id, string Label)> Albums { get; private set; } = [];

    // Leaf folders for the "new album" folder dropdown: (folderId?, display label)
    public List<(int? Id, string Label)> LeafFolders { get; private set; } = [];

    public UploadModel(
        IAppSettingsRepository appSettingsRepository,
        IProcessingQueue processingQueue,
        ITopicRepository topicRepository,
        IAlbumFolderRepository folderRepository,
        IVideoThumbnailService videoThumbnailService,
        IImageRepository imageRepository)
    {
        _appSettingsRepository = appSettingsRepository;
        _processingQueue = processingQueue;
        _topicRepository = topicRepository;
        _folderRepository = folderRepository;
        _videoThumbnailService = videoThumbnailService;
        _imageRepository = imageRepository;
    }

    public int? PreselectTopicId { get; private set; }

    public async Task OnGetAsync(int? topicId)
    {
        PreselectTopicId = topicId;
        var settings = await _appSettingsRepository.GetAsync();
        InboxPath = settings.InboxPath;
        await LoadDropdownsAsync();
    }

    public async Task<IActionResult> OnPostAsync(
        IFormFileCollection files,
        string? topicMode,       // "existing" | "new" | "none"
        int? topicId,
        string? newAlbumName,
        int? newAlbumFolderId)
    {
        var settings = await _appSettingsRepository.GetAsync();
        InboxPath = settings.InboxPath;
        await LoadDropdownsAsync();

        if (files == null || files.Count == 0)
        {
            ErrorMessage = "Bitte mindestens eine Datei auswählen.";
            return Page();
        }

        int? effectiveTopicId = null;
        string albumLabel = "";

        if (topicMode == "existing" && topicId.HasValue)
        {
            effectiveTopicId = topicId.Value;
            albumLabel = Albums.FirstOrDefault(a => a.Id == effectiveTopicId).Label ?? "";
        }
        else if (topicMode == "new")
        {
            if (string.IsNullOrWhiteSpace(newAlbumName))
            {
                ErrorMessage = "Bitte einen Albumnamen eingeben.";
                return Page();
            }
            var newTopic = new Topic
            {
                Name = newAlbumName.Trim(),
                Description = string.Empty,
                FolderId = newAlbumFolderId,
                CreatedAt = DateTime.UtcNow
            };
            await _topicRepository.AddAsync(newTopic);
            effectiveTopicId = newTopic.Id;
            albumLabel = newAlbumName.Trim();
        }

        if (!Directory.Exists(settings.InboxPath))
        {
            try { Directory.CreateDirectory(settings.InboxPath); }
            catch (Exception ex)
            {
                ErrorMessage = $"Eingangsordner konnte nicht erstellt werden: {ex.Message}";
                return Page();
            }
        }

        var savedCount = 0;
        var errors = new List<string>();

        foreach (var file in files)
        {
            if (file.Length == 0) continue;
            try
            {
                var ext = Path.GetExtension(file.FileName);
                if (VideoExtensions.Contains(ext))
                {
                    savedCount += await SaveVideoDirectlyAsync(file, settings.StorageBasePath, effectiveTopicId);
                }
                else
                {
                    var safeFileName = Path.GetFileName(file.FileName);
                    var uniqueName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{safeFileName}";
                    var destPath = Path.Combine(settings.InboxPath, uniqueName);
                    await using var stream = new FileStream(destPath, FileMode.Create);
                    await file.CopyToAsync(stream);
                    _processingQueue.Enqueue(destPath, effectiveTopicId);
                    savedCount++;
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{file.FileName}: {ex.Message}");
            }
        }

        if (errors.Any())
            ErrorMessage = $"Fehler bei {errors.Count} Datei(en): {string.Join("; ", errors)}";

        if (savedCount > 0)
        {
            var albumInfo = !string.IsNullOrEmpty(albumLabel) ? $" → {albumLabel}" : "";
            SuccessMessage = $"{savedCount} Datei(en) hochgeladen und zur Verarbeitung eingereicht.{albumInfo}";
        }

        return Page();
    }

    private async Task<int> SaveVideoDirectlyAsync(IFormFile file, string storagePath, int? effectiveTopicId)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var mimeType = VideoMimeTypes.GetValueOrDefault(ext, "video/mp4");
        var uniqueName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Path.GetFileName(file.FileName)}";

        // Save to temp first to compute hash and extract metadata
        var tempPath = Path.Combine(Path.GetTempPath(), uniqueName);
        await using (var stream = new FileStream(tempPath, FileMode.Create))
            await file.CopyToAsync(stream);

        // Compute hash
        using var fs = System.IO.File.OpenRead(tempPath);
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hashBytes = await sha.ComputeHashAsync(fs);
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        // Try to extract date (use now if unavailable)
        DateTime dateTaken = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        try
        {
            var metadata = MetadataExtractor.ImageMetadataReader.ReadMetadata(tempPath);
            foreach (var dir in metadata)
            {
                foreach (var tag in dir.Tags)
                {
                    if (tag.Name.Contains("Date", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(tag.Description))
                    {
                        if (DateTime.TryParseExact(tag.Description, "yyyy:MM:dd HH:mm:ss",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out var parsed))
                        {
                            dateTaken = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
                            break;
                        }
                    }
                }
                if (dateTaken != DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)) break;
            }
        }
        catch { /* fall back to UtcNow */ }

        // Build destination path
        var videoDir = Path.Combine(storagePath, "videos", dateTaken.ToString("yyyy"), dateTaken.ToString("MM"), dateTaken.ToString("dd"));
        Directory.CreateDirectory(videoDir);

        // Transcode non-native formats (MOV, AVI, MKV, …) to H.264 MP4 for browser compatibility
        string destPath;
        string storedFilename;
        string finalMimeType;
        if (!NativeBrowserVideoExtensions.Contains(ext))
        {
            var mp4Name = Path.GetFileNameWithoutExtension(uniqueName) + ".mp4";
            var mp4Path = Path.Combine(videoDir, mp4Name);
            System.IO.File.Move(tempPath, Path.Combine(Path.GetTempPath(), uniqueName), overwrite: true);
            var transcoded = await _videoThumbnailService.TranscodeToMp4Async(
                Path.Combine(Path.GetTempPath(), uniqueName), mp4Path, storagePath);
            System.IO.File.Delete(Path.Combine(Path.GetTempPath(), uniqueName));
            if (transcoded != null)
            {
                destPath       = mp4Path;
                storedFilename = mp4Name;
                finalMimeType  = "video/mp4";
            }
            else
            {
                // Transcoding failed — store original as fallback
                destPath       = Path.Combine(videoDir, uniqueName);
                storedFilename = uniqueName;
                finalMimeType  = mimeType;
                System.IO.File.Move(Path.Combine(Path.GetTempPath(), uniqueName), destPath, overwrite: true);
            }
        }
        else
        {
            destPath       = Path.Combine(videoDir, uniqueName);
            storedFilename = uniqueName;
            finalMimeType  = mimeType;
            System.IO.File.Move(tempPath, destPath, overwrite: true);
        }

        // Generate thumbnail
        var thumbDir = Path.Combine(storagePath, "thumbnails");
        Directory.CreateDirectory(thumbDir);
        var thumbName = Path.GetFileNameWithoutExtension(storedFilename) + "_thumb.jpg";
        var thumbPath = Path.Combine(thumbDir, thumbName);
        var generatedThumb = await _videoThumbnailService.GenerateThumbnailAsync(destPath, thumbPath, storagePath);

        // Save to DB
        var image = new Image
        {
            OriginalFilename = file.FileName,
            StoredFilename   = storedFilename,
            FilePath         = destPath,
            ThumbnailPath    = generatedThumb ?? "",
            FileSizeBytes    = new System.IO.FileInfo(destPath).Length,
            Hash             = hash,
            MimeType         = finalMimeType,
            DateTaken        = dateTaken,
            TopicId          = effectiveTopicId,
            IsVideo          = true,
            ProcessedAt      = DateTime.UtcNow,
            CreatedAt        = DateTime.UtcNow
        };
        await _imageRepository.AddAsync(image);
        return 1;
    }

    private async Task LoadDropdownsAsync()
    {
        var allFolders = (await _folderRepository.GetAllAsync()).ToList();
        var allTopics  = (await _topicRepository.GetAllAsync()).OrderBy(t => t.Name).ToList();

        // Albums with full path label
        foreach (var topic in allTopics)
            Albums.Add((topic.Id, BuildFolderPath(topic.FolderId, allFolders) + topic.Name));

        // All folders as targets for new album
        LeafFolders.Add((null, "— Root (kein Ordner) —"));
        foreach (var f in allFolders.OrderBy(f => f.Name))
            LeafFolders.Add((f.Id, BuildFolderPath(f.ParentFolderId, allFolders) + f.Name));
    }

    private static string BuildFolderPath(int? folderId, List<AlbumFolder> allFolders)
    {
        if (folderId == null) return "";
        var parts = new List<string>();
        var current = allFolders.FirstOrDefault(f => f.Id == folderId);
        while (current != null)
        {
            parts.Insert(0, current.Name);
            current = current.ParentFolderId.HasValue
                ? allFolders.FirstOrDefault(f => f.Id == current.ParentFolderId!.Value)
                : null;
        }
        return parts.Count > 0 ? string.Join(" / ", parts) + " / " : "";
    }
}
