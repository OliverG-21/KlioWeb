namespace Projekt42.Core.Interfaces;

public interface IHashService
{
    Task<string> ComputeAsync(string filePath);
}
