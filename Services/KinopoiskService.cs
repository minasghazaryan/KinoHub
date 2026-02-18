using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KinoHub.Web.Data;
using KinoHub.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace KinoHub.Web.Services;

public class KinopoiskService(
    IHttpClientFactory httpClientFactory,
    KinoContext dbContext,
    ILogger<KinopoiskService> logger)
{
    private const string BaseAddress = "https://kinopoiskapiunofficial.tech";
    private const string FilmsPath = "/api/v2.2/films";
    private const string CollectionsPath = "/api/v2.2/films/collections";
    private const string SearchByKeywordPath = "/api/v2.1/films/search-by-keyword";

    /// <summary>
    /// Fetches film details by Kinopoisk ID from /api/v2.2/films/{id}.
    /// </summary>
    public async Task<KinopoiskFilmDetailsDto?> GetMovieDetailsAsync(int kpId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Kinopoisk");
        var url = $"{BaseAddress}{FilmsPath}/{kpId}";
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
        var dto = await response.Content.ReadFromJsonAsync<KinopoiskFilmDetailsDto>(options, cancellationToken);
        return dto;
    }

    /// <summary>
    /// Fetches a collection page from /api/v2.2/films/collections?type={type}&amp;page={page}.
    /// Each page contains up to 20 films. Use <see cref="KinopoiskCollectionType"/> for type values.
    /// </summary>
    public async Task<KinopoiskCollectionPageResult> GetCollectionAsync(string type, int page = 1, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Kinopoisk");
        var url = $"{BaseAddress}{CollectionsPath}?type={Uri.EscapeDataString(type)}&page={page}";
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
        var wrapper = await response.Content.ReadFromJsonAsync<KinopoiskCollectionResponseDto>(options, cancellationToken);
        var items = wrapper?.Items ?? [];
        return new KinopoiskCollectionPageResult(
            items,
            wrapper?.Total ?? 0,
            wrapper?.TotalPages ?? 0,
            page
        );
    }

    /// <summary>
    /// Searches films by keyword: GET /api/v2.1/films/search-by-keyword?keyword={keyword}&amp;page={page}.
    /// Returns up to 20 films per page.
    /// </summary>
    public async Task<KinopoiskSearchByKeywordResult> SearchByKeywordAsync(string keyword, int page = 1, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return new KinopoiskSearchByKeywordResult([], 0, 0, "", page);

        var client = httpClientFactory.CreateClient("Kinopoisk");
        var url = $"{BaseAddress}{SearchByKeywordPath}?keyword={Uri.EscapeDataString(keyword.Trim())}&page={page}";
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<KinopoiskSearchByKeywordResponseDto>(cancellationToken);
        var films = dto?.Films ?? [];
        return new KinopoiskSearchByKeywordResult(
            films,
            dto?.SearchFilmsCountResult ?? 0,
            dto?.PagesCount ?? 0,
            dto?.Keyword ?? keyword,
            page
        );
    }

    /// <summary>
    /// Fetches TOP_250_MOVIES collection, page 1 (convenience wrapper).
    /// </summary>
    public async Task<IReadOnlyList<KinopoiskFilmItemDto>> GetTrendingMoviesAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetCollectionAsync(KinopoiskCollectionType.Top250Movies, page: 1, cancellationToken);
        return result.Items;
    }

    /// <summary>
    /// Saves or updates movies in the database from the given Kinopoisk film items.
    /// Matches by KinopoiskId; inserts new or updates existing (Title/NameRu, Description, ReleaseYear, PosterPath, Rating).
    /// </summary>
    public async Task<int> SaveOrUpdateMoviesAsync(IEnumerable<KinopoiskFilmItemDto> items, CancellationToken cancellationToken = default)
    {
        var list = items.ToList();
        if (list.Count == 0)
            return 0;

        var kpIds = list.Select(x => x.KinopoiskId).ToList();
        var existing = await dbContext.Movies
            .Where(m => m.KinopoiskId != null && kpIds.Contains(m.KinopoiskId.Value))
            .ToDictionaryAsync(m => m.KinopoiskId!.Value, cancellationToken);

        var updated = 0;
        foreach (var item in list)
        {
            var title = item.NameRu ?? item.NameEn ?? item.NameOriginal ?? "";
            var posterUrl = item.PosterUrl ?? item.PosterUrlPreview ?? "";
            var year = item.Year.HasValue ? item.Year.Value.ToString() : "";

            if (existing.TryGetValue(item.KinopoiskId, out var movie))
            {
                movie.Title = title;
                movie.NameRu = item.NameRu ?? "";
                movie.ReleaseYear = year;
                movie.PosterPath = posterUrl;
                movie.Rating = item.RatingKinopoisk;
                updated++;
            }
            else
            {
                await dbContext.Movies.AddAsync(new Movie
                {
                    KinopoiskId = item.KinopoiskId,
                    Title = title,
                    NameRu = item.NameRu ?? "",
                    Description = "", // Collection items don't include description
                    ReleaseYear = year,
                    PosterPath = posterUrl,
                    Rating = item.RatingKinopoisk,
                    ImdbId = $"kp-{item.KinopoiskId}" // Synthetic key so unique constraint holds
                }, cancellationToken);
                updated++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Saved or updated {Count} movies from Kinopoisk.", updated);
        return updated;
    }

    /// <summary>
    /// Fetches TOP_250_MOVIES page 1 and syncs to the database (convenience).
    /// </summary>
    public async Task<int> SyncTrendingToDatabaseAsync(CancellationToken cancellationToken = default)
    {
        var items = await GetTrendingMoviesAsync(cancellationToken);
        return await SaveOrUpdateMoviesAsync(items, cancellationToken);
    }

    /// <summary>
    /// Fetches a collection page by type and page number, then syncs to the database.
    /// </summary>
    public async Task<int> SyncCollectionToDatabaseAsync(string type, int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await GetCollectionAsync(type, page, cancellationToken);
        return await SaveOrUpdateMoviesAsync(result.Items, cancellationToken);
    }

    /// <summary>
    /// Fetches season data for a series by Kinopoisk film ID from /api/v2.2/films/{id}/seasons.
    /// Returns seasons with episodes (synopsis, release dates, etc.).
    /// </summary>
    public async Task<KinopoiskSeasonsResponseDto?> GetSeasonsAsync(int kpId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Kinopoisk");
        var url = $"{BaseAddress}{FilmsPath}/{kpId}/seasons";
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
        var dto = await response.Content.ReadFromJsonAsync<KinopoiskSeasonsResponseDto>(options, cancellationToken);
        return dto;
    }
}
