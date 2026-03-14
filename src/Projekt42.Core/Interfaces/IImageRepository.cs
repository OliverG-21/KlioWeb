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
}
