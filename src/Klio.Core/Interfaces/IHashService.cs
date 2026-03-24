namespace Klio.Core.Interfaces;

public interface IHashService
{
    Task<string> ComputeAsync(string filePath);
}
