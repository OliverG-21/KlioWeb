using System.Security.Cryptography;
using Klio.Core.Interfaces;

namespace Klio.Infrastructure.Services;

public class HashService : IHashService
{
    public async Task<string> ComputeAsync(string filePath)
    {
        using var sha256 = SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var hashBytes = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
