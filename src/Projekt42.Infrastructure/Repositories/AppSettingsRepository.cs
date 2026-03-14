using Microsoft.EntityFrameworkCore;
using Projekt42.Core.Interfaces;
using Projekt42.Core.Models;
using Projekt42.Infrastructure.Data;

namespace Projekt42.Infrastructure.Repositories;

public class AppSettingsRepository : IAppSettingsRepository
{
    private readonly AppDbContext _context;

    public AppSettingsRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AppSettings> GetAsync()
    {
        var settings = await _context.AppSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new AppSettings
            {
                AdminPasswordHash = BCrypt.Net.BCrypt.HashPassword("admin"),
                StorageBasePath = "/tmp/projekt42",
                InboxPath = "/tmp/projekt42/inbox",
                ThumbnailWidth = 300,
                ThumbnailHeight = 300
            };
            _context.AppSettings.Add(settings);
            await _context.SaveChangesAsync();
        }
        return settings;
    }

    public async Task UpdateAsync(AppSettings settings)
    {
        _context.AppSettings.Update(settings);
        await _context.SaveChangesAsync();
    }
}
