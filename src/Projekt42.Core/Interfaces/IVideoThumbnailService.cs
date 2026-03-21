namespace Projekt42.Core.Interfaces;

public interface IVideoThumbnailService
{
    /// <summary>Generates a thumbnail from a video. Returns thumbnail path on success, null on failure.</summary>
    Task<string?> GenerateThumbnailAsync(string videoAbsPath, string thumbnailAbsPath, string storageBasePath);

    /// <summary>Transcodes a video to H.264 MP4. Returns output path on success, null on failure.</summary>
    Task<string?> TranscodeToMp4Async(string inputAbsPath, string outputAbsPath, string storageBasePath);
}
