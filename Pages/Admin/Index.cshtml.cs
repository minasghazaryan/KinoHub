using KinoHub.Web.Data;
using KinoHub.Web.Models;
using KinoHub.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KinoHub.Web.Pages.Admin;

public class IndexModel(KinoContext dbContext, KinopoiskService kinopoiskService, ILogger<IndexModel> logger) : PageModel
{
    public string? Message { get; set; }
    public bool IsSuccess { get; set; }

    /// <summary>Catalog sync: main endpoint GET /api/v2.2/films (order, type, rating, year, page).</summary>
    public string CatalogOrder { get; set; } = "RATING";
    public string CatalogType { get; set; } = "ALL";
    public int CatalogYearFrom { get; set; } = 1900;
    public int CatalogYearTo { get; set; } = 2030;
    /// <summary>Genre filter: null = all. Kinopoisk genre ID (matches Genres table Id).</summary>
    public int? CatalogGenreId { get; set; }
    public int CatalogPage { get; set; } = 1;
    /// <summary>Total pages from API (max 20 for 400 items). 0 = unknown.</summary>
    public int CatalogTotalPages { get; set; }
    /// <summary>Genres for catalog filter dropdown (from DB, seeded from Kinopoisk).</summary>
    public IList<Genre> CatalogGenres { get; set; } = [];

    public string SelectedCollectionType { get; set; } = KinopoiskCollectionType.Top250Movies;
    public int CollectionPage { get; set; } = 1;
    public int CollectionTotalPages { get; set; }
    public IList<FeaturedPremiere> FeaturedPremieres { get; set; } = [];
    /// <summary>Premieres from API for the selected year/month (browse new coming films).</summary>
    public IReadOnlyList<KinopoiskPremiereItemDto> ApiPremieres { get; set; } = [];
    public int PremieresYear { get; set; }
    public int PremieresMonth { get; set; }
    /// <summary>Kinopoisk IDs that are already on the index (for showing Add vs Remove).</summary>
    public HashSet<int> FeaturedKinopoiskIds { get; set; } = [];

    /// <summary>Search query for Подборка carousel (admin).</summary>
    public string? CarouselSearchQuery { get; set; }
    /// <summary>Search results for adding to Подборка.</summary>
    public KinopoiskSearchByKeywordResult? CarouselSearchResult { get; set; }
    /// <summary>Films currently in the Подборка carousel.</summary>
    public IList<FeaturedCarousel> FeaturedCarousels { get; set; } = [];
    public HashSet<int> CarouselKinopoiskIds { get; set; } = [];

    private static readonly string[] MonthNames = ["JANUARY", "FEBRUARY", "MARCH", "APRIL", "MAY", "JUNE", "JULY", "AUGUST", "SEPTEMBER", "OCTOBER", "NOVEMBER", "DECEMBER"];

    public async Task OnGetAsync(int? premieresYear, int? premieresMonth, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        PremieresYear = premieresYear ?? now.Year;
        PremieresMonth = premieresMonth is >= 1 and <= 12 ? premieresMonth.Value : now.Month;

        FeaturedPremieres = await dbContext.FeaturedPremieres
            .AsNoTracking()
            .OrderBy(f => f.DisplayOrder)
            .ToListAsync(cancellationToken);
        FeaturedKinopoiskIds = FeaturedPremieres.Select(f => f.KinopoiskId).ToHashSet();

        FeaturedCarousels = await dbContext.FeaturedCarousels
            .AsNoTracking()
            .OrderBy(f => f.DisplayOrder)
            .ToListAsync(cancellationToken);
        CarouselKinopoiskIds = FeaturedCarousels.Select(f => f.KinopoiskId).ToHashSet();

        CatalogGenres = await dbContext.Genres.AsNoTracking().OrderBy(g => g.Name).ToListAsync(cancellationToken);

        var monthStr = MonthNames[PremieresMonth - 1];
        try
        {
            var response = await kinopoiskService.GetPremieresAsync(PremieresYear, monthStr, cancellationToken);
            var raw = response?.Items ?? [];
            ApiPremieres = raw.Where(i => i.Year == PremieresYear).ToList();
        }
        catch
        {
            ApiPremieres = [];
        }
    }

