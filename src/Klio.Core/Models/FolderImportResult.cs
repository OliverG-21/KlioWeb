namespace Klio.Core.Models;

public record FolderImportResult(
    int FoldersCreated,
    int AlbumsCreated,
    int AlbumsReused,
    int FilesEnqueued,
    List<string> Errors
);
