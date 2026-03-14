using Microsoft.AspNetCore.Mvc.RazorPages;
using Projekt42.Core.Interfaces;
using Projekt42.Core.Models;

namespace Projekt42.Web.Pages.Admin;

public class DuplicatesModel : PageModel
{
    private readonly IDuplicateLogRepository _duplicateLogRepository;

    public List<DuplicateLog> DuplicateLogs { get; private set; } = new();

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
}
