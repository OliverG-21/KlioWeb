using Klio.Core.Models;

namespace Klio.Core.Interfaces;

public interface IFolderImportService
{
    Task<FolderImportResult> ImportAsync(string folderInboxPath);
    Task ClearFolderInboxAsync(string folderInboxPath);
}
