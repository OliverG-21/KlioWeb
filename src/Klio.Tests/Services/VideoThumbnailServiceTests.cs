using System.Net;
using System.Text;
using Klio.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Klio.Tests.Services;

[Trait("Category", "Unit")]
public class VideoThumbnailServiceTests
{
    private const string StorageBase = "/storage/base";

    private static VideoThumbnailService BuildService(HttpStatusCode statusCode, string body = "{}")
    {
        var handler = new FakeHttpMessageHandler(body, statusCode);
        var httpClient = new HttpClient(handler);

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FfmpegService:Url"] = "http://fake-ffmpeg:5100"
            })
            .Build();

        var logger = NullLogger<VideoThumbnailService>.Instance;
        return new VideoThumbnailService(factory, logger, config);
    }

    [Fact]
    public async Task GenerateThumbnailAsync_SuccessResponse_ReturnsThumbnailPath()
    {
        // Arrange
        var sut = BuildService(HttpStatusCode.OK);
        var videoPath = Path.Combine(StorageBase, "videos", "test.mp4");
        var thumbPath = Path.Combine(StorageBase, "thumbnails", "test_thumb.jpg");

        // Act
        var result = await sut.GenerateThumbnailAsync(videoPath, thumbPath, StorageBase);

        // Assert
        Assert.Equal(thumbPath, result);
    }

    [Fact]
    public async Task GenerateThumbnailAsync_HttpError_ReturnsNull()
    {
        // Arrange
        var sut = BuildService(HttpStatusCode.InternalServerError, "ffmpeg failed");
        var videoPath = Path.Combine(StorageBase, "videos", "test.mp4");
        var thumbPath = Path.Combine(StorageBase, "thumbnails", "test_thumb.jpg");

        // Act
        var result = await sut.GenerateThumbnailAsync(videoPath, thumbPath, StorageBase);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task TranscodeToMp4Async_SuccessResponse_ReturnsOutputPath()
    {
        // Arrange
        var sut = BuildService(HttpStatusCode.OK);
        var inputPath  = Path.Combine(StorageBase, "videos", "input.mov");
        var outputPath = Path.Combine(StorageBase, "videos", "output.mp4");

        // Act
        var result = await sut.TranscodeToMp4Async(inputPath, outputPath, StorageBase);

        // Assert
        Assert.Equal(outputPath, result);
    }

    [Fact]
    public async Task TranscodeToMp4Async_HttpError_ReturnsNull()
    {
        // Arrange
        var sut = BuildService(HttpStatusCode.BadGateway);
        var inputPath  = Path.Combine(StorageBase, "videos", "input.mov");
        var outputPath = Path.Combine(StorageBase, "videos", "output.mp4");

        // Act
        var result = await sut.TranscodeToMp4Async(inputPath, outputPath, StorageBase);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ConvertToJpegAsync_ConvertsStoragePath_ToContainerPath()
    {
        // Arrange – capture the request body to verify the container path conversion
        string? capturedRequestBody = null;
        var handler = new CapturingFakeHttpMessageHandler(HttpStatusCode.OK, "{}", body => capturedRequestBody = body);
        var httpClient = new HttpClient(handler);

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FfmpegService:Url"] = "http://fake-ffmpeg:5100"
            })
            .Build();

        var sut = new VideoThumbnailService(factory, NullLogger<VideoThumbnailService>.Instance, config);

        var storageBase = "/mnt/data";
        var inputPath   = "/mnt/data/originals/photo.heic";
        var outputPath  = "/mnt/data/thumbnails/photo.jpg";

        // Act
        var result = await sut.ConvertToJpegAsync(inputPath, outputPath, storageBase);

        // Assert – container paths must start with /storage/...
        Assert.Equal(outputPath, result);
        Assert.NotNull(capturedRequestBody);
        Assert.Contains("/storage/originals/photo.heic",  capturedRequestBody);
        Assert.Contains("/storage/thumbnails/photo.jpg",  capturedRequestBody);
        // Original host path must NOT appear in the request
        Assert.DoesNotContain("/mnt/data", capturedRequestBody);
    }
}

/// <summary>
/// Fake handler that captures the request body in addition to returning a fixed response.
/// </summary>
internal sealed class CapturingFakeHttpMessageHandler(
    HttpStatusCode statusCode,
    string responseBody,
    Action<string> onRequestBody) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content != null)
        {
            var body = await request.Content.ReadAsStringAsync(cancellationToken);
            onRequestBody(body);
        }

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        };
    }
}
