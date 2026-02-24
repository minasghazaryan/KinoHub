using KinoHub.Web.Data;
using KinoHub.Web.Models;
using KinoHub.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KinoHub.Web.Pages;

/// <summary>Single item for the Подборка carousel (from FeaturedCarousel or Movie).</summary>
public record CarouselItem(int? KinopoiskId, string PosterPath, string NameRu, string Title, string ReleaseYear, double? Rating);

public class IndexModel(KinoContext dbContext, KinopoiskService kinopoiskService) : PageModel
{
    /// <summary>Items for the Подборка carousel. From admin-configured list if any; otherwise top movies from DB.</summary>
    public IReadOnlyList<CarouselItem> CarouselItems { get; set; } = [];
    public IList<Movie> Movies { get; set; } = [];
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
    public IList<Genre> Genres { get; set; } = [];
    public IList<Country> Countries { get; set; } = [];
    /// <summary>Admin-configured films for the Кинопремьеры section. Only these are shown; no API call.</summary>
    public IList<FeaturedPremiere> FeaturedPremieres { get; set; } = [];

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

        Genres = await dbContext.Genres.AsNoTracking().Where(g => g.Name != null && g.Name != "").OrderBy(g => g.Name).ToListAsync(cancellationToken);
        Countries = await dbContext.Countries.AsNoTracking().Where(c => c.Name != null && c.Name != "").OrderBy(c => c.Name).ToListAsync(cancellationToken);

        FeaturedPremieres = await dbContext.FeaturedPremieres.AsNoTracking().OrderBy(f => f.DisplayOrder).ToListAsync(cancellationToken);

        // Carousel: admin-configured list (Подборка) or fallback to top movies from DB.
        var featuredCarousel = await dbContext.FeaturedCarousels.AsNoTracking().OrderBy(f => f.DisplayOrder).ToListAsync(cancellationToken);
        if (featuredCarousel.Count > 0)
        {
            CarouselItems = featuredCarousel.Select(f =>
            {
                var poster = f.PosterUrl ?? "";
                if (!string.IsNullOrEmpty(poster) && !poster.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    poster = "https://kinopoiskapiunofficial.tech" + (poster.StartsWith("/") ? poster : "/" + poster);
                return new CarouselItem(f.KinopoiskId, poster, f.NameRu ?? "", f.NameEn ?? "", f.ReleaseYear ?? "", f.Rating);
            }).ToList();
        }
        else
        {
            var carouselQuery = dbContext.Movies.AsNoTracking()
                .OrderByDescending(m => m.Rating ?? 0)
                .ThenByDescending(m => m.Id);
            var movies = await carouselQuery.ToListAsync(cancellationToken);
            Movies = movies;
            CarouselItems = movies.Select(m => new CarouselItem(
                m.KinopoiskId,
                m.PosterPath ?? "",
                m.NameRu ?? "",
                m.Title ?? "",
                m.ReleaseYear ?? "",
                m.Rating
            )).ToList();
        }

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
        }
        else if (!string.IsNullOrWhiteSpace(collection))
        {
            var col = collection.Trim();
            // Anime and Мультфильмы use filters API by genre (collections API doesn't support these).
            if (col == "ANIME" || col == "KIDS_ANIMATION_THEME")
            {
                GenreId = col == "ANIME" ? 24 : 18; // аниме / мультфильм from Kinopoisk genre IDs
                FiltersPage = CollectionPage;
                try
                {
                    FiltersResult = await kinopoiskService.GetFilmsByFiltersAsync(
                        order: Order,
                        type: Type,
                        page: FiltersPage,
                        genreId: GenreId,
                        yearFrom: Year ?? 1000,
                        yearTo: Year ?? 3000,
                        cancellationToken: cancellationToken);
                }
                catch
                {
                    FiltersResult = new FilmsByFiltersResult([], 0, 0, FiltersPage);
                }
            }
            else
            {
                try
                {
                    CollectionResult = await kinopoiskService.GetCollectionAsync(col, CollectionPage, cancellationToken);
                }
                catch
                {
                    CollectionResult = new KinopoiskCollectionPageResult([], 0, 0, CollectionPage);
                }
            }
        }
        else
        {
            // Main page / catalog: try DB first (synced movies with genres/countries); fall back to API if tables missing
            try
            {
                FiltersResult = await kinopoiskService.GetMoviesFromDbAsync(
                    genreId: genreId,
                    countryId: countryId,
                    year: Year,
                    order: Order,
                    page: FiltersPage,
                    cancellationToken: cancellationToken);
            }
            catch
            {
                try
                {
                    var yearFrom = Year.HasValue ? Year.Value : 1000;
                    var yearTo = Year.HasValue ? Year.Value : 3000;
                    FiltersResult = await kinopoiskService.GetFilmsByFiltersAsync(
                        order: Order,
                        type: Type,
                        page: FiltersPage,
                        genreId: genreId,
                        countryId: countryId,
                        yearFrom: yearFrom,
                        yearTo: yearTo,
                        cancellationToken: cancellationToken);
                }
                catch
                {
                    FiltersResult = new FilmsByFiltersResult([], 0, 0, FiltersPage);
                }
            }
        }
    }

    /// <summary>Returns first 5 search suggestions as JSON for the header autocomplete (handler=SearchSuggestions).</summary>
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
                return new { id = f.FilmId, title, year = f.Year };
            }).ToList();
            return new JsonResult(suggestions);
        }
        catch
        {
            return new JsonResult(Array.Empty<object>());
        }
    }
}
