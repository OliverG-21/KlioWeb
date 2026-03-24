using System.IO.Compression;
using Klio.Core.Interfaces;
using Klio.Core.Models;
using Klio.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Klio.Tests.Services;

[Trait("Category", "Unit")]
public class FolderImportServiceTests : IDisposable
{
    private readonly IProcessingQueue        _queue            = Substitute.For<IProcessingQueue>();
    private readonly IAlbumFolderRepository  _folderRepository = Substitute.For<IAlbumFolderRepository>();
    private readonly ITopicRepository        _topicRepository  = Substitute.For<ITopicRepository>();
    private readonly ILogger<FolderImportService> _logger      = Substitute.For<ILogger<FolderImportService>>();

    private readonly FolderImportService _sut;
    private readonly List<string> _tempDirs = [];

    public FolderImportServiceTests()
    {
        _sut = new FolderImportService(_queue, _folderRepository, _topicRepository, _logger);

        // Default: no existing folders / albums
        _folderRepository.GetByParentIdAsync(Arg.Any<int?>())
            .Returns(Enumerable.Empty<AlbumFolder>());

        _topicRepository.GetByFolderIdAsync(Arg.Any<int?>())
            .Returns(Enumerable.Empty<Topic>());

        // Default: AddAsync sets Id and returns folder/topic
        _folderRepository
            .AddAsync(Arg.Any<AlbumFolder>())
            .Returns(callInfo => Task.CompletedTask);

        _topicRepository
            .AddAsync(Arg.Any<Topic>())
            .Returns(callInfo =>
            {
                var topic = callInfo.Arg<Topic>();
                topic.Id = 1;
                return Task.FromResult(topic);
            });
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"klio_import_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    private static void WriteMinimalJpeg(string path)
    {
        using var img = new Image<Rgb24>(4, 4);
        img.SaveAsJpeg(path);
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportAsync_EmptyDirectory_ReturnsZeroCounts()
    {
        // Arrange
        var emptyDir = CreateTempDir();

        // Act
        var result = await _sut.ImportAsync(emptyDir);

        // Assert
        Assert.Equal(0, result.FilesEnqueued);
        Assert.Equal(0, result.FoldersCreated);
        Assert.Equal(0, result.AlbumsCreated);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ImportAsync_DirectoryWithImages_EnqueuesFiles()
    {
        // Arrange
        var inbox = CreateTempDir();
        WriteMinimalJpeg(Path.Combine(inbox, "a.jpg"));
        WriteMinimalJpeg(Path.Combine(inbox, "b.jpg"));

        // Act
        var result = await _sut.ImportAsync(inbox);

        // Assert
        Assert.Equal(2, result.FilesEnqueued);
        _queue.Received(2).Enqueue(Arg.Any<string>(), Arg.Any<int?>());
    }

    [Fact]
    public async Task ImportAsync_NestedDirectory_CreatesFolderHierarchy()
    {
        // Arrange
        var inbox  = CreateTempDir();
        var subDir = Path.Combine(inbox, "MyAlbum");
        Directory.CreateDirectory(subDir);
        WriteMinimalJpeg(Path.Combine(subDir, "photo.jpg"));

        // Act
        var result = await _sut.ImportAsync(inbox);

        // Assert – at least one AlbumFolder should have been created for the subdir
        await _folderRepository.Received().AddAsync(Arg.Is<AlbumFolder>(f =>
            f.Name == "MyAlbum"));
        Assert.True(result.FoldersCreated >= 1);
    }

    [Fact]
    public async Task ImportAsync_ZipFile_ExtractsAndEnqueuesImages()
    {
        // Arrange
        var inbox = CreateTempDir();

        // Build a real ZIP containing one JPEG
        var zipPath  = Path.Combine(inbox, "archive.zip");
        var jpegName = "inside.jpg";
        using (var ms = new MemoryStream())
        {
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = zip.CreateEntry(jpegName);
                using var entryStream = entry.Open();
                using var tempImg = new Image<Rgb24>(4, 4);
                tempImg.SaveAsJpeg(entryStream);
            }
            await File.WriteAllBytesAsync(zipPath, ms.ToArray());
        }

        // Act
        var result = await _sut.ImportAsync(inbox);

        // Assert
        Assert.Equal(1, result.FilesEnqueued);
        _queue.Received(1).Enqueue(Arg.Any<string>(), Arg.Any<int?>());
    }

    [Fact]
    public async Task ClearFolderInboxAsync_DeletesAllContents()
    {
        // Arrange
        var inbox = CreateTempDir();
        WriteMinimalJpeg(Path.Combine(inbox, "file1.jpg"));
        WriteMinimalJpeg(Path.Combine(inbox, "file2.jpg"));
        var subDir = Path.Combine(inbox, "sub");
        Directory.CreateDirectory(subDir);
        WriteMinimalJpeg(Path.Combine(subDir, "nested.jpg"));

        // Act
        await _sut.ClearFolderInboxAsync(inbox);

        // Assert – directory itself should still exist but be empty
        Assert.True(Directory.Exists(inbox));
        Assert.Empty(Directory.GetFileSystemEntries(inbox));
    }

    public void Dispose()
    {
        foreach (var d in _tempDirs)
            try { if (Directory.Exists(d)) Directory.Delete(d, recursive: true); } catch { /* ignore */ }
    }
}
