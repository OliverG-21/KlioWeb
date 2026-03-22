using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Projekt42.Core.Interfaces;

namespace Projekt42.Worker;

public class FileWatcherService : BackgroundService
{
    private readonly IProcessingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FileWatcherService> _logger;
    private FileSystemWatcher? _watcher;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".heic", ".heif",
        ".mp4", ".webm", ".mov", ".m4v", ".avi", ".mkv", ".ogv"
    };

    public FileWatcherService(
        IProcessingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<FileWatcherService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string inboxPath;
        using (var scope = _scopeFactory.CreateScope())
        {
            var appSettingsRepo = scope.ServiceProvider.GetRequiredService<IAppSettingsRepository>();
            var settings = await appSettingsRepo.GetAsync();
            inboxPath = settings.InboxPath;
        }

        if (!Directory.Exists(inboxPath))
        {
            Directory.CreateDirectory(inboxPath);
            _logger.LogInformation("Created inbox directory: {InboxPath}", inboxPath);
        }

        // Enqueue any files already sitting in inbox
        foreach (var file in Directory.GetFiles(inboxPath))
        {
            if (SupportedExtensions.Contains(Path.GetExtension(file)))
            {
                _logger.LogInformation("Enqueueing existing file from inbox: {File}", file);
                _queue.Enqueue(file, null);
            }
        }

        _watcher = new FileSystemWatcher(inboxPath)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            Filter = "*.*",
            EnableRaisingEvents = true
        };

        _watcher.Created += OnFileCreated;
        _watcher.Renamed += OnFileRenamed;

        _logger.LogInformation("FileWatcherService started, watching: {InboxPath}", inboxPath);

        await Task.Delay(Timeout.Infinite, stoppingToken);

        _watcher.EnableRaisingEvents = false;
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        if (!SupportedExtensions.Contains(Path.GetExtension(e.FullPath)))
            return;

        _logger.LogInformation("New file detected: {FilePath}", e.FullPath);
        // Small delay to ensure file is fully written before processing
        Task.Delay(500).ContinueWith(_ => _queue.Enqueue(e.FullPath, null));
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (!SupportedExtensions.Contains(Path.GetExtension(e.FullPath)))
            return;

        _logger.LogInformation("File renamed/moved into inbox: {FilePath}", e.FullPath);
        Task.Delay(500).ContinueWith(_ => _queue.Enqueue(e.FullPath, null));
    }

    public override void Dispose()
    {
        _watcher?.Dispose();
        base.Dispose();
    }
}
