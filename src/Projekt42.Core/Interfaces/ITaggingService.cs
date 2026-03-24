namespace Projekt42.Core.Interfaces;

public interface ITaggingService
{
    /// <summary>Generates tags for the image at the given path. Returns empty list on failure or when disabled.</summary>
    Task<IReadOnlyList<string>> TagImageAsync(string imagePath, CancellationToken ct = default);

    /// <summary>Returns true when the service is connected and ready.</summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}
