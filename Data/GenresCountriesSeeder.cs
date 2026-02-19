using System.Text.Json;
using System.Text.Json.Serialization;
using KinoHub.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace KinoHub.Web.Data;

public static class GenresCountriesSeeder
{
    private sealed class SeedGenreItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("genre")]
        public string? Genre { get; set; }
    }

    private sealed class SeedCountryItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("country")]
        public string? Country { get; set; }
    }

    private sealed class SeedFile
    {
        [JsonPropertyName("genres")]
        public List<SeedGenreItem>? Genres { get; set; }
        [JsonPropertyName("countries")]
        public List<SeedCountryItem>? Countries { get; set; }
    }

    public static async Task SeedAsync(KinoContext context, string contentRootPath, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(contentRootPath, "Data", "Seed", "genres_countries.json");
        if (!File.Exists(path))
            return;

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var data = JsonSerializer.Deserialize<SeedFile>(json);
        if (data?.Genres == null && data?.Countries == null)
            return;

        if (data.Genres != null && data.Genres.Count > 0 && !await context.Genres.AnyAsync(cancellationToken))
        {
            var genres = data.Genres
                .Select(g => new Genre { Id = g.Id, Name = g.Genre ?? "" })
                .ToList();
            await context.Genres.AddRangeAsync(genres, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        if (data.Countries != null && data.Countries.Count > 0 && !await context.Countries.AnyAsync(cancellationToken))
        {
            var countries = data.Countries
                .Select(c => new Country { Id = c.Id, Name = c.Country ?? "" })
                .ToList();
            await context.Countries.AddRangeAsync(countries, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
