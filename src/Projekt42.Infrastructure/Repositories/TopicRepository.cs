using Microsoft.EntityFrameworkCore;
using Projekt42.Core.Interfaces;
using Projekt42.Core.Models;
using Projekt42.Infrastructure.Data;

namespace Projekt42.Infrastructure.Repositories;

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
