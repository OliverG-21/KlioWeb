using Klio.Core.Interfaces;
using Klio.Core.Models;
using Klio.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Klio.Tests.Services;

[Trait("Category", "Unit")]
public class FileOrganizerServiceTests : IDisposable
{
    // ── Mocks ───────────────────────────────────────────────────────────────
    private readonly IHashService               _hashService               = Substitute.For<IHashService>();
    private readonly IExifService               _exifService               = Substitute.For<IExifService>();
    private readonly IThumbnailService          _thumbnailService          = Substitute.For<IThumbnailService>();
    private readonly IVideoThumbnailService     _videoThumbnailService     = Substitute.For<IVideoThumbnailService>();
    private readonly IImageRepository           _imageRepository           = Substitute.For<IImageRepository>();
    private readonly IDuplicateLogRepository    _duplicateLogRepository    = Substitute.For<IDuplicateLogRepository>();
    private readonly IAppSettingsRepository     _appSettingsRepository     = Substitute.For<IAppSettingsRepository>();
    private readonly ITaggingQueue              _taggingQueue              = Substitute.For<ITaggingQueue>();
    private readonly ILogger<FileOrganizerService> _logger                 = Substitute.For<ILogger<FileOrganizerService>>();

    private readonly FileOrganizerService _sut;

    // Temp infrastructure
    private readonly string _storageBase;
    private readonly List<string> _tempFiles = [];
    private readonly List<string> _tempDirs  = [];

