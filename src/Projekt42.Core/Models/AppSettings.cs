namespace Projekt42.Core.Models;

public class AppSettings
{
    public int Id { get; set; }
    public string AdminPasswordHash { get; set; } = string.Empty;
    public string StorageBasePath { get; set; } = "/storage";
    public string InboxPath { get; set; } = "/storage/inbox";
    public int ThumbnailWidth { get; set; } = 300;
    public int ThumbnailHeight { get; set; } = 300;
}
