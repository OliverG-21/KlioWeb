using Projekt42.Core.Models;

namespace Projekt42.Core.Interfaces;

public interface ITopicRepository
{
    Task<IEnumerable<Topic>> GetAllAsync();
    Task<IEnumerable<Topic>> GetByFolderIdAsync(int? folderId);
    Task<Topic?> GetByIdAsync(int id);
    Task<Topic> AddAsync(Topic topic);
    Task UpdateAsync(Topic topic);
    Task UpdateSortOrdersAsync(int[] orderedIds);
    Task DeleteAsync(int id);
}
