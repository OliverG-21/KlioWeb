using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Klio.Core.Interfaces;

namespace Klio.Infrastructure.Services;

public class ExifService : IExifService
{
    public Task<ExifData> ExtractAsync(string filePath)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(filePath);

            DateTime? dateTaken = null;
            double? latitude = null;
            double? longitude = null;
            string? cameraModel = null;

            var exifSubIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (exifSubIfd != null)
            {
                if (exifSubIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var dt))
                {
                    dateTaken = dt;
                }
                else if (exifSubIfd.TryGetDateTime(ExifDirectoryBase.TagDateTime, out var dt2))
                {
                    dateTaken = dt2;
                }
            }

            if (dateTaken == null)
            {
                var exifIfd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
                if (exifIfd0 != null && exifIfd0.TryGetDateTime(ExifDirectoryBase.TagDateTime, out var dt3))
                {
                    dateTaken = dt3;
                }
            }

            var gpsDirectory = directories.OfType<GpsDirectory>().FirstOrDefault();
            if (gpsDirectory != null)
            {
                var geoLocation = gpsDirectory.GetGeoLocation();
                if (geoLocation != null && !geoLocation.IsZero)
                {
                    latitude = geoLocation.Latitude;
                    longitude = geoLocation.Longitude;
                }
            }

            var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            if (ifd0 != null)
            {
                var make  = ifd0.GetString(ExifDirectoryBase.TagMake)?.Trim();
                var model = ifd0.GetString(ExifDirectoryBase.TagModel)?.Trim();
                if (!string.IsNullOrEmpty(model))
                {
                    // Avoid duplicating make if it's already part of the model string
                    cameraModel = (!string.IsNullOrEmpty(make) && !model.StartsWith(make, StringComparison.OrdinalIgnoreCase))
                        ? $"{make} {model}"
                        : model;
                }
            }

            return Task.FromResult(new ExifData(dateTaken, latitude, longitude, cameraModel));
        }
        catch
        {
            return Task.FromResult(new ExifData(null, null, null, null));
        }
    }
}
