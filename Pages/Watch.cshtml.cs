using KinoHub.Web.Models;
using KinoHub.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KinoHub.Web.Pages;

public class WatchModel(VibixService vibixService) : PageModel
{
    public VibixVideoDto? Video { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken cancellationToken = default)
    {
        if (!id.HasValue || id.Value <= 0)
        {
            ErrorMessage = "Не указан ID фильма.";
            return Page();
        }

        Video = await vibixService.GetVideoByKpIdAsync(id.Value, cancellationToken);
        if (Video == null)
            ErrorMessage = "Видео по этому фильму пока недоступно.";

        return Page();
    }
}
