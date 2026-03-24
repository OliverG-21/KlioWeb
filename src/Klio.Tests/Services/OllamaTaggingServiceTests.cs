using System.Net;
using System.Text;
using System.Text.Json;
using Klio.Core.Interfaces;
using Klio.Core.Models;
using Klio.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Klio.Tests.Services;

/// <summary>
/// Tests for OllamaTaggingService focusing on the ParseTags logic, exercised
/// through TagImageAsync with a stubbed HTTP handler.
/// </summary>
public class OllamaTaggingServiceTests : IDisposable
{
    // A real image file is required because TagImageAsync reads the file before calling HTTP.
    private readonly string _tempImagePath;

    public OllamaTaggingServiceTests()
    {
        _tempImagePath = Path.GetTempFileName();
        // Write a minimal valid 1x1 JPEG so File.ReadAllBytesAsync succeeds.
        // These are the raw bytes of the smallest possible JPEG.
        byte[] minimalJpeg =
        [
            0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01,
            0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0xFF, 0xDB, 0x00, 0x43,
            0x00, 0x08, 0x06, 0x06, 0x07, 0x06, 0x05, 0x08, 0x07, 0x07, 0x07, 0x09,
            0x09, 0x08, 0x0A, 0x0C, 0x14, 0x0D, 0x0C, 0x0B, 0x0B, 0x0C, 0x19, 0x12,
            0x13, 0x0F, 0x14, 0x1D, 0x1A, 0x1F, 0x1E, 0x1D, 0x1A, 0x1C, 0x1C, 0x20,
            0x24, 0x2E, 0x27, 0x20, 0x22, 0x2C, 0x23, 0x1C, 0x1C, 0x28, 0x37, 0x29,
            0x2C, 0x30, 0x31, 0x34, 0x34, 0x34, 0x1F, 0x27, 0x39, 0x3D, 0x38, 0x32,
            0x3C, 0x2E, 0x33, 0x34, 0x32, 0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x01,
            0x00, 0x01, 0x01, 0x01, 0x11, 0x00, 0xFF, 0xC4, 0x00, 0x1F, 0x00, 0x00,
            0x01, 0x05, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
            0x09, 0x0A, 0x0B, 0xFF, 0xC4, 0x00, 0xB5, 0x10, 0x00, 0x02, 0x01, 0x03,
            0x03, 0x02, 0x04, 0x03, 0x05, 0x05, 0x04, 0x04, 0x00, 0x00, 0x01, 0x7D,
            0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12, 0x21, 0x31, 0x41, 0x06,
            0x13, 0x51, 0x61, 0x07, 0x22, 0x71, 0x14, 0x32, 0x81, 0x91, 0xA1, 0x08,
            0x23, 0x42, 0xB1, 0xC1, 0x15, 0x52, 0xD1, 0xF0, 0x24, 0x33, 0x62, 0x72,
            0x82, 0x09, 0x0A, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x25, 0x26, 0x27, 0x28,
            0x29, 0x2A, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x43, 0x44, 0x45,
            0x46, 0x47, 0x48, 0x49, 0x4A, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59,
            0x5A, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6A, 0x73, 0x74, 0x75,
            0x76, 0x77, 0x78, 0x79, 0x7A, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89,
            0x8A, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9A, 0xA2, 0xA3,
            0xA4, 0xA5, 0xA6, 0xA7, 0xA8, 0xA9, 0xAA, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6,
            0xB7, 0xB8, 0xB9, 0xBA, 0xC2, 0xC3, 0xC4, 0xC5, 0xC6, 0xC7, 0xC8, 0xC9,
            0xCA, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA, 0xE1, 0xE2,
            0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xF1, 0xF2, 0xF3, 0xF4,
            0xF5, 0xF6, 0xF7, 0xF8, 0xF9, 0xFA, 0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01,
            0x00, 0x00, 0x3F, 0x00, 0xFB, 0xD7, 0xFF, 0xD9
        ];
        File.WriteAllBytes(_tempImagePath, minimalJpeg);
    }

