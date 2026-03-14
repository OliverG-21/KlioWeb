using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Projekt42.Core.Interfaces;

namespace Projekt42.Web.Pages.Images;

public class ThumbnailModel : PageModel
{
    private readonly IImageRepository _imageRepository;

    public ThumbnailModel(IImageRepository imageRepository)
    {
        _imageRepository = imageRepository;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var image = await _imageRepository.GetByIdAsync(id);
        if (image == null)
            return NotFound();

        if (string.IsNullOrEmpty(image.ThumbnailPath) || !System.IO.File.Exists(image.ThumbnailPath))
        {
            // Fall back to full image if thumbnail not available
            if (!System.IO.File.Exists(image.FilePath))
                return NotFound();

            var fullBytes = await System.IO.File.ReadAllBytesAsync(image.FilePath);
            return File(fullBytes, image.MimeType);
        }

        var bytes = await System.IO.File.ReadAllBytesAsync(image.ThumbnailPath);
        return File(bytes, "image/jpeg");
    }
}
