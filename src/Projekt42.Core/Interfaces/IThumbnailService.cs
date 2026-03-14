namespace Projekt42.Core.Interfaces;

public interface IThumbnailService
{
    Task<string?> GenerateAsync(string sourceFilePath, string thumbnailFilePath, int maxWidth, int maxHeight);
}
