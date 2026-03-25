using KinoHub.Web.Models;
using KinoHub.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KinoHub.Web.Pages;

public record CarouselItem(int KinopoiskId, string PosterPath, string NameRu, string Title, string ReleaseYear, double? Rating);
public record CatalogFilterOption(int Id, string Name);

public class IndexModel(KinopoiskService kinopoiskService) : PageModel
{
    private const int CatalogUiPageSize = 10;

    public IReadOnlyList<CarouselItem> CarouselItems { get; set; } = [];
    public string? SearchQuery { get; set; }
    public string? Collection { get; set; }
    public KinopoiskSearchByKeywordResult? SearchResult { get; set; }
    public KinopoiskCollectionPageResult? CollectionResult { get; set; }
    public FilmsByFiltersResult? FiltersResult { get; set; }
    public int SearchPage { get; set; } = 1;
    public int CollectionPage { get; set; } = 1;
    public int FiltersPage { get; set; } = 1;
    public int? GenreId { get; set; }
    public int? CountryId { get; set; }
    public int? Year { get; set; }
    public string Order { get; set; } = "RATING";
    public string Type { get; set; } = "ALL";
    public IReadOnlyList<CatalogFilterOption> Genres { get; set; } = [];
    public IReadOnlyList<CatalogFilterOption> Countries { get; set; } = [];
    public IReadOnlyList<KinopoiskPremiereItemDto> Premieres { get; set; } = [];

    public async Task OnGetAsync(string? q, string? collection, int? genreId, int? countryId, int? year, string? order, string? type, int page = 1, int filtersPage = 0, CancellationToken cancellationToken = default)
    {
        SearchQuery = q;
        Collection = collection;
        SearchPage = page < 1 ? 1 : page;
        CollectionPage = page < 1 ? 1 : page;
        GenreId = genreId;
        CountryId = countryId;
        Year = year;
        Order = order ?? "RATING";
        Type = type ?? "ALL";
        FiltersPage = filtersPage < 1 ? 1 : filtersPage;

        await LoadFiltersAsync(cancellationToken);
        await LoadCarouselAsync(cancellationToken);
        await LoadPremieresAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(q))
        {
            try
            {
                SearchResult = await kinopoiskService.SearchByKeywordAsync(q.Trim(), SearchPage, cancellationToken);
            }
            catch
            {
                SearchResult = new KinopoiskSearchByKeywordResult([], 0, 0, q.Trim(), SearchPage);
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(collection))
        {
            await LoadCollectionOrSpecialCatalogAsync(collection.Trim(), cancellationToken);
            return;
        }

        await LoadCatalogAsync(cancellationToken);
    }

    public string GetMovieUrlByKinopoiskId(int? kinopoiskId)
    {
        if (!kinopoiskId.HasValue || kinopoiskId.Value <= 0)
            return "#";

        return $"/Details?id={kinopoiskId.Value}";
    }

    private async Task LoadFiltersAsync(CancellationToken cancellationToken)
    {
        try
        {
            var filters = await kinopoiskService.GetFiltersAsync(cancellationToken);
            Genres = filters.Genres
                .Where(g => g.Id > 0 && !string.IsNullOrWhiteSpace(g.Genre))
                .Select(g => new CatalogFilterOption(g.Id, g.Genre!))
                .OrderBy(g => g.Name)
                .ToList();
            Countries = filters.Countries
                .Where(c => c.Id > 0 && !string.IsNullOrWhiteSpace(c.Country))
                .Select(c => new CatalogFilterOption(c.Id, c.Country!))
                .OrderBy(c => c.Name)
                .ToList();
        }
        catch
        {
            Genres = [];
            Countries = [];
        }
    }

    private async Task LoadCarouselAsync(CancellationToken cancellationToken)
    {
        try
        {
            var items = await kinopoiskService.GetTrendingMoviesAsync(cancellationToken);
            CarouselItems = items
                .Take(20)
                .Select(i => new CarouselItem(
                    i.KinopoiskId,
                    i.PosterUrl ?? i.PosterUrlPreview ?? "",
                    i.NameRu ?? "",
                    i.NameEn ?? i.NameOriginal ?? "",
                    i.Year?.ToString() ?? "",
                    i.RatingKinopoisk > 0 ? i.RatingKinopoisk : null))
                .ToList();
        }
        catch
        {
            CarouselItems = [];
        }
    }

