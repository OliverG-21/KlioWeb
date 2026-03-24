using System.Text.Json;
using Microsoft.Extensions.Logging;
using Projekt42.Core.Interfaces;
using Projekt42.Core.Models;

namespace Projekt42.Infrastructure.Services;

public class OllamaTaggingService : ITaggingService
{
    private readonly IAppSettingsRepository _settingsRepo;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OllamaTaggingService> _logger;


    public OllamaTaggingService(
        IAppSettingsRepository settingsRepo,
        IHttpClientFactory httpClientFactory,
        ILogger<OllamaTaggingService> logger)
    {
        _settingsRepo      = settingsRepo;
        _httpClientFactory = httpClientFactory;
        _logger            = logger;
    }

    public async Task<IReadOnlyList<string>> TagImageAsync(string imagePath, CancellationToken ct = default)
    {
        var settings = await _settingsRepo.GetAsync();
        try
        {
            var imageBytes = await File.ReadAllBytesAsync(imagePath, ct);
            var b64 = Convert.ToBase64String(imageBytes);

            var defaults  = new AppSettings();
            var prompt    = string.IsNullOrWhiteSpace(settings.OllamaPrompt)  ? defaults.OllamaPrompt  : settings.OllamaPrompt;
            var model     = string.IsNullOrWhiteSpace(settings.OllamaModel)   ? defaults.OllamaModel   : settings.OllamaModel;
            var ollamaUrl = string.IsNullOrWhiteSpace(settings.OllamaUrl)     ? defaults.OllamaUrl     : settings.OllamaUrl;

            var request = new
            {
                model  = model,
                prompt = prompt,
                images = new[] { b64 },
                stream = false
            };

            using var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(ollamaUrl.TrimEnd('/'));
            client.Timeout     = TimeSpan.FromSeconds(120);

            var body     = JsonSerializer.Serialize(request);
            var content  = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/generate", content, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var responseText = doc.RootElement.GetProperty("response").GetString() ?? "";

            return ParseTags(responseText);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama tagging failed for {Path}", imagePath);
            return Array.Empty<string>();
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        var settings  = await _settingsRepo.GetAsync();
        var defaults  = new AppSettings();
        var ollamaUrl = string.IsNullOrWhiteSpace(settings.OllamaUrl) ? defaults.OllamaUrl : settings.OllamaUrl;
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(ollamaUrl.TrimEnd('/'));
            client.Timeout     = TimeSpan.FromSeconds(5);
            var response = await client.GetAsync("/api/tags", ct);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private static IReadOnlyList<string> ParseTags(string responseText)
    {
        var text  = responseText.Trim();
        var start = text.IndexOf('[');
        var end   = text.LastIndexOf(']');
        if (start < 0 || end <= start) return Array.Empty<string>();
        try
        {
            var arrayJson = text[start..(end + 1)];
            var tags = JsonSerializer.Deserialize<List<string>>(arrayJson);
            return tags?
                .Select(t => t.Trim().ToLowerInvariant())
                .Where(t => t.Length > 0)
                .Take(8)
                .ToList() ?? [];
        }
        catch { return Array.Empty<string>(); }
    }
}
