using Microsoft.EntityFrameworkCore;
using Projekt42.Core.Interfaces;
using Projekt42.Core.Models;
using Projekt42.Infrastructure.Data;

namespace Projekt42.Infrastructure.Repositories;

public class DuplicateLogRepository : IDuplicateLogRepository
{
    private readonly AppDbContext _context;

    public DuplicateLogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<DuplicateLog>> GetAllAsync()
    {
        return await _context.DuplicateLogs
            .OrderByDescending(d => d.DetectedAt)
            .ToListAsync();
    }

    public async Task<DuplicateLog> AddAsync(DuplicateLog log)
    {
        log.DetectedAt = DateTime.UtcNow;
        _context.DuplicateLogs.Add(log);
        await _context.SaveChangesAsync();
        return log;
    }

    public async Task ClearAllAsync()
    {
        await _context.DuplicateLogs.ExecuteDeleteAsync();
    }
}
