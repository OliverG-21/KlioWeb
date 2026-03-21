using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Projekt42.Core.Interfaces;

namespace Projekt42.Infrastructure.Services;

public class VideoThumbnailService : IVideoThumbnailService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(90) };
    private readonly ILogger<VideoThumbnailService> _logger;
    private readonly string _serviceUrl;

    public VideoThumbnailService(ILogger<VideoThumbnailService> logger, IConfiguration config)
    {
        _logger = logger;
        _serviceUrl = config["FfmpegService:Url"] ?? "http://localhost:5100";
    }

    public async Task<string?> GenerateThumbnailAsync(string videoAbsPath, string thumbnailAbsPath, string storageBasePath)
    {
        try
        {
            var sep = storageBasePath.TrimEnd('/', '\\');
            var containerVideo = "/storage" + videoAbsPath[sep.Length..].Replace('\\', '/');
            var containerThumb = "/storage" + thumbnailAbsPath[sep.Length..].Replace('\\', '/');

            var response = await _http.PostAsJsonAsync(_serviceUrl + "/thumbnail", new
            {
                input = containerVideo,
                output = containerThumb,
                timestamp = "00:00:01"
            });

            if (response.IsSuccessStatusCode)
                return thumbnailAbsPath;

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("ffmpeg-service {Status}: {Body}", response.StatusCode, body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Video thumbnail generation failed for {Path}", videoAbsPath);
        }
        return null;
    }

    public async Task<string?> TranscodeToMp4Async(string inputAbsPath, string outputAbsPath, string storageBasePath)
    {
        try
        {
            var sep = storageBasePath.TrimEnd('/', '\\');
            var containerInput  = "/storage" + inputAbsPath[sep.Length..].Replace('\\', '/');
            var containerOutput = "/storage" + outputAbsPath[sep.Length..].Replace('\\', '/');

            var response = await _http.PostAsJsonAsync(_serviceUrl + "/transcode", new
            {
                input  = containerInput,
                output = containerOutput
            });

            if (response.IsSuccessStatusCode)
                return outputAbsPath;

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("ffmpeg-service transcode {Status}: {Body}", response.StatusCode, body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Video transcoding failed for {Path}", inputAbsPath);
        }
        return null;
    }
}
