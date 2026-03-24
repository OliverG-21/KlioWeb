using Microsoft.EntityFrameworkCore;
using Projekt42.Core.Interfaces;
using Projekt42.Core.Models;
using Projekt42.Infrastructure.Data;

namespace Projekt42.Infrastructure.Repositories;

public class ImageTagRepository : IImageTagRepository
{
    private readonly AppDbContext _context;

    public ImageTagRepository(AppDbContext context) => _context = context;

    public async Task AddRangeAsync(IEnumerable<ImageTag> tags)
    {
        _context.ImageTags.AddRange(tags);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<ImageTag>> GetByImageIdAsync(int imageId) =>
        await _context.ImageTags.Where(t => t.ImageId == imageId).OrderBy(t => t.Tag).ToListAsync();

    public async Task<Dictionary<int, List<string>>> GetTagMapForImagesAsync(IEnumerable<int> imageIds)
    {
        var ids = imageIds.ToList();
        var tags = await _context.ImageTags
            .Where(t => ids.Contains(t.ImageId))
            .Select(t => new { t.ImageId, t.Tag })
            .ToListAsync();
        return tags
            .GroupBy(t => t.ImageId)
            .ToDictionary(g => g.Key, g => g.Select(t => t.Tag).ToList());
    }

    public async Task<IReadOnlyList<string>> GetAllDistinctTagsAsync() =>
        await _context.ImageTags.Select(t => t.Tag).Distinct().OrderBy(t => t).ToListAsync();

    public async Task<Dictionary<string, int>> GetTagsWithCountsAsync() =>
        await _context.ImageTags
            .GroupBy(t => t.Tag)
            .Select(g => new { Tag = g.Key, Count = g.Count() })
            .OrderBy(x => x.Tag)
            .ToDictionaryAsync(x => x.Tag, x => x.Count);

    public async Task<int> GetUntaggedCountAsync() =>
        await _context.Images.CountAsync(i => !_context.ImageTags.Any(t => t.ImageId == i.Id));

    public async Task<int> GetAutoTaggedCountAsync() =>
        await _context.Images.CountAsync(i => _context.ImageTags.Any(t => t.ImageId == i.Id && t.Source == TagSource.Auto));

    public async Task<IReadOnlyList<int>> GetUntaggedImageIdsAsync(int limit = 100) =>
        await _context.Images
            .Where(i => !_context.ImageTags.Any(t => t.ImageId == i.Id))
            .OrderBy(i => i.Id)
            .Take(limit)
            .Select(i => i.Id)
            .ToListAsync();

    public async Task DeleteByIdAsync(int tagId)
    {
        var tag = await _context.ImageTags.FindAsync(tagId);
        if (tag != null)
        {
            _context.ImageTags.Remove(tag);
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteByImageIdAsync(int imageId)
    {
        var tags = await _context.ImageTags.Where(t => t.ImageId == imageId).ToListAsync();
        _context.ImageTags.RemoveRange(tags);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteByTagNameAsync(string tag)
    {
        var tags = await _context.ImageTags.Where(t => t.Tag == tag).ToListAsync();
        _context.ImageTags.RemoveRange(tags);
        await _context.SaveChangesAsync();
    }

    public async Task MergeTagsAsync(string sourceTag, string targetTag)
    {
        // Find all images that have the source tag
        var sourceTags = await _context.ImageTags.Where(t => t.Tag == sourceTag).ToListAsync();
        // Find images that already have the target tag (to avoid duplicates)
        var imageIdsWithTarget = await _context.ImageTags
            .Where(t => t.Tag == targetTag)
            .Select(t => t.ImageId)
            .ToListAsync();

        // For images that don't yet have the target tag: rename source → target
        var toRename = sourceTags.Where(t => !imageIdsWithTarget.Contains(t.ImageId)).ToList();
        foreach (var t in toRename) t.Tag = targetTag;

        // For images that already have target: just remove the source duplicate
        var toDrop = sourceTags.Where(t => imageIdsWithTarget.Contains(t.ImageId)).ToList();
        _context.ImageTags.RemoveRange(toDrop);

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAllAutoTagsAsync()
    {
        var tags = await _context.ImageTags.Where(t => t.Source == TagSource.Auto).ToListAsync();
        _context.ImageTags.RemoveRange(tags);
        await _context.SaveChangesAsync();
    }
}
