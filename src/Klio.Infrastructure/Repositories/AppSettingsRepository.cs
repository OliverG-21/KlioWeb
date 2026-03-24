using Microsoft.EntityFrameworkCore;
using Klio.Core.Interfaces;
using Klio.Core.Models;
using Klio.Infrastructure.Data;

namespace Klio.Infrastructure.Repositories;

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
                StorageBasePath = "/Users/oliver/Documents/Klio-Data",
                InboxPath = "/Users/oliver/Documents/Klio-Data/inbox",
                FolderInboxPath = "/Users/oliver/Documents/Klio-Data/folder_inbox",
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
