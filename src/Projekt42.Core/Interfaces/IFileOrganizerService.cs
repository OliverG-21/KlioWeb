using Projekt42.Core.Models;

namespace Projekt42.Core.Interfaces;

public interface IFileOrganizerService
{
    Task<Image> OrganizeAsync(string incomingFilePath, int? topicId = null);
}
