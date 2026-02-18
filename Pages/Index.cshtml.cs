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
    public int SearchPage { get; set; } = 1;
    public int CollectionPage { get; set; } = 1;

    public async Task OnGetAsync(string? q, string? collection, int page = 1, CancellationToken cancellationToken = default)
    {
        SearchQuery = q;
        Collection = collection;
        SearchPage = page < 1 ? 1 : page;
        CollectionPage = page < 1 ? 1 : page;

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
        else
        {
            var query = dbContext.Movies.AsNoTracking();
            Movies = await query
                .OrderByDescending(m => m.Rating ?? 0)
                .ThenByDescending(m => m.Id)
                .ToListAsync(cancellationToken);
        }
    }
}
