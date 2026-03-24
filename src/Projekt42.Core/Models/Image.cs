namespace Projekt42.Core.Models;

public class Image
{
    public int Id { get; set; }
    public string OriginalFilename { get; set; } = string.Empty;
    public string StoredFilename { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ThumbnailPath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Hash { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public DateTime? DateTaken { get; set; }
    public double? GpsLatitude { get; set; }
    public double? GpsLongitude { get; set; }
    public string? CameraModel { get; set; }
    public bool IsVideo { get; set; }
    public int? TopicId { get; set; }
    public Topic? Topic { get; set; }
    public DateTime ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<ImageTag> Tags { get; set; } = new List<ImageTag>();
}
