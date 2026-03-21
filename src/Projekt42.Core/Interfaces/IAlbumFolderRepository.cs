using Projekt42.Core.Models;

namespace Projekt42.Core.Interfaces;

public interface IAlbumFolderRepository
{
    Task<IEnumerable<AlbumFolder>> GetAllAsync();
    Task<IEnumerable<AlbumFolder>> GetByParentIdAsync(int? parentId);
    Task<AlbumFolder?> GetByIdAsync(int id);
    Task AddAsync(AlbumFolder folder);
    Task UpdateAsync(AlbumFolder folder);
    Task UpdateSortOrdersAsync(int[] orderedIds);
    Task DeleteAsync(int id);
    Task<bool> HasSubFoldersAsync(int? id);
    Task<bool> HasAlbumsAsync(int? id);
    Task<List<AlbumFolder>> GetAncestorsAsync(int folderId);
}
