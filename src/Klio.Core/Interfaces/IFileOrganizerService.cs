using Klio.Core.Models;

namespace Klio.Core.Interfaces;

public interface IFileOrganizerService
{
    Task<Image> OrganizeAsync(string incomingFilePath, int? topicId = null);
}
