using Microsoft.EntityFrameworkCore;
using Klio.Core.Interfaces;
using Klio.Core.Models;
using Klio.Infrastructure.Data;

namespace Klio.Infrastructure.Repositories;

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
