using Microsoft.AspNetCore.Mvc.RazorPages;
using Projekt42.Core.Interfaces;
using Projekt42.Core.Models;

namespace Projekt42.Web.Pages;

public class IndexModel : PageModel
{
    private readonly IImageRepository _imageRepository;

    public IndexModel(IImageRepository imageRepository)
    {
        _imageRepository = imageRepository;
    }

    public IReadOnlyList<Image> Images { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Images = (await _imageRepository.GetAllAsync()).ToList();
    }
}
