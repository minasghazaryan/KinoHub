using KinoHub.Web.Models;
using KinoHub.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KinoHub.Web.Pages.Admin;

public class IndexModel(RapidApiMovieService movieService, KinopoiskService kinopoiskService, ILogger<IndexModel> logger) : PageModel
{
    public string? Message { get; set; }
    public bool IsSuccess { get; set; }
    public string? SearchQuery { get; set; }
    public IReadOnlyList<MovieSearchResult> SearchResults { get; set; } = [];
    public string SelectedCollectionType { get; set; } = KinopoiskCollectionType.Top250Movies;
    public int CollectionPage { get; set; } = 1;

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnGetSearchAsync(string? q, CancellationToken cancellationToken)
    {
        SearchQuery = q;
        if (string.IsNullOrWhiteSpace(q))
        {
            Message = "Enter a title to search.";
            return Page();
        }

        try
        {
            SearchResults = await movieService.SearchByTitleAsync(q.Trim(), cancellationToken);
            if (SearchResults.Count == 0)
                Message = "No movies found. Try a different title.";
            return Page();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RapidAPI search failed");
            Message = "Search failed. Ensure RapidApi:Key is set in user secrets.";
            IsSuccess = false;
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAddByImdbIdAsync(string imdbId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imdbId))
        {
            Message = "No IMDb ID provided.";
            IsSuccess = false;
            return Page();
        }

        try
        {
            var added = await movieService.AddMovieByImdbIdAsync(imdbId.Trim(), cancellationToken);
            Message = added
                ? $"Added movie (IMDb {imdbId}) to the database."
                : $"Movie with IMDb ID {imdbId} is already in the database.";
            IsSuccess = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Add movie by IMDb ID failed");
            Message = "Failed to add movie. Check logs and RapidApi:Key.";
            IsSuccess = false;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSyncKinopoiskAsync(CancellationToken cancellationToken)
    {
        try
        {
            var count = await kinopoiskService.SyncTrendingToDatabaseAsync(cancellationToken);
            Message = $"Synced {count} movies from Kinopoisk TOP 250 (page 1).";
            IsSuccess = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Kinopoisk sync failed");
            Message = "Kinopoisk sync failed. Check logs and KinopoiskApiKey in appsettings.";
            IsSuccess = false;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSyncKinopoiskCollectionAsync(string? collectionType, int page, CancellationToken cancellationToken)
    {
        var type = string.IsNullOrWhiteSpace(collectionType) ? KinopoiskCollectionType.Top250Movies : collectionType.Trim();
        var pageNum = page < 1 ? 1 : page;

        try
        {
            var count = await kinopoiskService.SyncCollectionToDatabaseAsync(type, pageNum, cancellationToken);
            Message = $"Synced {count} movies from Kinopoisk collection '{type}' (page {pageNum}). Up to 20 per page.";
            IsSuccess = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Kinopoisk collection sync failed");
            Message = "Collection sync failed. Check logs and KinopoiskApiKey.";
            IsSuccess = false;
        }

        SelectedCollectionType = type;
        CollectionPage = pageNum;
        return Page();
    }
}
