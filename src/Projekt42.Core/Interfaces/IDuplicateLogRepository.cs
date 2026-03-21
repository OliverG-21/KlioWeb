using Projekt42.Core.Models;

namespace Projekt42.Core.Interfaces;

public interface IDuplicateLogRepository
{
    Task<IEnumerable<DuplicateLog>> GetAllAsync();
    Task<DuplicateLog> AddAsync(DuplicateLog log);
    Task ClearAllAsync();
}
