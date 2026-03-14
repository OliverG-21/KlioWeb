namespace Projekt42.Core.Models;

public class DuplicateLog
{
    public int Id { get; set; }
    public string IncomingFilename { get; set; } = string.Empty;
    public string DuplicatePath { get; set; } = string.Empty;
    public int ExistingImageId { get; set; }
    public string ExistingImagePath { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
}
