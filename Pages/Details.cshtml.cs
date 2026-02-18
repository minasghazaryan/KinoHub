using KinoHub.Web.Models;
using KinoHub.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KinoHub.Web.Pages;

public class DetailsModel(KinopoiskService kinopoiskService, ILogger<DetailsModel> logger) : PageModel
{
    public KinopoiskFilmDetailsDto? FilmDetails { get; set; }
    public KinopoiskSeasonsResponseDto? Seasons { get; set; }
    public bool IsSeries { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken cancellationToken = default)
    {
        if (!id.HasValue || id.Value <= 0)
        {
            ErrorMessage = "Не указан ID фильма.";
            return Page();
        }

        try
        {
            FilmDetails = await kinopoiskService.GetMovieDetailsAsync(id.Value, cancellationToken);
            if (FilmDetails == null)
            {
                ErrorMessage = "Фильм не найден.";
                return Page();
            }

            // Try to fetch seasons - if successful and has data, it's a series
            try
            {
                Seasons = await kinopoiskService.GetSeasonsAsync(id.Value, cancellationToken);
                IsSeries = Seasons != null && Seasons.Total > 0;
            }
            catch
            {
                // Not a series or seasons endpoint failed - that's OK
                IsSeries = false;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch film details for ID {FilmId}", id);
            ErrorMessage = "Ошибка при загрузке данных фильма.";
        }

        return Page();
    }
}
