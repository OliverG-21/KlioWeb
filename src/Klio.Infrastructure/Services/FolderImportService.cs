using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Klio.Core.Interfaces;
using Klio.Core.Models;

namespace Klio.Infrastructure.Services;

public class FolderImportService : IFolderImportService
{
    private readonly IProcessingQueue _queue;
    private readonly IAlbumFolderRepository _folderRepository;
    private readonly ITopicRepository _topicRepository;
    private readonly ILogger<FolderImportService> _logger;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".heic", ".heif",
        ".mp4", ".webm", ".mov", ".m4v", ".avi", ".mkv", ".ogv"
    };

    public FolderImportService(
        IProcessingQueue queue,
        IAlbumFolderRepository folderRepository,
        ITopicRepository topicRepository,
        ILogger<FolderImportService> logger)
    {
        _queue = queue;
        _folderRepository = folderRepository;
        _topicRepository = topicRepository;
        _logger = logger;
    }

    public async Task<FolderImportResult> ImportAsync(string folderInboxPath)
    {
        var counters = new ImportCounters();

        if (!Directory.Exists(folderInboxPath))
        {
            counters.Errors.Add($"FolderInbox-Verzeichnis existiert nicht: {folderInboxPath}");
            return counters.ToResult();
        }

        foreach (var entry in Directory.GetFileSystemEntries(folderInboxPath))
        {
            if (Path.GetFileName(entry).StartsWith(".tmp_"))
                continue;

            try
            {
                if (Directory.Exists(entry))
                {
                    await ProcessDirectoryAsync(entry, null, counters);
                }
                else if (File.Exists(entry))
                {
                    var ext = Path.GetExtension(entry);
                    if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                        await ProcessZipAsync(entry, folderInboxPath, counters);
                    else if (SupportedExtensions.Contains(ext))
                    {
                        _queue.Enqueue(entry, null);
                        counters.FilesEnqueued++;
                    }
                }
            }
            catch (Exception ex)
            {
                counters.Errors.Add($"{Path.GetFileName(entry)}: {ex.Message}");
                _logger.LogError(ex, "Error importing entry: {Entry}", entry);
            }
        }

        return counters.ToResult();
    }

    private async Task ProcessDirectoryAsync(string dirPath, int? parentFolderId, ImportCounters counters)
    {
        var dirName = Path.GetFileName(dirPath);

        var (folderId, folderIsNew) = await FindOrCreateFolderAsync(dirName, parentFolderId);
        if (folderIsNew) counters.FoldersCreated++;

        var directFiles = Directory.GetFiles(dirPath)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        int? topicId = null;
        if (directFiles.Count > 0)
        {
            var (albumId, albumIsNew) = await FindOrCreateAlbumAsync(dirName, folderId);
            if (albumIsNew) counters.AlbumsCreated++;
            else counters.AlbumsReused++;
            topicId = albumId;
        }

        foreach (var file in directFiles)
        {
            _queue.Enqueue(file, topicId);
            counters.FilesEnqueued++;
        }

        foreach (var subDir in Directory.GetDirectories(dirPath))
        {
            try
            {
                await ProcessDirectoryAsync(subDir, folderId, counters);
            }
            catch (Exception ex)
            {
                counters.Errors.Add($"{Path.GetFileName(subDir)}: {ex.Message}");
                _logger.LogError(ex, "Error processing subdirectory: {SubDir}", subDir);
            }
        }
    }

    private async Task ProcessZipAsync(string zipPath, string extractBasePath, ImportCounters counters)
    {
        var zipName = Path.GetFileName(zipPath);
        var tempDir = Path.Combine(extractBasePath, $".tmp_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            ZipFile.ExtractToDirectory(zipPath, tempDir, overwriteFiles: true);

            foreach (var entry in Directory.GetFileSystemEntries(tempDir))
            {
                if (Directory.Exists(entry))
                {
                    await ProcessDirectoryAsync(entry, null, counters);
                }
                else if (File.Exists(entry))
                {
                    var ext = Path.GetExtension(entry);
                    if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        // ZIP-in-ZIP: nicht unterstützt
                        var msg = $"{zipName}: verschachtelte ZIP-Datei '{Path.GetFileName(entry)}' wird ignoriert.";
                        counters.Errors.Add(msg);
                        _logger.LogWarning("ZIP-in-ZIP ignored: {NestedZip}", entry);
                    }
                    else if (SupportedExtensions.Contains(ext))
                    {
                        _queue.Enqueue(entry, null);
                        counters.FilesEnqueued++;
                    }
                }
            }

            // ZIP nach erfolgreichem Entpacken löschen
            File.Delete(zipPath);
            _logger.LogInformation("Deleted ZIP after extraction: {ZipPath}", zipPath);
        }
        catch (Exception ex)
        {
            counters.Errors.Add($"{zipName} (ZIP): {ex.Message}");
            _logger.LogError(ex, "Error extracting ZIP: {ZipPath}", zipPath);
            if (Directory.Exists(tempDir))
                try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    public Task ClearFolderInboxAsync(string folderInboxPath)
    {
        if (!Directory.Exists(folderInboxPath))
            return Task.CompletedTask;

        foreach (var entry in Directory.GetFileSystemEntries(folderInboxPath))
        {
            try
            {
                if (Directory.Exists(entry)) Directory.Delete(entry, recursive: true);
                else File.Delete(entry);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete FolderInbox entry: {Entry}", entry);
            }
        }
        return Task.CompletedTask;
    }

    private async Task<(int Id, bool IsNew)> FindOrCreateFolderAsync(string name, int? parentFolderId)
    {
        var siblings = await _folderRepository.GetByParentIdAsync(parentFolderId);
        var existing = siblings.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return (existing.Id, false);

        var folder = new AlbumFolder { Name = name, ParentFolderId = parentFolderId, CreatedAt = DateTime.UtcNow };
        await _folderRepository.AddAsync(folder);
        _logger.LogInformation("Created AlbumFolder '{Name}' (parentId: {ParentId})", name, parentFolderId);
        return (folder.Id, true);
    }

    private async Task<(int Id, bool IsNew)> FindOrCreateAlbumAsync(string name, int folderId)
    {
        var siblings = await _topicRepository.GetByFolderIdAsync(folderId);
        var existing = siblings.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return (existing.Id, false);

        var topic = new Topic { Name = name, Description = string.Empty, FolderId = folderId, CreatedAt = DateTime.UtcNow };
        await _topicRepository.AddAsync(topic);
        _logger.LogInformation("Created Topic '{Name}' (folderId: {FolderId})", name, folderId);
        return (topic.Id, true);
    }

    private sealed class ImportCounters
    {
        public int FoldersCreated;
        public int AlbumsCreated;
        public int AlbumsReused;
        public int FilesEnqueued;
        public List<string> Errors { get; } = [];

        public FolderImportResult ToResult() =>
            new(FoldersCreated, AlbumsCreated, AlbumsReused, FilesEnqueued, Errors);
    }
}
