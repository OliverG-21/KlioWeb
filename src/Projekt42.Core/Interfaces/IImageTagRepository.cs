using Projekt42.Core.Models;

namespace Projekt42.Core.Interfaces;

public interface IImageTagRepository
{
    Task AddRangeAsync(IEnumerable<ImageTag> tags);
    Task<IReadOnlyList<ImageTag>> GetByImageIdAsync(int imageId);
    Task<Dictionary<int, List<string>>> GetTagMapForImagesAsync(IEnumerable<int> imageIds);
    Task<IReadOnlyList<string>> GetAllDistinctTagsAsync();
    Task<int> GetUntaggedCountAsync();
    Task<int> GetAutoTaggedCountAsync();
    Task<IReadOnlyList<int>> GetUntaggedImageIdsAsync(int limit = 100);
    Task DeleteByIdAsync(int tagId);
    Task DeleteByImageIdAsync(int imageId);
    Task DeleteAllAutoTagsAsync();
}
