using Microsoft.EntityFrameworkCore;
using Klio.Core.Interfaces;
using Klio.Core.Models;
using Klio.Infrastructure.Data;

namespace Klio.Infrastructure.Repositories;

public class TopicRepository : ITopicRepository
{
    private readonly AppDbContext _context;

    public TopicRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Topic>> GetAllAsync()
    {
        return await _context.Topics
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Topic>> GetByFolderIdAsync(int? folderId)
    {
        return await _context.Topics
            .Where(t => t.FolderId == folderId)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
            .ToListAsync();
    }

    public async Task UpdateSortOrdersAsync(int[] orderedIds)
    {
        var topics = await _context.Topics
            .Where(t => orderedIds.Contains(t.Id))
            .ToListAsync();
        var orderMap = orderedIds.Select((id, idx) => (id, idx)).ToDictionary(x => x.id, x => x.idx);
        foreach (var t in topics)
            if (orderMap.TryGetValue(t.Id, out var idx)) t.SortOrder = idx;
        await _context.SaveChangesAsync();
    }

    public async Task<Topic?> GetByIdAsync(int id)
    {
        return await _context.Topics
            .Include(t => t.Images)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Topic> AddAsync(Topic topic)
    {
        topic.CreatedAt = DateTime.UtcNow;
        _context.Topics.Add(topic);
        await _context.SaveChangesAsync();
        return topic;
    }

    public async Task UpdateAsync(Topic topic)
    {
        _context.Topics.Update(topic);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var topic = await _context.Topics.FindAsync(id);
        if (topic != null)
        {
            _context.Topics.Remove(topic);
            await _context.SaveChangesAsync();
        }
    }
}
