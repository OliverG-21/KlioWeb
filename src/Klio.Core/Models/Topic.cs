namespace Klio.Core.Models;

public class Topic
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? FolderId { get; set; }
    public int SortOrder { get; set; } = 0;
    public AlbumFolder? Folder { get; set; }
    public int? CoverImageId { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<Image> Images { get; set; } = new List<Image>();
}
