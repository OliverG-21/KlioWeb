namespace Klio.Core.Models;

public class ImageTag
{
    public int Id { get; set; }
    public int ImageId { get; set; }
    public Image Image { get; set; } = null!;
    public string Tag { get; set; } = string.Empty;
    public float Confidence { get; set; } = 1f;
    public TagSource Source { get; set; } = TagSource.Auto;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum TagSource { Auto, Manual }
