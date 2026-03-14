using Projekt42.Core.Models;

namespace Projekt42.Core.Interfaces;

public interface IAppSettingsRepository
{
    Task<AppSettings> GetAsync();
    Task UpdateAsync(AppSettings settings);
}
