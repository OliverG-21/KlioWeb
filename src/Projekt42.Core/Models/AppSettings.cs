namespace Projekt42.Core.Models;

public class AppSettings
{
    public int Id { get; set; }
    public string AdminPasswordHash { get; set; } = string.Empty;
    public string StorageBasePath { get; set; } = "/storage";
    public string InboxPath { get; set; } = "/storage/inbox";
    public int ThumbnailWidth { get; set; } = 300;
    public int ThumbnailHeight { get; set; } = 300;
    public string FolderInboxPath { get; set; } = "/storage/folder_inbox";

    // Tagging Add-On
    public bool TaggingEnabled { get; set; } = false;
    public string OllamaUrl { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "llava-phi3";
    public string OllamaPrompt { get; set; } =
        "Liste bis zu 8 kurze beschreibende Tags für dieses Bild auf Deutsch. " +
        "Nutze einfache, allgemeine Begriffe wie Personen, Orte, Objekte, Aktivitäten oder Stimmungen. " +
        "Antworte nur mit einem JSON-Array, keine Erklärung. " +
        "Beispiel: [\"person\",\"draußen\",\"sommer\",\"lachen\"]";
}
