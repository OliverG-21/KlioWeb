using Projekt42.Core.Models;

namespace Projekt42.Core.Interfaces;

public interface IFolderImportService
{
    Task<FolderImportResult> ImportAsync(string folderInboxPath);
    Task ClearFolderInboxAsync(string folderInboxPath);
}
