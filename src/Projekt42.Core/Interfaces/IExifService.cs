namespace Projekt42.Core.Interfaces;

public record ExifData(DateTime? DateTaken, double? Latitude, double? Longitude, string? CameraModel);

public interface IExifService
{
    Task<ExifData> ExtractAsync(string filePath);
}
