using Projekt42.Core.Models;

namespace Projekt42.Core.Interfaces;

public interface IImageRepository
{
    Task<IEnumerable<Image>> GetAllAsync();
    Task<Image?> GetByIdAsync(int id);
    Task<IEnumerable<Image>> GetByTopicIdAsync(int topicId);
    Task<IEnumerable<Image>> GetWithGpsAsync();
    Task<Image> AddAsync(Image image);
    Task UpdateAsync(Image image);
    Task DeleteAsync(int id);
    Task<bool> ExistsByHashAsync(string hash);
    Task<Image?> GetByHashAsync(string hash);
    Task<Dictionary<int, int>> GetCountsByTopicAsync();
    Task<int> GetTotalCountAsync();
    Task<IEnumerable<Image>> GetPagedAsync(int skip, int take);
    Task<Dictionary<int, int>> GetFirstImageIdPerTopicAsync();
}
