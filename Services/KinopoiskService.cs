using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KinoHub.Web;
using KinoHub.Web.Models;

namespace KinoHub.Web.Services;

public partial class KinopoiskService(
    IHttpClientFactory httpClientFactory,
    ILogger<KinopoiskService> logger)
{
    private const string BaseAddress = "https://kinopoiskapiunofficial.tech";
    private const string FilmsPath = "/api/v2.2/films";
    private const string CollectionsPath = "/api/v2.2/films/collections";
    private const string PremieresPath = "/api/v2.2/films/premieres";
    private const string FiltersPath = "/api/v2.2/films/filters";
    private const string SearchByKeywordPath = "/api/v2.1/films/search-by-keyword";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public async Task<KinopoiskFilmDetailsDto?> GetMovieDetailsAsync(int kpId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Kinopoisk");
        var response = await client.GetAsync($"{BaseAddress}{FilmsPath}/{kpId}", cancellationToken);
        await EnsureSuccessfulResponseAsync(response);
        return await response.Content.ReadFromJsonAsync<KinopoiskFilmDetailsDto>(JsonOptions, cancellationToken);
    }

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
        var orderUpper = order?.ToUpperInvariant();
        var apiOrder = (orderUpper == "YEAR_ASC" || orderUpper == "YEAR_DESC") ? "YEAR" : (order ?? "RATING");

        var query = new List<string>
        {
            $"order={Uri.EscapeDataString(apiOrder)}",
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
        var response = await client.GetAsync($"{BaseAddress}{FilmsPath}?{string.Join("&", query)}", cancellationToken);
        await EnsureSuccessfulResponseAsync(response);

        var wrapper = await response.Content.ReadFromJsonAsync<KinopoiskCollectionResponseDto>(JsonOptions, cancellationToken);
        var rawItems = wrapper?.Items ?? [];
        var items = rawItems.Where(i => HasRealPoster(i.PosterUrl, i.PosterUrlPreview)).ToList();

        return new FilmsByFiltersResult(items, wrapper?.Total ?? 0, wrapper?.TotalPages ?? 0, page);
    }

    public async Task<KinopoiskFiltersResponseDto> GetFiltersAsync(CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Kinopoisk");
        var response = await client.GetAsync($"{BaseAddress}{FiltersPath}", cancellationToken);
        await EnsureSuccessfulResponseAsync(response);

        return await response.Content.ReadFromJsonAsync<KinopoiskFiltersResponseDto>(JsonOptions, cancellationToken)
            ?? new KinopoiskFiltersResponseDto();
    }

    private static bool HasRealPoster(string? posterUrl, string? posterUrlPreview)
    {
        var url = posterUrl ?? posterUrlPreview ?? "";
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (url.Contains("no-poster", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    public async Task<KinopoiskCollectionPageResult> GetCollectionAsync(string type, int page = 1, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Kinopoisk");
        var response = await client.GetAsync($"{BaseAddress}{CollectionsPath}?type={Uri.EscapeDataString(type)}&page={page}", cancellationToken);
        await EnsureSuccessfulResponseAsync(response);

        var wrapper = await response.Content.ReadFromJsonAsync<KinopoiskCollectionResponseDto>(JsonOptions, cancellationToken);
        var rawItems = wrapper?.Items ?? [];
        var items = rawItems.Where(i => HasRealPoster(i.PosterUrl, i.PosterUrlPreview)).ToList();

        return new KinopoiskCollectionPageResult(items, wrapper?.Total ?? 0, wrapper?.TotalPages ?? 0, page);
    }

    public async Task<KinopoiskPremieresResponseDto?> GetPremieresAsync(int year, string month, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Kinopoisk");
        var response = await client.GetAsync($"{BaseAddress}{PremieresPath}?year={year}&month={Uri.EscapeDataString(month)}", cancellationToken);
        await EnsureSuccessfulResponseAsync(response);
        return await response.Content.ReadFromJsonAsync<KinopoiskPremieresResponseDto>(JsonOptions, cancellationToken);
    }

    public async Task<KinopoiskSearchByKeywordResult> SearchByKeywordAsync(string keyword, int page = 1, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return new KinopoiskSearchByKeywordResult([], 0, 0, "", page);

        var client = httpClientFactory.CreateClient("Kinopoisk");
        var response = await client.GetAsync(
            $"{BaseAddress}{SearchByKeywordPath}?keyword={Uri.EscapeDataString(keyword.Trim())}&page={page}",
            cancellationToken);
        await EnsureSuccessfulResponseAsync(response);

        var dto = await response.Content.ReadFromJsonAsync<KinopoiskSearchByKeywordResponseDto>(cancellationToken);
        var rawFilms = dto?.Films ?? [];
        var films = rawFilms.Where(f => HasRealPoster(f.PosterUrl, f.PosterUrlPreview)).ToList();

        return new KinopoiskSearchByKeywordResult(
            films,
            dto?.SearchFilmsCountResult ?? 0,
            dto?.PagesCount ?? 0,
            dto?.Keyword ?? keyword.Trim(),
            page);
    }

    public async Task<IReadOnlyList<KinopoiskFilmItemDto>> GetTrendingMoviesAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetCollectionAsync(KinopoiskCollectionType.Top250Movies, 1, cancellationToken);
        return result.Items;
    }

    public async Task<KinopoiskSeasonsResponseDto?> GetSeasonsAsync(int kpId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Kinopoisk");
        var response = await client.GetAsync($"{BaseAddress}{FilmsPath}/{kpId}/seasons", cancellationToken);
        await EnsureSuccessfulResponseAsync(response);
        return await response.Content.ReadFromJsonAsync<KinopoiskSeasonsResponseDto>(JsonOptions, cancellationToken);
    }

    public async Task<KinopoiskVideosResponseDto?> GetVideosAsync(int kpId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Kinopoisk");
        var response = await client.GetAsync($"{BaseAddress}{FilmsPath}/{kpId}/videos", cancellationToken);
        await EnsureSuccessfulResponseAsync(response);
        return await response.Content.ReadFromJsonAsync<KinopoiskVideosResponseDto>(JsonOptions, cancellationToken);
    }

    public async Task<IReadOnlyList<KinopoiskStaffItemDto>> GetStaffAsync(int kpId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Kinopoisk");
        var response = await client.GetAsync($"{BaseAddress}/api/v1/staff?filmId={kpId}", cancellationToken);
        await EnsureSuccessfulResponseAsync(response);
        var list = await response.Content.ReadFromJsonAsync<List<KinopoiskStaffItemDto>>(JsonOptions, cancellationToken);
        return list ?? [];
    }

    public async Task<KinopoiskFactsResponseDto?> GetFactsAsync(int kpId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Kinopoisk");
        var response = await client.GetAsync($"{BaseAddress}{FilmsPath}/{kpId}/facts", cancellationToken);
        await EnsureSuccessfulResponseAsync(response);
        return await response.Content.ReadFromJsonAsync<KinopoiskFactsResponseDto>(JsonOptions, cancellationToken);
    }

    public async Task<KinopoiskBoxOfficeResponseDto?> GetBoxOfficeAsync(int kpId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Kinopoisk");
        var response = await client.GetAsync($"{BaseAddress}{FilmsPath}/{kpId}/box_office", cancellationToken);
        await EnsureSuccessfulResponseAsync(response);
        return await response.Content.ReadFromJsonAsync<KinopoiskBoxOfficeResponseDto>(JsonOptions, cancellationToken);
    }

    public async Task<KinopoiskAwardsResponseDto?> GetAwardsAsync(int kpId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Kinopoisk");
        var response = await client.GetAsync($"{BaseAddress}{FilmsPath}/{kpId}/awards", cancellationToken);
        await EnsureSuccessfulResponseAsync(response);
        return await response.Content.ReadFromJsonAsync<KinopoiskAwardsResponseDto>(JsonOptions, cancellationToken);
    }

    public async Task<KinopoiskSimilarsResponseDto?> GetSimilarsAsync(int kpId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Kinopoisk");
        var response = await client.GetAsync($"{BaseAddress}{FilmsPath}/{kpId}/similars", cancellationToken);
        await EnsureSuccessfulResponseAsync(response);
        return await response.Content.ReadFromJsonAsync<KinopoiskSimilarsResponseDto>(JsonOptions, cancellationToken);
    }

    public async Task<KinopoiskRelationsResponseDto?> GetRelationsAsync(int kpId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Kinopoisk");
        var response = await client.GetAsync($"{BaseAddress}{FilmsPath}/{kpId}/relations", cancellationToken);
        await EnsureSuccessfulResponseAsync(response);
        return await response.Content.ReadFromJsonAsync<KinopoiskRelationsResponseDto>(JsonOptions, cancellationToken);
    }

    public async Task<KinopoiskReviewsResponseDto?> GetReviewsAsync(int kpId, int page = 1, string order = "DATE_DESC", CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Kinopoisk");
        var response = await client.GetAsync(
            $"{BaseAddress}{FilmsPath}/{kpId}/reviews?page={page}&order={Uri.EscapeDataString(order)}",
            cancellationToken);
        await EnsureSuccessfulResponseAsync(response);
        return await response.Content.ReadFromJsonAsync<KinopoiskReviewsResponseDto>(JsonOptions, cancellationToken);
    }

    private async Task EnsureSuccessfulResponseAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.PaymentRequired)
        {
            logger.LogWarning("Kinopoisk API returned 402 Payment Required. Check API key quota at https://kinopoiskapiunofficial.tech");
            throw new KinopoiskQuotaExceededException();
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            logger.LogWarning(
                "Kinopoisk API request failed with status {StatusCode}. Body: {Body}",
                (int)response.StatusCode,
                body);
        }

        response.EnsureSuccessStatusCode();
    }
}