    private static OllamaTaggingService BuildService(string ollamaResponseJson)
    {
        var settingsRepo = Substitute.For<IAppSettingsRepository>();
        settingsRepo.GetAsync().Returns(new AppSettings
        {
            OllamaUrl   = "http://localhost:11434",
            OllamaModel = "llava-phi3",
            OllamaPrompt = "test prompt"
        });

        var handler = new FakeHttpMessageHandler(ollamaResponseJson, HttpStatusCode.OK);
        var httpClient = new HttpClient(handler);

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var logger = NullLogger<OllamaTaggingService>.Instance;
        return new OllamaTaggingService(settingsRepo, factory, logger);
    }

    [Fact]
    public async Task TagImageAsync_ValidJsonArrayResponse_ReturnsExpectedTags()
    {
        // Arrange – Ollama returns a clean JSON array in the "response" field
        var ollamaJson = JsonSerializer.Serialize(new { response = """["hund","draußen","sommer"]""" });
        var sut = BuildService(ollamaJson);

        // Act
        var tags = await sut.TagImageAsync(_tempImagePath);

        // Assert
        Assert.Equal(3, tags.Count);
        Assert.Contains("hund", tags);
        Assert.Contains("draußen", tags);
        Assert.Contains("sommer", tags);
    }

    [Fact]
    public async Task TagImageAsync_ResponseWithTextAroundJson_StillExtractsTags()
    {
        // Arrange – Ollama prefixes the array with a sentence (common LLM behavior)
        var responseText = """Here are the tags: ["tag1","tag2"] — hope that helps!""";
        var ollamaJson = JsonSerializer.Serialize(new { response = responseText });
        var sut = BuildService(ollamaJson);

        // Act
        var tags = await sut.TagImageAsync(_tempImagePath);

        // Assert
        Assert.Equal(2, tags.Count);
        Assert.Contains("tag1", tags);
        Assert.Contains("tag2", tags);
    }

    [Fact]
    public async Task TagImageAsync_InvalidJson_ReturnsEmptyList()
    {
        // Arrange – Ollama returns plain text with no JSON array at all
        var ollamaJson = JsonSerializer.Serialize(new { response = "I cannot tag this image." });
        var sut = BuildService(ollamaJson);

        // Act
        var tags = await sut.TagImageAsync(_tempImagePath);

        // Assert
        Assert.Empty(tags);
    }

    [Fact]
    public async Task TagImageAsync_TagsAreNormalizedToLowercase()
    {
        // Arrange
        var ollamaJson = JsonSerializer.Serialize(new { response = """["HUND","Draußen","SOMMER"]""" });
        var sut = BuildService(ollamaJson);

        // Act
        var tags = await sut.TagImageAsync(_tempImagePath);

        // Assert
        Assert.All(tags, t => Assert.Equal(t, t.ToLowerInvariant()));
    }

    [Fact]
    public async Task TagImageAsync_MoreThan8Tags_ReturnsAtMost8()
    {
        // Arrange – Ollama returns 10 tags; ParseTags caps at 8
        var manyTags = Enumerable.Range(1, 10).Select(i => $"tag{i}").ToList();
        var ollamaJson = JsonSerializer.Serialize(new { response = JsonSerializer.Serialize(manyTags) });
        var sut = BuildService(ollamaJson);

        // Act
        var tags = await sut.TagImageAsync(_tempImagePath);

        // Assert
        Assert.True(tags.Count <= 8, $"Expected at most 8 tags but got {tags.Count}");
    }

    public void Dispose()
    {
        if (File.Exists(_tempImagePath)) File.Delete(_tempImagePath);
    }
}

/// <summary>
/// Minimal HttpMessageHandler stub that returns a fixed response body.
/// </summary>
internal class FakeHttpMessageHandler(string responseBody, HttpStatusCode statusCode) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}
