using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Klio.Core.Interfaces;

namespace Klio.Web.Pages.Api;

public class ImageTagsModel : PageModel
{
    private readonly IImageTagRepository _tagRepository;

    public ImageTagsModel(IImageTagRepository tagRepository)
    {
        _tagRepository = tagRepository;
    }

    public async Task<IActionResult> OnGetAsync(int imageId)
    {
        var tags = await _tagRepository.GetByImageIdAsync(imageId);
        return new JsonResult(tags.Select(t => new { t.Id, t.Tag, t.Source }));
    }
}
