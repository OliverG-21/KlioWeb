using Microsoft.AspNetCore.Mvc.RazorPages;
using Projekt42.Core.Interfaces;
using System.Text.Json;

namespace Projekt42.Web.Pages;

public class MapModel : PageModel
{
    private readonly IImageRepository _imageRepository;

    public MapModel(IImageRepository imageRepository)
    {
        _imageRepository = imageRepository;
    }

    public bool HasGpsImages { get; private set; }
    public double CenterLat { get; private set; } = 48.1351;
    public double CenterLon { get; private set; } = 11.5820;
    public int Zoom { get; private set; } = 5;
    public string MarkersJson { get; private set; } = "[]";

    public async Task OnGetAsync()
    {
        var images = (await _imageRepository.GetWithGpsAsync()).ToList();
        HasGpsImages = images.Any();

        if (HasGpsImages)
        {
            CenterLat = images[0].GpsLatitude!.Value;
            CenterLon = images[0].GpsLongitude!.Value;
            Zoom = 10;
        }

        var markers = images.Select(img => new
        {
            lat = img.GpsLatitude!.Value,
            lon = img.GpsLongitude!.Value,
            title = img.DateTaken.HasValue
                ? img.DateTaken.Value.ToString("dd.MM.yyyy HH:mm")
                : img.OriginalFilename,
            hasThumbnail = !string.IsNullOrEmpty(img.ThumbnailPath),
            id = img.Id
        }).ToList();

        MarkersJson = JsonSerializer.Serialize(markers);
    }
}