    /// <summary>Main sync: one page from GET /api/v2.2/films (order, type, rating, year, page).</summary>
    public async Task<IActionResult> OnPostSyncCatalogAsync(string? order, string? type, int? yearFrom, int? yearTo, int? genreId, int page, CancellationToken cancellationToken)
    {
        var orderVal = string.IsNullOrWhiteSpace(order) ? "RATING" : order.Trim();
        var typeVal = string.IsNullOrWhiteSpace(type) ? "ALL" : type.Trim();
        var yearFromVal = yearFrom ?? 1900;
        var yearToVal = yearTo ?? 2030;
        if (yearFromVal > yearToVal) (yearFromVal, yearToVal) = (yearToVal, yearFromVal);
        var genreIdVal = genreId is > 0 ? genreId : null;
        var pageNum = page < 1 ? 1 : page;
        try
        {
            var result = await kinopoiskService.GetFilmsByFiltersAsync(orderVal, typeVal, yearFrom: yearFromVal, yearTo: yearToVal, page: pageNum, genreId: genreIdVal, cancellationToken: cancellationToken);
            var count = await kinopoiskService.SaveOrUpdateMoviesAsync(result.Items, cancellationToken);
            CatalogTotalPages = result.TotalPages;
            CatalogOrder = orderVal;
            CatalogType = typeVal;
            CatalogYearFrom = yearFromVal;
            CatalogYearTo = yearToVal;
            CatalogGenreId = genreIdVal;
            CatalogPage = pageNum;
            Message = CatalogTotalPages > 0
                ? $"Synced {count} movies (page {pageNum} of {CatalogTotalPages})."
                : $"Synced {count} movies (page {pageNum}).";
            IsSuccess = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Catalog sync failed");
            Message = "Catalog sync failed. Check logs and KinopoiskApiKey.";
            IsSuccess = false;
        }
        await LoadPageDataAsync(null, null, cancellationToken);
        return Page();
    }

    /// <summary>Sync all pages from GET /api/v2.2/films (order=RATING, type=ALL, etc.).</summary>
    public async Task<IActionResult> OnPostSyncAllCatalogAsync(string? order, string? type, int? yearFrom, int? yearTo, int? genreId, CancellationToken cancellationToken)
    {
        var orderVal = string.IsNullOrWhiteSpace(order) ? "RATING" : order.Trim();
        var typeVal = string.IsNullOrWhiteSpace(type) ? "ALL" : type.Trim();
        var yearFromVal = yearFrom ?? 1900;
        var yearToVal = yearTo ?? 2030;
        if (yearFromVal > yearToVal) (yearFromVal, yearToVal) = (yearToVal, yearFromVal);
        var genreIdVal = genreId is > 0 ? genreId : null;
        try
        {
            var (totalSynced, totalPages) = await kinopoiskService.SyncAllFilmsByFiltersToDatabaseAsync(order: orderVal, type: typeVal, yearFrom: yearFromVal, yearTo: yearToVal, genreId: genreIdVal, cancellationToken: cancellationToken);
            CatalogOrder = orderVal;
            CatalogType = typeVal;
            CatalogYearFrom = yearFromVal;
            CatalogYearTo = yearToVal;
            CatalogGenreId = genreIdVal;
            CatalogPage = 1;
            CatalogTotalPages = totalPages;
            Message = $"Synced all {totalPages} page(s): {totalSynced} films saved/updated.";
            IsSuccess = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sync all catalog failed");
            Message = "Sync all failed. Check logs and KinopoiskApiKey.";
            IsSuccess = false;
        }
        await LoadPageDataAsync(null, null, cancellationToken);
        return Page();
    }

