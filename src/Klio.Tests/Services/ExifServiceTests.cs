using Klio.Infrastructure.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace Klio.Tests.Services;

[Trait("Category", "Unit")]
public class ExifServiceTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    private string CreateJpegWithExif(Action<ExifProfile>? configureExif = null)
    {
        var path = Path.GetTempFileName() + ".jpg";
        _tempFiles.Add(path);

        using var image = new Image<Rgb24>(10, 10);
        if (configureExif != null)
        {
            var exif = new ExifProfile();
            configureExif(exif);
            image.Metadata.ExifProfile = exif;
        }
        image.SaveAsJpeg(path);
        return path;
    }

    [Fact]
    public async Task ExtractAsync_JpegWithDateTimeOriginal_ReturnsCorrectDate()
    {
        // Arrange – ImageSharp writes EXIF DateTimeOriginal in "YYYY:MM:DD HH:MM:SS" format
        var path = CreateJpegWithExif(exif =>
        {
            exif.SetValue(ExifTag.DateTimeOriginal, "2023:06:15 14:30:00");
        });

        var sut = new ExifService();

        // Act
        var result = await sut.ExtractAsync(path);

        // Assert
        Assert.NotNull(result.DateTaken);
        Assert.Equal(2023, result.DateTaken!.Value.Year);
        Assert.Equal(6,    result.DateTaken!.Value.Month);
        Assert.Equal(15,   result.DateTaken!.Value.Day);
        Assert.Equal(14,   result.DateTaken!.Value.Hour);
        Assert.Equal(30,   result.DateTaken!.Value.Minute);
    }

    [Fact]
    public async Task ExtractAsync_JpegWithCameraInfo_ReturnsMakeAndModel()
    {
        // Arrange
        var path = CreateJpegWithExif(exif =>
        {
            exif.SetValue(ExifTag.Make,  "Apple");
            exif.SetValue(ExifTag.Model, "iPhone 14");
        });

        var sut = new ExifService();

        // Act
        var result = await sut.ExtractAsync(path);

        // Assert – Make is not a prefix of "iPhone 14", so both are concatenated
        Assert.NotNull(result.CameraModel);
        Assert.Contains("Apple",     result.CameraModel);
        Assert.Contains("iPhone 14", result.CameraModel);
    }

    [Fact]
    public async Task ExtractAsync_JpegWithGps_ReturnsLatitudeLongitude()
    {
        // Arrange – GPS coordinates: 48°8'0" N, 11°34'0" E  ≈  48.133, 11.567
        var path = CreateJpegWithExif(exif =>
        {
            exif.SetValue(ExifTag.GPSLatitude,    new[] { new Rational(48, 1), new Rational(8, 1), new Rational(0, 1) });
            exif.SetValue(ExifTag.GPSLatitudeRef, "N");
            exif.SetValue(ExifTag.GPSLongitude,   new[] { new Rational(11, 1), new Rational(34, 1), new Rational(0, 1) });
            exif.SetValue(ExifTag.GPSLongitudeRef, "E");
        });

        var sut = new ExifService();

        // Act
        var result = await sut.ExtractAsync(path);

        // Assert
        Assert.NotNull(result.Latitude);
        Assert.NotNull(result.Longitude);
        Assert.True(result.Latitude  > 48.0 && result.Latitude  < 49.0,   $"Lat={result.Latitude}");
        Assert.True(result.Longitude > 11.0 && result.Longitude < 12.0, $"Lon={result.Longitude}");
    }

    [Fact]
    public async Task ExtractAsync_JpegWithNoExif_ReturnsEmptyExifData()
    {
        // Arrange – no EXIF profile at all
        var path = CreateJpegWithExif(configureExif: null);

        var sut = new ExifService();

        // Act
        var result = await sut.ExtractAsync(path);

        // Assert
        Assert.Null(result.DateTaken);
        Assert.Null(result.Latitude);
        Assert.Null(result.Longitude);
        Assert.Null(result.CameraModel);
    }

    [Fact]
    public async Task ExtractAsync_NonExistentFile_ReturnsEmptyExifData()
    {
        // Arrange – ExifService swallows all exceptions and returns empty data
        var sut = new ExifService();

        // Act
        var result = await sut.ExtractAsync("/nonexistent/path/that/does_not_exist.jpg");

        // Assert – service must not throw; all fields null
        Assert.Null(result.DateTaken);
        Assert.Null(result.Latitude);
        Assert.Null(result.Longitude);
        Assert.Null(result.CameraModel);
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            try { if (File.Exists(f)) File.Delete(f); } catch { /* ignore */ }
    }
}
