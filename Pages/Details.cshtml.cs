using KinoHub.Web;
using KinoHub.Web.Models;
using KinoHub.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KinoHub.Web.Pages;

public class DetailsModel(KinopoiskService kinopoiskService, VibixService vibixService, IConfiguration configuration, ILogger<DetailsModel> logger) : PageModel
{
    public KinopoiskFilmDetailsDto? FilmDetails { get; set; }
    /// <summary>Vibix Publisher ID for SDK player (data-publisher-id). From Vibix:PublisherId.</summary>
    public string? VibixPublisherId { get; set; }
    public KinopoiskSeasonsResponseDto? Seasons { get; set; }
    public VibixVideoDto? VibixVideo { get; set; }
    public IReadOnlyList<VibixVoiceoverDto> VoiceoversList { get; set; } = [];
    public KinopoiskVideosResponseDto? Videos { get; set; }
    public IReadOnlyList<KinopoiskStaffItemDto> Staff { get; set; } = [];
    public KinopoiskFactsResponseDto? Facts { get; set; }
    public KinopoiskBoxOfficeResponseDto? BoxOffice { get; set; }
    public KinopoiskAwardsResponseDto? Awards { get; set; }
    public KinopoiskSimilarsResponseDto? Similars { get; set; }
    public KinopoiskRelationsResponseDto? Relations { get; set; }
    public KinopoiskReviewsResponseDto? Reviews { get; set; }
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
            VibixPublisherId = configuration["Vibix:PublisherId"];
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
                IsSeries = false;
            }

            // Trailers and teasers
            try { Videos = await kinopoiskService.GetVideosAsync(id.Value, cancellationToken); } catch { }

            // Staff (cast & crew)
            try { Staff = await kinopoiskService.GetStaffAsync(id.Value, cancellationToken); } catch { }

            // Facts and bloopers
            try { Facts = await kinopoiskService.GetFactsAsync(id.Value, cancellationToken); } catch { }

            // Box office
            try { BoxOffice = await kinopoiskService.GetBoxOfficeAsync(id.Value, cancellationToken); } catch { }

            // Awards
            try { Awards = await kinopoiskService.GetAwardsAsync(id.Value, cancellationToken); } catch { }

            // Similar films
            try { Similars = await kinopoiskService.GetSimilarsAsync(id.Value, cancellationToken); } catch { }

            // Related films (sequels, prequels, etc.)
            try { Relations = await kinopoiskService.GetRelationsAsync(id.Value, cancellationToken); } catch { }

            // Reviews (page 1, DATE_DESC)
            try { Reviews = await kinopoiskService.GetReviewsAsync(id.Value, page: 1, order: "DATE_DESC", cancellationToken); } catch { }

            // Vibix: video player and voiceovers for this film (from GET .../videos/kp/{id} only)
            try
            {
                VibixVideo = await vibixService.GetVideoByKpIdAsync(id.Value, cancellationToken);
                if (VibixVideo?.Voiceovers != null && VibixVideo.Voiceovers.Count > 0)
                    VoiceoversList = VibixVideo.Voiceovers;
            }
            catch { }
        }
        catch (KinopoiskQuotaExceededException)
        {
            // Hide provider-specific quota details from end users, but log on the server.
            logger.LogWarning("Kinopoisk API quota exceeded while loading film details.");
            ErrorMessage = "Сервис временно недоступен. Попробуйте ещё раз позже.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch film details for ID {FilmId}", id);
            ErrorMessage = "Ошибка при загрузке данных фильма.";
        }

        return Page();
    }

}
