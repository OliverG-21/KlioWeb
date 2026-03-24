using Klio.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Klio.Tests.Services;

[Trait("Category", "Unit")]
public class ThumbnailServiceTests : IDisposable
{
    private readonly ILogger<ThumbnailService> _logger = Substitute.For<ILogger<ThumbnailService>>();
    private readonly ThumbnailService _sut;
    private readonly List<string> _tempFiles = [];
    private readonly List<string> _tempDirs  = [];

    public ThumbnailServiceTests()
    {
        _sut = new ThumbnailService(_logger);
    }

    // Creates a real 10x10 JPEG using ImageSharp and returns the path.
    private string CreateTempJpeg(int width = 10, int height = 10)
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".jpg");
        using var img = new Image<Rgb24>(width, height);
        img.SaveAsJpeg(path);
        _tempFiles.Add(path);
        return path;
    }

    private string NewTempPath(string extension = ".jpg")
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), extension);
        _tempFiles.Add(path);
        return path;
    }

    [Fact]
    public async Task GenerateAsync_ValidJpeg_CreatesOutputFile()
    {
        // Arrange
        var source = CreateTempJpeg();
        var output = NewTempPath();

        // Act
        var result = await _sut.GenerateAsync(source, output, 100, 100);

        // Assert
        Assert.NotNull(result);
        Assert.True(File.Exists(output));
    }

    [Fact]
    public async Task GenerateAsync_ValidJpeg_OutputDimensionsRespectMaxBounds()
    {
        // Arrange – 100x100 source, constrain to 50x50
        var source = CreateTempJpeg(100, 100);
        var output = NewTempPath();
        const int maxWidth  = 50;
        const int maxHeight = 50;

        // Act
        var result = await _sut.GenerateAsync(source, output, maxWidth, maxHeight);

        // Assert
        Assert.NotNull(result);
        using var thumb = await Image.LoadAsync(output);
        Assert.True(thumb.Width  <= maxWidth,  $"Width {thumb.Width} exceeds maxWidth {maxWidth}");
        Assert.True(thumb.Height <= maxHeight, $"Height {thumb.Height} exceeds maxHeight {maxHeight}");
    }

    [Fact]
    public async Task GenerateAsync_HeicFile_ReturnsNull()
    {
        // Arrange – HEIC extension causes early return without reading content
        var heicPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.heic");
        File.WriteAllBytes(heicPath, [0x00]); // content irrelevant, skipped before loading
        _tempFiles.Add(heicPath);
        var output = NewTempPath();

        // Act
        var result = await _sut.GenerateAsync(heicPath, output, 100, 100);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateAsync_NonExistentSourceFile_ReturnsNull()
    {
        // Arrange
        var nonExistent = Path.Combine(Path.GetTempPath(), $"does_not_exist_{Guid.NewGuid():N}.jpg");
        var output = NewTempPath();

        // Act
        var result = await _sut.GenerateAsync(nonExistent, output, 100, 100);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateAsync_CreatesOutputDirectory_WhenNotExists()
    {
        // Arrange
        var source  = CreateTempJpeg();
        var newDir  = Path.Combine(Path.GetTempPath(), $"klio_test_{Guid.NewGuid():N}");
        var output  = Path.Combine(newDir, "thumb.jpg");
        _tempDirs.Add(newDir);

        // Act
        var result = await _sut.GenerateAsync(source, output, 100, 100);

        // Assert
        Assert.NotNull(result);
        Assert.True(File.Exists(output));
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            try { if (File.Exists(f)) File.Delete(f); } catch { /* ignore */ }

        foreach (var d in _tempDirs)
            try { if (Directory.Exists(d)) Directory.Delete(d, recursive: true); } catch { /* ignore */ }
    }
}
