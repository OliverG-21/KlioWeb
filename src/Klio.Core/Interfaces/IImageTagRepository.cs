using Klio.Core.Models;

namespace Klio.Core.Interfaces;

public interface IImageTagRepository
{
    Task AddRangeAsync(IEnumerable<ImageTag> tags);
    Task<IReadOnlyList<ImageTag>> GetByImageIdAsync(int imageId);
    Task<Dictionary<int, List<string>>> GetTagMapForImagesAsync(IEnumerable<int> imageIds);
    Task<IReadOnlyList<string>> GetAllDistinctTagsAsync();
    Task<Dictionary<string, int>> GetTagsWithCountsAsync();
    Task<int> GetUntaggedCountAsync();
    Task<int> GetAutoTaggedCountAsync();
    Task<IReadOnlyList<int>> GetUntaggedImageIdsAsync(int limit = 100);
    Task DeleteByIdAsync(int tagId);
    Task DeleteByImageIdAsync(int imageId);
    Task DeleteByTagNameAsync(string tag);
    Task MergeTagsAsync(string sourceTag, string targetTag);
    Task DeleteAllAutoTagsAsync();
    Task<int> DeleteAllAsync();
    Task<int> AddTagToTopicAsync(int topicId, string tag);
}
