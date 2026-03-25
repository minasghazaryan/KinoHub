using KinoHub.Web.Models;
using KinoHub.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KinoHub.Web.Pages;

public class WatchModel(KinopoiskService kinopoiskService) : PageModel
{
    public KinopoiskFilmDetailsDto? FilmDetails { get; set; }
    public IReadOnlyList<KinopoiskVideoItemDto> Videos { get; set; } = [];
    public KinopoiskVideoItemDto? SelectedVideo { get; set; }
    public string? SelectedEmbedUrl { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int? id, int? video = null, CancellationToken cancellationToken = default)
    {
        if (!id.HasValue || id.Value <= 0)
        {
            ErrorMessage = "Не указан ID фильма.";
            return Page();
        }

        try
        {
            FilmDetails = await kinopoiskService.GetMovieDetailsAsync(id.Value, cancellationToken);
            var response = await kinopoiskService.GetVideosAsync(id.Value, cancellationToken);
            Videos = (response?.Items ?? [])
                .Where(v => !string.IsNullOrWhiteSpace(v.Url))
                .ToList();

            if (Videos.Count == 0)
            {
                ErrorMessage = "Видео по этому фильму пока недоступно.";
                return Page();
            }

            var selectedIndex = video.GetValueOrDefault();
            if (selectedIndex < 0 || selectedIndex >= Videos.Count)
                selectedIndex = 0;

            SelectedVideo = Videos[selectedIndex];
            SelectedEmbedUrl = BuildEmbedUrl(SelectedVideo.Url);
        }
        catch
        {
            ErrorMessage = "Не удалось загрузить видео для этого фильма.";
        }

        return Page();
    }

    private static string? BuildEmbedUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        var host = uri.Host.ToLowerInvariant();
        if (host.Contains("youtu.be"))
        {
            var id = uri.AbsolutePath.Trim('/');
            return string.IsNullOrWhiteSpace(id) ? null : $"https://www.youtube.com/embed/{id}";
        }

        if (host.Contains("youtube.com"))
        {
            if (uri.AbsolutePath.StartsWith("/embed/", StringComparison.OrdinalIgnoreCase))
                return url;

            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
            if (query.TryGetValue("v", out var videoId))
                return $"https://www.youtube.com/embed/{videoId.ToString()}";
        }

        return null;
    }
}
