using System.Net.Http.Json;
using System.Text.Json.Serialization;
using KinoHub.Web.Data;
using KinoHub.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace KinoHub.Web.Services;

/// <summary>
/// Uses the "Movie Database Alternative" API on RapidAPI.
/// X-RapidAPI-Key is read from configuration (e.g. user secrets: RapidApi:Key).
/// </summary>
public class RapidApiMovieService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    KinoContext dbContext,
    ILogger<RapidApiMovieService> logger)
{
    private const string RapidApiHost = "movie-database-imdb-alternative.p.rapidapi.com";

    /// <summary>
    /// Search by title and return matching results with their IMDb IDs.
    /// </summary>
    public async Task<IReadOnlyList<MovieSearchResult>> SearchByTitleAsync(string title, CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["RapidApi:Key"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("RapidAPI key is not configured. Set RapidApi:Key in user secrets or appsettings.");
            return [];
        }

        using var client = httpClientFactory.CreateClient("RapidApiMovie");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-RapidAPI-Key", apiKey);
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-RapidAPI-Host", RapidApiHost);

        var url = $"?s={Uri.EscapeDataString(title)}&r=json";
        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "RapidAPI Movie Database Alternative request failed");
            throw;
        }

        var root = await response.Content.ReadFromJsonAsync<RapidApiSearchResponse>(cancellationToken);
        if (root?.Search is null || root.Search.Count == 0)
        {
            logger.LogInformation("RapidAPI returned no results for search: {Title}", title);
            return [];
        }

        return root.Search
            .Where(s => !string.IsNullOrWhiteSpace(s.ImdbId))
            .Select(s => new MovieSearchResult(
                ImdbId: s.ImdbId!,
                Title: s.Title ?? "",
                Year: s.Year ?? "",
                PosterUrl: s.Poster))
            .ToList();
    }

    /// <summary>
    /// Get full movie details by IMDb ID. Returns null if not found.
    /// </summary>
    public async Task<MovieDetailsResult?> GetByImdbIdAsync(string imdbId, CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["RapidApi:Key"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("RapidAPI key is not configured.");
            return null;
        }

        using var client = httpClientFactory.CreateClient("RapidApiMovie");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-RapidAPI-Key", apiKey);
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-RapidAPI-Host", RapidApiHost);

        var url = $"?i={Uri.EscapeDataString(imdbId)}&r=json";
        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "RapidAPI get by IMDb ID failed for {ImdbId}", imdbId);
            throw;
        }

        var dto = await response.Content.ReadFromJsonAsync<RapidApiTitleResponse>(cancellationToken);
        if (dto is null || string.IsNullOrWhiteSpace(dto.ImdbId))
            return null;

        return new MovieDetailsResult(
            ImdbId: dto.ImdbId,
            Title: dto.Title ?? "",
            Description: dto.Plot ?? "",
            ReleaseYear: ParseYear(dto.Year),
            PosterPath: dto.Poster ?? "");
    }

    /// <summary>
    /// Add a movie to the database by IMDb ID if it does not already exist. Returns true if added.
    /// </summary>
    public async Task<bool> AddMovieByImdbIdAsync(string imdbId, CancellationToken cancellationToken = default)
    {
        var exists = await dbContext.Movies.AnyAsync(m => m.ImdbId == imdbId, cancellationToken);
        if (exists)
        {
            logger.LogInformation("Movie with ImdbId {ImdbId} already exists.", imdbId);
            return false;
        }

        var details = await GetByImdbIdAsync(imdbId, cancellationToken);
        if (details is null)
        {
            logger.LogWarning("Could not fetch details for ImdbId {ImdbId}.", imdbId);
            return false;
        }

        var movie = new Movie
        {
            ImdbId = details.ImdbId,
            Title = details.Title,
            Description = details.Description,
            ReleaseYear = details.ReleaseYear,
            PosterPath = details.PosterPath
        };
        await dbContext.Movies.AddAsync(movie, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Added movie {Title} ({ImdbId}) to database.", movie.Title, movie.ImdbId);
        return true;
    }

    private static string ParseYear(string? year)
    {
        if (string.IsNullOrWhiteSpace(year))
            return "";
        var digits = new string(year.Where(char.IsDigit).Take(4).ToArray());
        return digits.Length >= 4 ? digits : year;
    }

    private sealed class RapidApiSearchResponse
    {
        public List<RapidApiSearchItem>? Search { get; set; }
    }

    private sealed class RapidApiSearchItem
    {
        public string? Title { get; set; }
        public string? Year { get; set; }
        [JsonPropertyName("imdbID")]
        public string? ImdbId { get; set; }
        public string? Poster { get; set; }
    }

    private sealed class RapidApiTitleResponse
    {
        [JsonPropertyName("imdbID")]
        public string? ImdbId { get; set; }
        public string? Title { get; set; }
        public string? Plot { get; set; }
        public string? Year { get; set; }
        public string? Poster { get; set; }
    }
}

public record MovieSearchResult(string ImdbId, string Title, string Year, string? PosterUrl);

public record MovieDetailsResult(string ImdbId, string Title, string Description, string ReleaseYear, string PosterPath);
