namespace Projekt42.Core.Models;

public class Topic
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public ICollection<Image> Images { get; set; } = new List<Image>();
}
