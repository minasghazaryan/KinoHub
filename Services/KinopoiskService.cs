using System.Net;
using System.Net.Http.Json;
using KinoHub.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using KinoHub.Web.Data;
using KinoHub.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace KinoHub.Web.Services;

public class KinopoiskService(
    IHttpClientFactory httpClientFactory,
    KinoContext dbContext,
    IApiCacheService apiCache,
    ILogger<KinopoiskService> logger)
{
    private const string BaseAddress = "https://kinopoiskapiunofficial.tech";
    private const string FilmsPath = "/api/v2.2/films";
    private const string CollectionsPath = "/api/v2.2/films/collections";
    private const string PremieresPath = "/api/v2.2/films/premieres";
    private const string SearchByKeywordPath = "/api/v2.1/films/search-by-keyword";

    /// <summary>
    /// Fetches film details by Kinopoisk ID from /api/v2.2/films/{id}. Cached for 10–30 min.
    /// </summary>
    public Task<KinopoiskFilmDetailsDto?> GetMovieDetailsAsync(int kpId, CancellationToken cancellationToken = default) =>
        apiCache.GetOrCreateAsync("kinopoisk", $"film:{kpId}", async ct =>
        {
            var client = httpClientFactory.CreateClient("Kinopoisk");
            var url = $"{BaseAddress}{FilmsPath}/{kpId}";
            var response = await client.GetAsync(url, ct);
            if (response.StatusCode == HttpStatusCode.PaymentRequired)
            {
                logger.LogWarning("Kinopoisk API returned 402 Payment Required. Check API key quota at https://kinopoiskapiunofficial.tech");
                throw new KinopoiskQuotaExceededException();
            }
            response.EnsureSuccessStatusCode();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };
            return await response.Content.ReadFromJsonAsync<KinopoiskFilmDetailsDto>(options, ct);
        }, cancellationToken);

    /// <summary>
    /// Fetches films by filters: GET /api/v2.2/films. Uses our DB genre/country IDs (no /films/filters call).
    /// Max 20 per page, max 400 total. API allows at most one genre and one country.
    /// </summary>
    public async Task<FilmsByFiltersResult> GetFilmsByFiltersAsync(
        string order = "RATING",
        string type = "ALL",
        double ratingFrom = 0,
        double ratingTo = 10,
        int yearFrom = 1000,
        int yearTo = 3000,
        int page = 1,
        int? genreId = null,
        int? countryId = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"order={Uri.EscapeDataString(order)}",
            $"type={Uri.EscapeDataString(type)}",
            $"ratingFrom={ratingFrom}",
            $"ratingTo={ratingTo}",
            $"yearFrom={yearFrom}",
            $"yearTo={yearTo}",
            $"page={page}"
        };
        if (genreId.HasValue)
            query.Add($"genres={genreId.Value}");
        if (countryId.HasValue)
            query.Add($"countries={countryId.Value}");

        var client = httpClientFactory.CreateClient("Kinopoisk");
        var url = $"{BaseAddress}{FilmsPath}?{string.Join("&", query)}";
        var response = await client.GetAsync(url, cancellationToken);
        if (response.StatusCode == HttpStatusCode.PaymentRequired)
        {
            logger.LogWarning("Kinopoisk API returned 402 Payment Required. Check API key quota.");
            return new FilmsByFiltersResult([], 0, 0, page);
        }
        response.EnsureSuccessStatusCode();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
        var wrapper = await response.Content.ReadFromJsonAsync<KinopoiskCollectionResponseDto>(options, cancellationToken);
        var rawItems = wrapper?.Items ?? [];
        var items = rawItems.Where(i => HasRealPoster(i.PosterUrl, i.PosterUrlPreview)).ToList();
        return new FilmsByFiltersResult(
            items,
            wrapper?.Total ?? 0,
            wrapper?.TotalPages ?? 0,
            page
        );
    }

    private const int CatalogPageSize = 20;

    /// <summary>
    /// Loads catalog from DB with optional genre/country/year filter and rating order. Use when movies are synced with genres/countries.
    /// </summary>
    public async Task<FilmsByFiltersResult> GetMoviesFromDbAsync(
        int? genreId = null,
        int? countryId = null,
        int? year = null,
        string order = "RATING",
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var pageNum = page < 1 ? 1 : page;
        var query = dbContext.Movies
            .AsNoTracking()
            .AsSplitQuery()
            .Include(m => m.Genres)
            .Include(m => m.Countries)
            .Where(m => m.KinopoiskId != null);

        if (genreId.HasValue)
            query = query.Where(m => m.Genres.Any(g => g.Id == genreId.Value));
        if (countryId.HasValue)
            query = query.Where(m => m.Countries.Any(c => c.Id == countryId.Value));
        if (year.HasValue)
            query = query.Where(m => m.ReleaseYear != null && m.ReleaseYear == year.Value.ToString());

        query = order?.ToUpperInvariant() == "YEAR" || order == "NUM_VOTE"
            ? query.OrderByDescending(m => m.ReleaseYear).ThenByDescending(m => m.Rating ?? 0)
            : query.OrderByDescending(m => m.Rating ?? 0).ThenByDescending(m => m.Id);

        var total = await query.CountAsync(cancellationToken);
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)CatalogPageSize);
        var movies = await query
            .Skip((pageNum - 1) * CatalogPageSize)
            .Take(CatalogPageSize)
            .ToListAsync(cancellationToken);

        var items = movies.Select(MovieToFilmItemDto).ToList();
        return new FilmsByFiltersResult(items, total, totalPages, pageNum);
    }

    private static KinopoiskFilmItemDto MovieToFilmItemDto(Movie m)
    {
        var year = int.TryParse(m.ReleaseYear, out var y) ? y : (int?)null;
        return new KinopoiskFilmItemDto
        {
            KinopoiskId = m.KinopoiskId ?? 0,
            NameRu = m.NameRu,
            NameEn = m.Title,
            NameOriginal = m.Title,
            Countries = m.Countries.Select(c => new KinopoiskCountryDto { Country = c.Name }).ToList(),
            Genres = m.Genres.Select(g => new KinopoiskGenreDto { Genre = g.Name }).ToList(),
            RatingKinopoisk = m.Rating ?? 0,
            Year = year,
            PosterUrl = m.PosterPath,
            PosterUrlPreview = m.PosterPath
        };
    }

    /// <summary>True if the item has a real poster URL (not null/empty and not the no-poster placeholder).</summary>
    private static bool HasRealPoster(string? posterUrl, string? posterUrlPreview)
    {
        var url = posterUrl ?? posterUrlPreview ?? "";
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (url.Contains("no-poster", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
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
        if (response.StatusCode == HttpStatusCode.PaymentRequired)
        {
            logger.LogWarning("Kinopoisk API returned 402 Payment Required. Check API key quota.");
            return new KinopoiskCollectionPageResult([], 0, 0, page);
        }
        response.EnsureSuccessStatusCode();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
        var wrapper = await response.Content.ReadFromJsonAsync<KinopoiskCollectionResponseDto>(options, cancellationToken);
        var rawItems = wrapper?.Items ?? [];
        var items = rawItems.Where(i => HasRealPoster(i.PosterUrl, i.PosterUrlPreview)).ToList();
        return new KinopoiskCollectionPageResult(
            items,
            wrapper?.Total ?? 0,
            wrapper?.TotalPages ?? 0,
            page
        );
    }

    /// <summary>
    /// Fetches premieres: GET /api/v2.2/films/premieres?year={year}&amp;month={month}.
    /// Month must be uppercase English (e.g. JANUARY, APRIL).
    /// </summary>
    public async Task<KinopoiskPremieresResponseDto?> GetPremieresAsync(int year, string month, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Kinopoisk");
        var url = $"{BaseAddress}{PremieresPath}?year={year}&month={Uri.EscapeDataString(month)}";
        var response = await client.GetAsync(url, cancellationToken);
        if (response.StatusCode == HttpStatusCode.PaymentRequired)
        {
            logger.LogWarning("Kinopoisk API returned 402 Payment Required. Check API key quota.");
            return null;
        }
        response.EnsureSuccessStatusCode();
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
        return await response.Content.ReadFromJsonAsync<KinopoiskPremieresResponseDto>(options, cancellationToken);
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
        if (response.StatusCode == HttpStatusCode.PaymentRequired)
        {
            logger.LogWarning("Kinopoisk API returned 402 Payment Required. Check API key quota.");
            return new KinopoiskSearchByKeywordResult([], 0, 0, keyword.Trim(), page);
        }
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<KinopoiskSearchByKeywordResponseDto>(cancellationToken);
        var rawFilms = dto?.Films ?? [];
        var films = rawFilms.Where(f => HasRealPoster(f.PosterUrl, f.PosterUrlPreview)).ToList();
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
    /// Matches by KinopoiskId; inserts new or updates existing (Title/NameRu, Description, ReleaseYear, PosterPath, Rating, Genres, Countries).
    /// </summary>
    public async Task<int> SaveOrUpdateMoviesAsync(IEnumerable<KinopoiskFilmItemDto> items, CancellationToken cancellationToken = default)
    {
        var list = items.ToList();
        if (list.Count == 0)
            return 0;

        var genresList = await dbContext.Genres.ToListAsync(cancellationToken);
        var genresByName = new Dictionary<string, Genre>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in genresList)
        {
            var key = g.Name.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(key) && !genresByName.ContainsKey(key))
                genresByName[key] = g;
        }
        var countriesList = await dbContext.Countries.ToListAsync(cancellationToken);
        var countriesByName = new Dictionary<string, Country>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in countriesList)
        {
            var key = c.Name.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(key) && !countriesByName.ContainsKey(key))
                countriesByName[key] = c;
        }

        var kpIds = list.Select(x => x.KinopoiskId).ToList();
        var existing = await dbContext.Movies
            .Include(m => m.Genres)
            .Include(m => m.Countries)
            .Where(m => m.KinopoiskId != null && kpIds.Contains(m.KinopoiskId.Value))
            .ToDictionaryAsync(m => m.KinopoiskId!.Value, cancellationToken);

        var updated = 0;
        foreach (var item in list)
        {
            var title = item.NameRu ?? item.NameEn ?? item.NameOriginal ?? "";
            var posterUrl = item.PosterUrl ?? item.PosterUrlPreview ?? "";
            var year = item.Year.HasValue ? item.Year.Value.ToString() : "";

            var genreList = ResolveGenres(item.Genres, genresByName);
            var countryList = ResolveCountries(item.Countries, countriesByName);

            if (existing.TryGetValue(item.KinopoiskId, out var movie))
            {
                movie.Title = title;
                movie.NameRu = item.NameRu ?? "";
                movie.ReleaseYear = year;
                movie.PosterPath = posterUrl;
                movie.Rating = item.RatingKinopoisk;
                movie.Genres.Clear();
                foreach (var g in genreList)
                    movie.Genres.Add(g);
                movie.Countries.Clear();
                foreach (var c in countryList)
                    movie.Countries.Add(c);
                updated++;
            }
            else
            {
                var newMovie = new Movie
                {
                    KinopoiskId = item.KinopoiskId,
                    Title = title,
                    NameRu = item.NameRu ?? "",
                    Description = "", // Collection items don't include description
                    ReleaseYear = year,
                    PosterPath = posterUrl,
                    Rating = item.RatingKinopoisk,
                    ImdbId = $"kp-{item.KinopoiskId}" // Synthetic key so unique constraint holds
                };
                foreach (var g in genreList)
                    newMovie.Genres.Add(g);
                foreach (var c in countryList)
                    newMovie.Countries.Add(c);
                await dbContext.Movies.AddAsync(newMovie, cancellationToken);
                updated++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Saved or updated {Count} movies from Kinopoisk (with genres/countries).", updated);
        return updated;
    }

    private static List<Genre> ResolveGenres(List<KinopoiskGenreDto>? apiGenres, IReadOnlyDictionary<string, Genre> byName)
    {
        var list = new List<Genre>();
        if (apiGenres == null) return list;
        foreach (var g in apiGenres)
        {
            var name = (g.Genre ?? "").Trim();
            if (string.IsNullOrEmpty(name)) continue;
            if (byName.TryGetValue(name.ToLowerInvariant(), out var genre))
                list.Add(genre);
        }
        return list;
    }

    private static List<Country> ResolveCountries(List<KinopoiskCountryDto>? apiCountries, IReadOnlyDictionary<string, Country> byName)
    {
        var list = new List<Country>();
        if (apiCountries == null) return list;
        foreach (var c in apiCountries)
        {
            var name = (c.Country ?? "").Trim();
            if (string.IsNullOrEmpty(name)) continue;
            if (byName.TryGetValue(name.ToLowerInvariant(), out var country))
                list.Add(country);
        }
        return list;
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
    /// Main sync: GET /api/v2.2/films (order, type, rating, year, page). Syncs one page to the database.
    /// </summary>
    public async Task<int> SyncFilmsByFiltersToDatabaseAsync(
        string order = "RATING",
        string type = "ALL",
        double ratingFrom = 0,
        double ratingTo = 10,
        int yearFrom = 1000,
        int yearTo = 3000,
        int page = 1,
        int? genreId = null,
        int? countryId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await GetFilmsByFiltersAsync(order, type, ratingFrom, ratingTo, yearFrom, yearTo, page, genreId, countryId, cancellationToken);
        return await SaveOrUpdateMoviesAsync(result.Items, cancellationToken);
    }

    /// <summary>
    /// Syncs all pages from GET /api/v2.2/films (order=RATING, type=ALL, etc.). API returns max 400 items (20 pages).
    /// </summary>
    public async Task<(int TotalSynced, int TotalPages)> SyncAllFilmsByFiltersToDatabaseAsync(
        string order = "RATING",
        string type = "ALL",
        double ratingFrom = 0,
        double ratingTo = 10,
        int yearFrom = 1000,
        int yearTo = 3000,
        int? genreId = null,
        int? countryId = null,
        CancellationToken cancellationToken = default)
    {
        var first = await GetFilmsByFiltersAsync(order, type, ratingFrom, ratingTo, yearFrom, yearTo, 1, genreId, countryId, cancellationToken);
        var totalPages = first.TotalPages;
        if (totalPages <= 0)
            return (first.Items.Count, 1);

        var totalSynced = 0;
        for (var page = 1; page <= totalPages; page++)
        {
            var count = await SyncFilmsByFiltersToDatabaseAsync(order, type, ratingFrom, ratingTo, yearFrom, yearTo, page, genreId, countryId, cancellationToken);
            totalSynced += count;
            logger.LogInformation("Synced catalog page {Page}/{TotalPages}, +{Count} films.", page, totalPages, count);
        }
        return (totalSynced, totalPages);
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

    /// <summary>
    /// Fetches trailers, teasers and other videos for a film by Kinopoisk ID from /api/v2.2/films/{id}/videos.
    /// </summary>
    public async Task<KinopoiskVideosResponseDto?> GetVideosAsync(int kpId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Kinopoisk");
        var url = $"{BaseAddress}{FilmsPath}/{kpId}/videos";
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
        var dto = await response.Content.ReadFromJsonAsync<KinopoiskVideosResponseDto>(options, cancellationToken);
        return dto;
    }

    /// <summary>Fetches cast and crew (staff) by film ID: GET /api/v1/staff?filmId=</summary>
    public async Task<IReadOnlyList<KinopoiskStaffItemDto>> GetStaffAsync(int kpId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Kinopoisk");
        var url = $"{BaseAddress}/api/v1/staff?filmId={kpId}";
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var list = await response.Content.ReadFromJsonAsync<List<KinopoiskStaffItemDto>>(options, cancellationToken);
        return list ?? [];
    }

    /// <summary>Fetches facts and bloopers: GET /api/v2.2/films/{id}/facts</summary>
    public async Task<KinopoiskFactsResponseDto?> GetFactsAsync(int kpId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Kinopoisk");
        var url = $"{BaseAddress}{FilmsPath}/{kpId}/facts";
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return await response.Content.ReadFromJsonAsync<KinopoiskFactsResponseDto>(options, cancellationToken);
    }

    /// <summary>Fetches box office and budget: GET /api/v2.2/films/{id}/box_office</summary>
    public async Task<KinopoiskBoxOfficeResponseDto?> GetBoxOfficeAsync(int kpId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Kinopoisk");
        var url = $"{BaseAddress}{FilmsPath}/{kpId}/box_office";
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return await response.Content.ReadFromJsonAsync<KinopoiskBoxOfficeResponseDto>(options, cancellationToken);
    }

    /// <summary>Fetches awards: GET /api/v2.2/films/{id}/awards</summary>
    public async Task<KinopoiskAwardsResponseDto?> GetAwardsAsync(int kpId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Kinopoisk");
        var url = $"{BaseAddress}{FilmsPath}/{kpId}/awards";
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return await response.Content.ReadFromJsonAsync<KinopoiskAwardsResponseDto>(options, cancellationToken);
    }

    /// <summary>Fetches similar films: GET /api/v2.2/films/{id}/similars</summary>
    public async Task<KinopoiskSimilarsResponseDto?> GetSimilarsAsync(int kpId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Kinopoisk");
        var url = $"{BaseAddress}{FilmsPath}/{kpId}/similars";
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return await response.Content.ReadFromJsonAsync<KinopoiskSimilarsResponseDto>(options, cancellationToken);
    }

    /// <summary>Fetches related films: GET /api/v2.2/films/{id}/relations</summary>
    public async Task<KinopoiskRelationsResponseDto?> GetRelationsAsync(int kpId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Kinopoisk");
        var url = $"{BaseAddress}{FilmsPath}/{kpId}/relations";
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return await response.Content.ReadFromJsonAsync<KinopoiskRelationsResponseDto>(options, cancellationToken);
    }

    /// <summary>Fetches viewer reviews: GET /api/v2.2/films/{id}/reviews?page={page}&amp;order={order}</summary>
    public async Task<KinopoiskReviewsResponseDto?> GetReviewsAsync(int kpId, int page = 1, string order = "DATE_DESC", CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Kinopoisk");
        var url = $"{BaseAddress}{FilmsPath}/{kpId}/reviews?page={page}&order={Uri.EscapeDataString(order)}";
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return await response.Content.ReadFromJsonAsync<KinopoiskReviewsResponseDto>(options, cancellationToken);
    }
}
