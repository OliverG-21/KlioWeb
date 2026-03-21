using Microsoft.EntityFrameworkCore;
using Projekt42.Core.Interfaces;
using Projekt42.Core.Models;
using Projekt42.Infrastructure.Data;

namespace Projekt42.Infrastructure.Repositories;

public class AlbumFolderRepository : IAlbumFolderRepository
{
    private readonly AppDbContext _db;

    public AlbumFolderRepository(AppDbContext db) => _db = db;

    public async Task<IEnumerable<AlbumFolder>> GetAllAsync()
        => await _db.AlbumFolders.OrderBy(f => f.Name).ToListAsync();

    public async Task<IEnumerable<AlbumFolder>> GetByParentIdAsync(int? parentId)
        => await _db.AlbumFolders
            .Where(f => f.ParentFolderId == parentId)
            .OrderBy(f => f.Name)
            .ToListAsync();

    public async Task<AlbumFolder?> GetByIdAsync(int id)
        => await _db.AlbumFolders
            .Include(f => f.SubFolders)
            .Include(f => f.Albums)
            .FirstOrDefaultAsync(f => f.Id == id);

    public async Task AddAsync(AlbumFolder folder)
    {
        _db.AlbumFolders.Add(folder);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(AlbumFolder folder)
    {
        _db.AlbumFolders.Update(folder);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateSortOrdersAsync(int[] orderedIds)
    {
        var folders = await _db.AlbumFolders
            .Where(f => orderedIds.Contains(f.Id))
            .ToListAsync();
        var orderMap = orderedIds.Select((id, idx) => (id, idx)).ToDictionary(x => x.id, x => x.idx);
        foreach (var f in folders)
            if (orderMap.TryGetValue(f.Id, out var idx)) f.SortOrder = idx;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var folder = await _db.AlbumFolders.FindAsync(id);
        if (folder != null)
        {
            _db.AlbumFolders.Remove(folder);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<bool> HasSubFoldersAsync(int? id)
        => await _db.AlbumFolders.AnyAsync(f => f.ParentFolderId == id);

    public async Task<bool> HasAlbumsAsync(int? id)
        => await _db.Topics.AnyAsync(t => t.FolderId == id);

    /// <summary>Returns ancestors from root to immediate parent (for breadcrumb).</summary>
    public async Task<List<AlbumFolder>> GetAncestorsAsync(int folderId)
    {
        var ancestors = new List<AlbumFolder>();
        var current = await _db.AlbumFolders.FindAsync(folderId);
        while (current?.ParentFolderId != null)
        {
            current = await _db.AlbumFolders.FindAsync(current.ParentFolderId);
            if (current != null) ancestors.Insert(0, current);
        }
        return ancestors;
    }
}
