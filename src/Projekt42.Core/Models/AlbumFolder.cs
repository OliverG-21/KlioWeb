namespace Projekt42.Core.Models;

public class AlbumFolder
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? ParentFolderId { get; set; }
    public AlbumFolder? ParentFolder { get; set; }
    public ICollection<AlbumFolder> SubFolders { get; set; } = new List<AlbumFolder>();
    public ICollection<Topic> Albums { get; set; } = new List<Topic>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