    /// <summary>Fetches page 1 to get total pages for catalog (no sync).</summary>
    public async Task<IActionResult> OnPostCheckCatalogPagesAsync(string? order, string? type, int? yearFrom, int? yearTo, int? genreId, CancellationToken cancellationToken)
    {
        var orderVal = string.IsNullOrWhiteSpace(order) ? "RATING" : order.Trim();
        var typeVal = string.IsNullOrWhiteSpace(type) ? "ALL" : type.Trim();
        var yearFromVal = yearFrom ?? 1900;
        var yearToVal = yearTo ?? 2030;
        if (yearFromVal > yearToVal) (yearFromVal, yearToVal) = (yearToVal, yearFromVal);
        var genreIdVal = genreId is > 0 ? genreId : null;
        try
        {
            var result = await kinopoiskService.GetFilmsByFiltersAsync(orderVal, typeVal, yearFrom: yearFromVal, yearTo: yearToVal, page: 1, genreId: genreIdVal, cancellationToken: cancellationToken);
            CatalogTotalPages = result.TotalPages;
            CatalogOrder = orderVal;
            CatalogType = typeVal;
            CatalogYearFrom = yearFromVal;
            CatalogYearTo = yearToVal;
            CatalogGenreId = genreIdVal;
            CatalogPage = 1;
            Message = result.TotalPages > 0
                ? $"Catalog has {CatalogTotalPages} page(s). Up to 20 films per page, max 400 total."
                : "Could not get page count.";
            IsSuccess = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Check catalog pages failed");
            Message = "Check failed. Check KinopoiskApiKey.";
            IsSuccess = false;
        }
        await LoadPageDataAsync(null, null, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSyncKinopoiskCollectionAsync(string? collectionType, int page, CancellationToken cancellationToken)
    {
        var type = string.IsNullOrWhiteSpace(collectionType) ? KinopoiskCollectionType.Top250Movies : collectionType.Trim();
        var pageNum = page < 1 ? 1 : page;

        try
        {
            var result = await kinopoiskService.GetCollectionAsync(type, pageNum, cancellationToken);
            var count = await kinopoiskService.SaveOrUpdateMoviesAsync(result.Items, cancellationToken);
            CollectionTotalPages = result.TotalPages;
            Message = result.TotalPages > 0
                ? $"Synced {count} movies (page {pageNum} of {result.TotalPages})."
                : $"Synced {count} movies from collection (page {pageNum}).";
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
        await LoadPageDataAsync(null, null, cancellationToken);
        return Page();
    }

    /// <summary>Syncs all pages of the selected collection (fetches page count, then loops 1..totalPages).</summary>
    public async Task<IActionResult> OnPostSyncAllCollectionAsync(string? collectionType, CancellationToken cancellationToken)
    {
        var type = string.IsNullOrWhiteSpace(collectionType) ? KinopoiskCollectionType.Top250Movies : collectionType.Trim();
        try
        {
            var first = await kinopoiskService.GetCollectionAsync(type, 1, cancellationToken);
            var totalPages = first.TotalPages;
            if (totalPages < 1)
            {
                Message = "Collection has no pages (API returned 0).";
                IsSuccess = false;
                SelectedCollectionType = type;
                await LoadPageDataAsync(null, null, cancellationToken);
                return Page();
            }
            var totalSynced = 0;
            for (var page = 1; page <= totalPages; page++)
            {
                var count = await kinopoiskService.SyncCollectionToDatabaseAsync(type, page, cancellationToken);
                totalSynced += count;
                logger.LogInformation("Synced collection {Type} page {Page}/{TotalPages}, +{Count} films.", type, page, totalPages, count);
            }
            CollectionTotalPages = totalPages;
            SelectedCollectionType = type;
            CollectionPage = 1;
            Message = $"Synced all {totalPages} page(s): {totalSynced} films saved/updated.";
            IsSuccess = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sync all collection failed");
            Message = "Sync all failed. Check logs and KinopoiskApiKey.";
            IsSuccess = false;
            SelectedCollectionType = type;
        }
        await LoadPageDataAsync(null, null, cancellationToken);
        return Page();
    }

    /// <summary>Fetches page 1 to get total pages for the selected collection (no sync).</summary>
    public async Task<IActionResult> OnPostCheckCollectionPagesAsync(string? collectionType, CancellationToken cancellationToken)
    {
        var type = string.IsNullOrWhiteSpace(collectionType) ? KinopoiskCollectionType.Top250Movies : collectionType.Trim();
        try
        {
            var result = await kinopoiskService.GetCollectionAsync(type, 1, cancellationToken);
            CollectionTotalPages = result.TotalPages;
            SelectedCollectionType = type;
            CollectionPage = 1;
            Message = CollectionTotalPages > 0
                ? $"Collection has {CollectionTotalPages} page(s). Up to 20 films per page."
                : "Could not get page count (API may have returned no data).";
            IsSuccess = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Check collection pages failed");
            Message = "Failed to get page count. Check KinopoiskApiKey.";
            IsSuccess = false;
        }
        await LoadPageDataAsync(null, null, cancellationToken);
        return Page();
    }

    /// <summary>Add from API premieres list (no extra API call).</summary>
    public async Task<IActionResult> OnPostAddFeaturedPremiereFromApiAsync(int? kinopoiskId, string? nameRu, string? nameEn, string? posterUrl, int? year, string? premiereRu, int? premieresYear, int? premieresMonth, CancellationToken cancellationToken)
    {
        if (!kinopoiskId.HasValue || kinopoiskId.Value <= 0)
        {
            await LoadPageDataAsync(premieresYear, premieresMonth, cancellationToken);
            return Page();
        }
        if (await dbContext.FeaturedPremieres.AnyAsync(f => f.KinopoiskId == kinopoiskId.Value, cancellationToken))
        {
            Message = "Already on index.";
            IsSuccess = false;
            await LoadPageDataAsync(premieresYear, premieresMonth, cancellationToken);
            return Page();
        }
        string? poster = posterUrl;
        if (!string.IsNullOrEmpty(poster) && !poster.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            poster = "https://kinopoiskapiunofficial.tech" + poster;
        var fp = new FeaturedPremiere
        {
            KinopoiskId = kinopoiskId.Value,
            DisplayOrder = (await dbContext.FeaturedPremieres.MaxAsync(f => (int?)f.DisplayOrder, cancellationToken) ?? -1) + 1,
            NameRu = nameRu,
            NameEn = nameEn,
            PosterUrl = poster,
            Year = year,
            PremiereRu = premiereRu
        };
        dbContext.FeaturedPremieres.Add(fp);
        await dbContext.SaveChangesAsync(cancellationToken);
        Message = $"Added «{fp.NameRu ?? fp.NameEn ?? fp.KinopoiskId.ToString()}» to index.";
        IsSuccess = true;
        await LoadPageDataAsync(premieresYear, premieresMonth, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostRemoveFeaturedPremiereAsync(int? id, int? premieresYear, int? premieresMonth, CancellationToken cancellationToken)
    {
        if (id.HasValue)
        {
            var fp = await dbContext.FeaturedPremieres.FindAsync([id.Value], cancellationToken);
            if (fp != null)
            {
                dbContext.FeaturedPremieres.Remove(fp);
                await dbContext.SaveChangesAsync(cancellationToken);
                Message = "Removed from Кинопремьеры.";
                IsSuccess = true;
            }
        }
        await LoadPageDataAsync(premieresYear, premieresMonth, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostMoveFeaturedPremiereAsync(int? id, string? direction, int? premieresYear, int? premieresMonth, CancellationToken cancellationToken)
    {
        if (!id.HasValue || string.IsNullOrEmpty(direction))
        {
            await LoadPageDataAsync(premieresYear, premieresMonth, cancellationToken);
            return Page();
        }
        var list = await dbContext.FeaturedPremieres.OrderBy(f => f.DisplayOrder).ToListAsync(cancellationToken);
        var idx = list.FindIndex(f => f.Id == id.Value);
        if (idx < 0) { await LoadPageDataAsync(premieresYear, premieresMonth, cancellationToken); return Page(); }
        int swapIdx = direction.Equals("up", StringComparison.OrdinalIgnoreCase) ? idx - 1 : idx + 1;
        if (swapIdx < 0 || swapIdx >= list.Count) { await LoadPageDataAsync(premieresYear, premieresMonth, cancellationToken); return Page(); }
        var a = list[idx];
        var b = list[swapIdx];
        (a.DisplayOrder, b.DisplayOrder) = (b.DisplayOrder, a.DisplayOrder);
        await dbContext.SaveChangesAsync(cancellationToken);
        Message = "Order updated.";
        IsSuccess = true;
        await LoadPageDataAsync(premieresYear, premieresMonth, cancellationToken);
        return Page();
    }

    private async Task LoadPageDataAsync(int? premieresYear, int? premieresMonth, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        PremieresYear = premieresYear ?? now.Year;
        PremieresMonth = premieresMonth is >= 1 and <= 12 ? premieresMonth.Value : now.Month;

        FeaturedPremieres = await dbContext.FeaturedPremieres
            .AsNoTracking()
            .OrderBy(f => f.DisplayOrder)
            .ToListAsync(cancellationToken);
        FeaturedKinopoiskIds = FeaturedPremieres.Select(f => f.KinopoiskId).ToHashSet();

        FeaturedCarousels = await dbContext.FeaturedCarousels
            .AsNoTracking()
            .OrderBy(f => f.DisplayOrder)
            .ToListAsync(cancellationToken);
        CarouselKinopoiskIds = FeaturedCarousels.Select(f => f.KinopoiskId).ToHashSet();

        CatalogGenres = await dbContext.Genres.AsNoTracking().OrderBy(g => g.Name).ToListAsync(cancellationToken);

        var monthStr = MonthNames[PremieresMonth - 1];
        try
        {
            var response = await kinopoiskService.GetPremieresAsync(PremieresYear, monthStr, cancellationToken);
            var raw = response?.Items ?? [];
            ApiPremieres = raw.Where(i => i.Year == PremieresYear).ToList();
        }
        catch
        {
            ApiPremieres = [];
        }
    }

    public async Task<IActionResult> OnPostSearchCarouselAsync(string? carouselSearch, CancellationToken cancellationToken)
    {
        CarouselSearchQuery = carouselSearch?.Trim();
        if (string.IsNullOrWhiteSpace(CarouselSearchQuery))
        {
            Message = "Enter a search query.";
            IsSuccess = false;
            await LoadPageDataAsync(null, null, cancellationToken);
            return Page();
        }
        try
        {
            CarouselSearchResult = await kinopoiskService.SearchByKeywordAsync(CarouselSearchQuery, 1, cancellationToken);
            Message = null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Carousel search failed");
            Message = "Search failed. Check KinopoiskApiKey.";
            IsSuccess = false;
            CarouselSearchResult = null;
        }
        await LoadPageDataAsync(null, null, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAddToCarouselAsync(int? kinopoiskId, string? nameRu, string? nameEn, string? posterUrl, string? year, string? rating, CancellationToken cancellationToken)
    {
        if (!kinopoiskId.HasValue || kinopoiskId.Value <= 0)
        {
            await LoadPageDataAsync(null, null, cancellationToken);
            return Page();
        }
        if (await dbContext.FeaturedCarousels.AnyAsync(f => f.KinopoiskId == kinopoiskId.Value, cancellationToken))
        {
            Message = "Already in Подборка.";
            IsSuccess = false;
            await LoadPageDataAsync(null, null, cancellationToken);
            return Page();
        }
        double? ratingVal = null;
        if (!string.IsNullOrWhiteSpace(rating) && double.TryParse(rating.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var r))
            ratingVal = r;
        var poster = posterUrl ?? "";
        if (!string.IsNullOrEmpty(poster) && !poster.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            poster = "https://kinopoiskapiunofficial.tech" + (poster.StartsWith("/") ? poster : "/" + poster);
        var maxOrder = (await dbContext.FeaturedCarousels.MaxAsync(f => (int?)f.DisplayOrder, cancellationToken) ?? -1) + 1;
        dbContext.FeaturedCarousels.Add(new FeaturedCarousel
        {
            KinopoiskId = kinopoiskId.Value,
            DisplayOrder = maxOrder,
            NameRu = nameRu,
            NameEn = nameEn,
            PosterUrl = poster,
            ReleaseYear = year,
            Rating = ratingVal
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        Message = $"Added to Подборка.";
        IsSuccess = true;
        await LoadPageDataAsync(null, null, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostRemoveCarouselItemAsync(int? id, CancellationToken cancellationToken)
    {
        if (id.HasValue)
        {
            var item = await dbContext.FeaturedCarousels.FindAsync([id.Value], cancellationToken);
            if (item != null)
            {
                dbContext.FeaturedCarousels.Remove(item);
                await dbContext.SaveChangesAsync(cancellationToken);
                Message = "Removed from Подборка.";
                IsSuccess = true;
            }
        }
        await LoadPageDataAsync(null, null, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostMoveCarouselItemAsync(int? id, string? direction, CancellationToken cancellationToken)
    {
        if (!id.HasValue || string.IsNullOrEmpty(direction))
        {
            await LoadPageDataAsync(null, null, cancellationToken);
            return Page();
        }
        var list = await dbContext.FeaturedCarousels.OrderBy(f => f.DisplayOrder).ToListAsync(cancellationToken);
        var idx = list.FindIndex(f => f.Id == id.Value);
        if (idx < 0) { await LoadPageDataAsync(null, null, cancellationToken); return Page(); }
        int swapIdx = direction.Equals("up", StringComparison.OrdinalIgnoreCase) ? idx - 1 : idx + 1;
        if (swapIdx < 0 || swapIdx >= list.Count) { await LoadPageDataAsync(null, null, cancellationToken); return Page(); }
        var a = list[idx];
        var b = list[swapIdx];
        (a.DisplayOrder, b.DisplayOrder) = (b.DisplayOrder, a.DisplayOrder);
        await dbContext.SaveChangesAsync(cancellationToken);
        Message = "Order updated.";
        IsSuccess = true;
        await LoadPageDataAsync(null, null, cancellationToken);
        return Page();
    }
}
