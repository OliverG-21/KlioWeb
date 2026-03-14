namespace Projekt42.Core.Interfaces;

public record ExifData(DateTime? DateTaken, double? Latitude, double? Longitude);

public interface IExifService
{
    Task<ExifData> ExtractAsync(string filePath);
}