    private async Task LoadPremieresAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var month = now.ToString("MMMM", System.Globalization.CultureInfo.InvariantCulture).ToUpperInvariant();

        try
        {
            var response = await kinopoiskService.GetPremieresAsync(now.Year, month, cancellationToken);
            Premieres = (response?.Items ?? [])
                .Where(i => i.KinopoiskId > 0)
                .Take(5)
                .ToList();
        }
        catch
        {
            Premieres = [];
        }
    }

    private async Task LoadCollectionOrSpecialCatalogAsync(string collection, CancellationToken cancellationToken)
    {
        if (collection == "ANIME" || collection == "KIDS_ANIMATION_THEME")
        {
            GenreId = collection == "ANIME"
                ? FindFilterIdByName(Genres, "аниме")
                : FindFilterIdByName(Genres, "мульт");
            var uiPage = CollectionPage;
            var apiPage = GetCatalogApiPage(uiPage);

            if (!GenreId.HasValue)
            {
                FiltersResult = new FilmsByFiltersResult([], 0, 0, uiPage);
                return;
            }

            try
            {
                FiltersResult = await kinopoiskService.GetFilmsByFiltersAsync(
                    order: Order,
                    type: Type,
                    page: apiPage,
                    genreId: GenreId,
                    yearFrom: Year ?? 1000,
                    yearTo: Year ?? 3000,
                    cancellationToken: cancellationToken);
                FiltersResult = AdaptFiltersResultForUi(FiltersResult, uiPage);
            }
            catch
            {
                FiltersResult = new FilmsByFiltersResult([], 0, 0, uiPage);
            }

            return;
        }

        try
        {
            CollectionResult = await kinopoiskService.GetCollectionAsync(collection, CollectionPage, cancellationToken);
        }
        catch
        {
            CollectionResult = new KinopoiskCollectionPageResult([], 0, 0, CollectionPage);
        }
    }

    private async Task LoadCatalogAsync(CancellationToken cancellationToken)
    {
        try
        {
            var uiPage = FiltersPage;
            var apiPage = GetCatalogApiPage(uiPage);
            var yearFrom = Year ?? 1000;
            var yearTo = Year ?? 3000;
            FiltersResult = await kinopoiskService.GetFilmsByFiltersAsync(
                order: Order,
                type: Type,
                page: apiPage,
                genreId: GenreId,
                countryId: CountryId,
                yearFrom: yearFrom,
                yearTo: yearTo,
                cancellationToken: cancellationToken);
            FiltersResult = AdaptFiltersResultForUi(FiltersResult, uiPage);
        }
        catch
        {
            FiltersResult = new FilmsByFiltersResult([], 0, 0, FiltersPage);
        }
    }

    private static int GetCatalogApiPage(int uiPage)
    {
        var safePage = uiPage < 1 ? 1 : uiPage;
        return (safePage + 1) / 2;
    }

    private static FilmsByFiltersResult AdaptFiltersResultForUi(FilmsByFiltersResult source, int uiPage)
    {
        var safePage = uiPage < 1 ? 1 : uiPage;
        var skip = safePage % 2 == 0 ? CatalogUiPageSize : 0;
        var items = source.Items.Skip(skip).Take(CatalogUiPageSize).ToList();
        var totalPages = source.Total <= 0
            ? 0
            : (int)Math.Ceiling(source.Total / (double)CatalogUiPageSize);

        return new FilmsByFiltersResult(items, source.Total, totalPages, safePage);
    }

    private static int? FindFilterIdByName(IEnumerable<CatalogFilterOption> items, string term)
    {
        return items.FirstOrDefault(i => i.Name.Contains(term, StringComparison.OrdinalIgnoreCase))?.Id;
    }

    public async Task<IActionResult> OnGetSearchSuggestionsAsync(string? q, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return new JsonResult(Array.Empty<object>());

        try
        {
            var result = await kinopoiskService.SearchByKeywordAsync(q.Trim(), 1, cancellationToken);
            var suggestions = result.Films.Take(5).Select(f =>
            {
                var title = !string.IsNullOrWhiteSpace(f.NameRu) ? f.NameRu : (f.NameEn ?? "");
                return new { id = f.FilmId, title, year = f.Year, url = $"/Details?id={f.FilmId}" };
            }).ToList();
            return new JsonResult(suggestions);
        }
        catch
        {
            return new JsonResult(Array.Empty<object>());
        }
    }
}
