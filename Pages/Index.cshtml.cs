using KinoHub.Web.Data;
using KinoHub.Web.Models;
using KinoHub.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KinoHub.Web.Pages;

public class IndexModel(KinoContext dbContext, KinopoiskService kinopoiskService) : PageModel
{
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
    public string Order { get; set; } = "RATING";
    public string Type { get; set; } = "ALL";
    public IList<Genre> Genres { get; set; } = [];
    public IList<Country> Countries { get; set; } = [];

    public async Task OnGetAsync(string? q, string? collection, int? genreId, int? countryId, string? order, string? type, int page = 1, int filtersPage = 0, CancellationToken cancellationToken = default)
    {
        SearchQuery = q;
        Collection = collection;
        SearchPage = page < 1 ? 1 : page;
        CollectionPage = page < 1 ? 1 : page;
        GenreId = genreId;
        CountryId = countryId;
        Order = order ?? "RATING";
        Type = type ?? "ALL";
        FiltersPage = filtersPage < 1 ? 1 : filtersPage;

        Genres = await dbContext.Genres.AsNoTracking().Where(g => g.Name != null && g.Name != "").OrderBy(g => g.Name).ToListAsync(cancellationToken);
        Countries = await dbContext.Countries.AsNoTracking().Where(c => c.Name != null && c.Name != "").OrderBy(c => c.Name).ToListAsync(cancellationToken);

        // Always load top movies for the carousel (shown on main, novinki, seriali, top 250, catalog).
        var carouselQuery = dbContext.Movies.AsNoTracking()
            .OrderByDescending(m => m.Rating ?? 0)
            .ThenByDescending(m => m.Id);
        Movies = await carouselQuery.ToListAsync(cancellationToken);

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
            try
            {
                CollectionResult = await kinopoiskService.GetCollectionAsync(collection.Trim(), CollectionPage, cancellationToken);
            }
            catch
            {
                CollectionResult = new KinopoiskCollectionPageResult([], 0, 0, CollectionPage);
            }
        }
        else if (genreId.HasValue || countryId.HasValue || filtersPage >= 1)
        {
            try
            {
                FiltersResult = await kinopoiskService.GetFilmsByFiltersAsync(
                    order: Order,
                    type: Type,
                    page: FiltersPage,
                    genreId: genreId,
                    countryId: countryId,
                    cancellationToken: cancellationToken);
            }
            catch
            {
                FiltersResult = new FilmsByFiltersResult([], 0, 0, FiltersPage);
            }
        }
    }
}
