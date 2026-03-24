using Klio.Core.Models;

namespace Klio.Core.Interfaces;

public interface IDuplicateLogRepository
{
    Task<IEnumerable<DuplicateLog>> GetAllAsync();
    Task<DuplicateLog> AddAsync(DuplicateLog log);
    Task ClearAllAsync();
}
