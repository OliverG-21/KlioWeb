using Klio.Core.Interfaces;

namespace Klio.Infrastructure.Services;

/// <summary>No-op implementation used when tagging is disabled. Never throws.</summary>
public class NullTaggingService : ITaggingService
{
    public Task<IReadOnlyList<string>> TagImageAsync(string imagePath, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    public Task<bool> IsAvailableAsync(CancellationToken ct = default) =>
        Task.FromResult(false);
}
