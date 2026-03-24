using Klio.Core.Models;

namespace Klio.Core.Interfaces;

public interface IAppSettingsRepository
{
    Task<AppSettings> GetAsync();
    Task UpdateAsync(AppSettings settings);
}