    public FileOrganizerServiceTests()
    {
        _storageBase = Path.Combine(Path.GetTempPath(), $"klio_organizer_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_storageBase);
        _tempDirs.Add(_storageBase);

        _sut = new FileOrganizerService(
            _hashService,
            _exifService,
            _thumbnailService,
            _videoThumbnailService,
            _imageRepository,
            _duplicateLogRepository,
            _appSettingsRepository,
            _taggingQueue,
            _logger);

        // Default: AddAsync sets Id on the passed image and returns it
        _imageRepository
            .AddAsync(Arg.Any<Core.Models.Image>())
            .Returns(callInfo =>
            {
                var img = callInfo.Arg<Core.Models.Image>();
                img.Id = 1;
                return Task.FromResult(img);
            });

        // Default: no duplicate
        _imageRepository.GetByHashAsync(Arg.Any<string>()).Returns((Core.Models.Image?)null);

        // Default: thumbnail returns a path
        _thumbnailService
            .GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(callInfo => Task.FromResult<string?>(callInfo.ArgAt<string>(1)));
    }

    // Creates a real 10x10 JPEG in a temp location that can be moved by the service.
    private string CreateInboxJpeg(string filename = "test.jpg")
    {
        var inboxDir = Path.Combine(_storageBase, "inbox");
        Directory.CreateDirectory(inboxDir);
        var path = Path.Combine(inboxDir, filename);
        using var img = new Image<Rgb24>(10, 10);
        img.SaveAsJpeg(path);
        // Do NOT add to _tempFiles – the service will move it
        return path;
    }

    private AppSettings MakeSettings(bool taggingEnabled = false) => new()
    {
        StorageBasePath = _storageBase,
        ThumbnailWidth  = 300,
        ThumbnailHeight = 300,
        TaggingEnabled  = taggingEnabled
    };

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OrganizeAsync_NewImage_MovesFileToDateBasedPath()
    {
        // Arrange
        var incomingPath = CreateInboxJpeg("photo.jpg");
        var dateTaken    = new DateTime(2024, 6, 15, 10, 30, 0);

        _hashService.ComputeAsync(incomingPath).Returns("abc123def456abc123def456abc123def456abc123def456abc123def456abc1");
        _exifService.ExtractAsync(incomingPath).Returns(new ExifData(dateTaken, null, null, null));
        _appSettingsRepository.GetAsync().Returns(MakeSettings());

        // Act
        var result = await _sut.OrganizeAsync(incomingPath);

        // Assert – file path stored in DB should contain YYYY/MM/DD
        await _imageRepository.Received(1).AddAsync(Arg.Is<Core.Models.Image>(img =>
            img.FilePath.Contains("2024") &&
            img.FilePath.Contains("06") &&
            img.FilePath.Contains("15")));
    }

    [Fact]
    public async Task OrganizeAsync_NewImageNoExifDate_UsesNodatePath()
    {
        // Arrange
        var incomingPath = CreateInboxJpeg("nodate.jpg");

        _hashService.ComputeAsync(incomingPath).Returns("abc123def456abc123def456abc123def456abc123def456abc123def456abc1");
        _exifService.ExtractAsync(incomingPath).Returns(new ExifData(null, null, null, null));
        _appSettingsRepository.GetAsync().Returns(MakeSettings());

        // Act
        var result = await _sut.OrganizeAsync(incomingPath);

        // Assert – file path should contain "nodate"
        await _imageRepository.Received(1).AddAsync(Arg.Is<Core.Models.Image>(img =>
            img.FilePath.Contains("nodate")));
    }

    [Fact]
    public async Task OrganizeAsync_DuplicateHash_CallsDuplicateLog()
    {
        // Arrange
        var incomingPath  = CreateInboxJpeg("dup.jpg");
        var existingImage = new Core.Models.Image
        {
            Id       = 42,
            FilePath = Path.Combine(_storageBase, "2024", "01", "01", "existing.jpg")
        };

        _hashService.ComputeAsync(incomingPath).Returns("abc123def456abc123def456abc123def456abc123def456abc123def456abc1");
        _imageRepository.GetByHashAsync(Arg.Any<string>()).Returns(existingImage);
        _appSettingsRepository.GetAsync().Returns(MakeSettings());

        // Pre-create the existing image file so the service can look at its path
        // (it doesn't need the actual file, but MoveToDuplicatesAsync moves incomingPath)
        // The duplicate dir must exist for the move to work
        Directory.CreateDirectory(Path.Combine(_storageBase, "duplicates"));

        // Act
        var result = await _sut.OrganizeAsync(incomingPath);

        // Assert – duplicate log AddAsync must have been called
        await _duplicateLogRepository.Received(1).AddAsync(Arg.Any<DuplicateLog>());
        // The original AddAsync for a new image should NOT have been called
        await _imageRepository.DidNotReceive().AddAsync(Arg.Any<Core.Models.Image>());
    }

    [Fact]
    public async Task OrganizeAsync_TaggingEnabled_EnqueuesImageId()
    {
        // Arrange
        var incomingPath = CreateInboxJpeg("tagged.jpg");
        var dateTaken    = new DateTime(2024, 3, 10, 8, 0, 0);

        _hashService.ComputeAsync(incomingPath).Returns("abc123def456abc123def456abc123def456abc123def456abc123def456abc1");
        _exifService.ExtractAsync(incomingPath).Returns(new ExifData(dateTaken, null, null, null));
        _appSettingsRepository.GetAsync().Returns(MakeSettings(taggingEnabled: true));

        // Act
        await _sut.OrganizeAsync(incomingPath);

        // Assert
        _taggingQueue.Received(1).Enqueue(Arg.Any<int>());
    }

    [Fact]
    public async Task OrganizeAsync_TaggingDisabled_DoesNotEnqueue()
    {
        // Arrange
        var incomingPath = CreateInboxJpeg("notagging.jpg");
        var dateTaken    = new DateTime(2024, 3, 10, 8, 0, 0);

        _hashService.ComputeAsync(incomingPath).Returns("abc123def456abc123def456abc123def456abc123def456abc123def456abc1");
        _exifService.ExtractAsync(incomingPath).Returns(new ExifData(dateTaken, null, null, null));
        _appSettingsRepository.GetAsync().Returns(MakeSettings(taggingEnabled: false));

        // Act
        await _sut.OrganizeAsync(incomingPath);

        // Assert
        _taggingQueue.DidNotReceive().Enqueue(Arg.Any<int>());
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            try { if (File.Exists(f)) File.Delete(f); } catch { /* ignore */ }

        foreach (var d in _tempDirs)
            try { if (Directory.Exists(d)) Directory.Delete(d, recursive: true); } catch { /* ignore */ }
    }
}
