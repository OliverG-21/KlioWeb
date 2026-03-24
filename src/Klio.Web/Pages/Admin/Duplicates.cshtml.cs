using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Klio.Core.Interfaces;
using Klio.Core.Models;

namespace Klio.Web.Pages.Admin;

public class DuplicatesModel : PageModel
{
    private readonly IDuplicateLogRepository _duplicateLogRepository;

    public List<DuplicateLog> DuplicateLogs { get; private set; } = new();
    [TempData] public string? SuccessMessage { get; set; }

    public DuplicatesModel(IDuplicateLogRepository duplicateLogRepository)
    {
        _duplicateLogRepository = duplicateLogRepository;
    }

    public async Task OnGetAsync()
    {
        DuplicateLogs = (await _duplicateLogRepository.GetAllAsync())
            .OrderByDescending(d => d.DetectedAt)
            .ToList();
    }

    public async Task<IActionResult> OnPostClearAllAsync()
    {
        await _duplicateLogRepository.ClearAllAsync();
        SuccessMessage = "Alle Duplikat-Einträge wurden gelöscht.";
        return RedirectToPage();
    }
}
