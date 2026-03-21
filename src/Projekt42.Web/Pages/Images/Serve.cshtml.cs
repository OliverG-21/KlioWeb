using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Projekt42.Core.Interfaces;

namespace Projekt42.Web.Pages.Images;

public class ServeModel : PageModel
{
    private readonly IImageRepository _imageRepository;

    public ServeModel(IImageRepository imageRepository)
    {
        _imageRepository = imageRepository;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var image = await _imageRepository.GetByIdAsync(id);
        if (image == null || !System.IO.File.Exists(image.FilePath))
            return NotFound();

        return new PhysicalFileResult(image.FilePath, image.MimeType) { EnableRangeProcessing = true };
    }
}
