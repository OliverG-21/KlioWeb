using Microsoft.AspNetCore.Mvc.RazorPages;
using Klio.Core.Interfaces;
using System.Text.Json;

namespace Klio.Web.Pages;

public class MapModel : PageModel
{
    private readonly IImageRepository _imageRepository;

    public MapModel(IImageRepository imageRepository)
    {
        _imageRepository = imageRepository;
    }

    public bool HasGpsImages { get; private set; }
    public bool HasReturnPosition { get; private set; }
    public double CenterLat { get; private set; } = 48.1351;
    public double CenterLon { get; private set; } = 11.5820;
    public int Zoom { get; private set; } = 5;
    public string MarkersJson { get; private set; } = "[]";

    public async Task OnGetAsync(double? lat, double? lon, int? zoom)
    {
        var images = (await _imageRepository.GetWithGpsAsync()).ToList();
        HasGpsImages = images.Any();

        if (lat.HasValue && lon.HasValue)
        {
            HasReturnPosition = true;
            CenterLat = lat.Value;
            CenterLon = lon.Value;
            Zoom = zoom ?? 13;
        }
        else if (HasGpsImages)
        {
            CenterLat = images[0].GpsLatitude!.Value;
            CenterLon = images[0].GpsLongitude!.Value;
            Zoom = 10;
        }

        // Group images at the same GPS position (rounded to 4 decimal places ≈ 11m)
        var markers = images
            .GroupBy(img => (
                Math.Round(img.GpsLatitude!.Value, 4),
                Math.Round(img.GpsLongitude!.Value, 4)))
            .Select(g =>
            {
                var first = g.OrderByDescending(i => i.DateTaken ?? i.CreatedAt).First();
                return new
                {
                    lat = first.GpsLatitude!.Value,
                    lon = first.GpsLongitude!.Value,
                    date = (first.DateTaken ?? first.CreatedAt).ToString("dd.MM.yyyy"),
                    hasThumbnail = !string.IsNullOrEmpty(first.ThumbnailPath),
                    id = first.Id,
                    topicId = first.TopicId ?? 0,
                    count = g.Count()
                };
            }).ToList();

        MarkersJson = JsonSerializer.Serialize(markers);
    }
}
