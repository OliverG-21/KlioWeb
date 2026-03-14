using Microsoft.EntityFrameworkCore;
using Projekt42.Core.Interfaces;
using Projekt42.Core.Models;
using Projekt42.Infrastructure.Data;

namespace Projekt42.Infrastructure.Repositories;

public class ImageRepository : IImageRepository
{
    private readonly AppDbContext _context;

    public ImageRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Image>> GetAllAsync()
    {
        return await _context.Images
            .Include(i => i.Topic)
            .OrderByDescending(i => i.DateTaken ?? i.CreatedAt)
            .ToListAsync();
    }

    public async Task<Image?> GetByIdAsync(int id)
    {
        return await _context.Images
            .Include(i => i.Topic)
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<IEnumerable<Image>> GetByTopicIdAsync(int topicId)
    {
        return await _context.Images
            .Include(i => i.Topic)
            .Where(i => i.TopicId == topicId)
            .OrderByDescending(i => i.DateTaken ?? i.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Image>> GetWithGpsAsync()
    {
        return await _context.Images
            .Where(i => i.GpsLatitude != null && i.GpsLongitude != null)
            .ToListAsync();
    }

    public async Task<Image> AddAsync(Image image)
    {
        image.CreatedAt = DateTime.UtcNow;
        _context.Images.Add(image);
        await _context.SaveChangesAsync();
        return image;
    }

    public async Task UpdateAsync(Image image)
    {
        _context.Images.Update(image);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var image = await _context.Images.FindAsync(id);
        if (image != null)
        {
            _context.Images.Remove(image);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsByHashAsync(string hash)
    {
        return await _context.Images.AnyAsync(i => i.Hash == hash);
    }

    public async Task<Image?> GetByHashAsync(string hash)
    {
        return await _context.Images.FirstOrDefaultAsync(i => i.Hash == hash);
    }
}
