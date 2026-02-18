using System.Text.Json;
using System.Text.Json.Serialization;

namespace KinoHub.Web.Models;

/// <summary>Allows year to be number or string in JSON (e.g. series vs films).</summary>
internal sealed class YearFlexibleConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var n)) return n;
        if (reader.TokenType == JsonTokenType.String && int.TryParse(reader.GetString(), out var parsed)) return parsed;
        return null;
    }
    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (value.HasValue) writer.WriteNumberValue(value.Value);
        else writer.WriteNullValue();
    }
}

/// <summary>Allows rating to be number, string, or null (e.g. "99%" for unreleased).</summary>
internal sealed class RatingFlexibleConverter : JsonConverter<double>
{
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return 0;
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetDouble(out var d)) return d;
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (string.IsNullOrEmpty(s)) return 0;
            var num = new string(s.Where(c => char.IsDigit(c) || c == '.').ToArray());
            return double.TryParse(num, out var v) ? v : 0;
        }
        return 0;
    }
    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);
}

/// <summary>
/// Response from GET /api/v2.2/films/{id}
/// </summary>
public class KinopoiskFilmDetailsDto
{
    [JsonPropertyName("kinopoiskId")]
    public int KinopoiskId { get; set; }

    [JsonPropertyName("nameRu")]
    public string? NameRu { get; set; }

    [JsonPropertyName("nameEn")]
    public string? NameEn { get; set; }

    [JsonPropertyName("nameOriginal")]
    public string? NameOriginal { get; set; }

    [JsonPropertyName("posterUrl")]
    public string? PosterUrl { get; set; }

    [JsonPropertyName("posterUrlPreview")]
    public string? PosterUrlPreview { get; set; }

    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("shortDescription")]
    public string? ShortDescription { get; set; }

    [JsonPropertyName("ratingKinopoisk")]
    [JsonConverter(typeof(RatingFlexibleConverter))]
    public double RatingKinopoisk { get; set; }

    [JsonPropertyName("ratingKinopoiskVoteCount")]
    public int RatingKinopoiskVoteCount { get; set; }

    [JsonPropertyName("ratingImdb")]
    [JsonConverter(typeof(RatingFlexibleConverter))]
    public double RatingImdb { get; set; }

    [JsonPropertyName("ratingImdbVoteCount")]
    public int RatingImdbVoteCount { get; set; }

    [JsonPropertyName("filmLength")]
    public int? FilmLength { get; set; }

    [JsonPropertyName("countries")]
    public List<KinopoiskCountryDto> Countries { get; set; } = [];

    [JsonPropertyName("genres")]
    public List<KinopoiskGenreDto> Genres { get; set; } = [];
}

/// <summary>
/// Collection item from GET /api/v2.2/films/collections (items[] element).
/// </summary>
public class KinopoiskFilmItemDto
{
    [JsonPropertyName("kinopoiskId")]
    public int KinopoiskId { get; set; }

    [JsonPropertyName("nameRu")]
    public string? NameRu { get; set; }

    [JsonPropertyName("nameEn")]
    public string? NameEn { get; set; }

    [JsonPropertyName("nameOriginal")]
    public string? NameOriginal { get; set; }

    [JsonPropertyName("countries")]
    public List<KinopoiskCountryDto> Countries { get; set; } = [];

    [JsonPropertyName("genres")]
    public List<KinopoiskGenreDto> Genres { get; set; } = [];

    [JsonPropertyName("ratingKinopoisk")]
    [JsonConverter(typeof(RatingFlexibleConverter))]
    public double RatingKinopoisk { get; set; }

    [JsonPropertyName("ratingImdb")]
    [JsonConverter(typeof(RatingFlexibleConverter))]
    public double RatingImdb { get; set; }

    [JsonPropertyName("year")]
    [JsonConverter(typeof(YearFlexibleConverter))]
    public int? Year { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("posterUrl")]
    public string? PosterUrl { get; set; }

    [JsonPropertyName("posterUrlPreview")]
    public string? PosterUrlPreview { get; set; }
}

public class KinopoiskCountryDto
{
    [JsonPropertyName("country")]
    public string? Country { get; set; }
}

public class KinopoiskGenreDto
{
    [JsonPropertyName("genre")]
    public string? Genre { get; set; }
}

/// <summary>
/// Wrapper for collections endpoint response (items array, optional pagination).
/// </summary>
public class KinopoiskCollectionResponseDto
{
    [JsonPropertyName("items")]
    public List<KinopoiskFilmItemDto>? Items { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }
}

/// <summary>
/// Known collection types for /api/v2.2/films/collections (type parameter).
/// </summary>
public static class KinopoiskCollectionType
{
    public const string TopPopularAll = "TOP_POPULAR_ALL";
    public const string TopPopularMovies = "TOP_POPULAR_MOVIES";
    public const string Top250TvShows = "TOP_250_TV_SHOWS";
    public const string Top250Movies = "TOP_250_MOVIES";
    public const string VampireTheme = "VAMPIRE_THEME";
    public const string ComicsTheme = "COMICS_THEME";
    public const string ClosesReleases = "CLOSES_RELEASES";
    public const string Family = "FAMILY";
    public const string OskarWinners2021 = "OSKAR_WINNERS_2021";
    public const string LoveTheme = "LOVE_THEME";
    public const string ZombieTheme = "ZOMBIE_THEME";
    public const string CatastropheTheme = "CATASTROPHE_THEME";
    public const string KidsAnimationTheme = "KIDS_ANIMATION_THEME";
    public const string PopularSeries = "POPULAR_SERIES";

    /// <summary>All supported type values for dropdowns.</summary>
    public static IReadOnlyList<(string Value, string Label)> All { get; } =
    [
        (TopPopularAll, "Top popular (all)"),
        (TopPopularMovies, "Top popular (movies)"),
        (Top250Movies, "Top 250 movies"),
        (Top250TvShows, "Top 250 TV shows"),
        (PopularSeries, "Popular series"),
        (ClosesReleases, "Coming soon"),
        (Family, "Family"),
        (OskarWinners2021, "Oscar winners 2021"),
        (LoveTheme, "Love theme"),
        (VampireTheme, "Vampire theme"),
        (ComicsTheme, "Comics theme"),
        (ZombieTheme, "Zombie theme"),
        (CatastropheTheme, "Catastrophe theme"),
        (KidsAnimationTheme, "Kids animation"),
    ];
}

/// <summary>
/// Result of a single collections API page (items + pagination info).
/// </summary>
public record KinopoiskCollectionPageResult(
    IReadOnlyList<KinopoiskFilmItemDto> Items,
    int Total,
    int TotalPages,
    int Page
);

/// <summary>
/// Response from GET /api/v2.1/films/search-by-keyword?keyword={keyword}&amp;page={page}.
/// </summary>
public class KinopoiskSearchByKeywordResponseDto
{
    [JsonPropertyName("keyword")]
    public string? Keyword { get; set; }

    [JsonPropertyName("pagesCount")]
    public int PagesCount { get; set; }

    [JsonPropertyName("searchFilmsCountResult")]
    public int SearchFilmsCountResult { get; set; }

    [JsonPropertyName("films")]
    public List<KinopoiskSearchFilmDto> Films { get; set; } = [];
}

/// <summary>
/// Film item in search-by-keyword response (films[] element).
/// </summary>
public class KinopoiskSearchFilmDto
{
    [JsonPropertyName("filmId")]
    public int FilmId { get; set; }

    [JsonPropertyName("nameRu")]
    public string? NameRu { get; set; }

    [JsonPropertyName("nameEn")]
    public string? NameEn { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("year")]
    public string? Year { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("filmLength")]
    public string? FilmLength { get; set; }

    [JsonPropertyName("countries")]
    public List<KinopoiskCountryDto> Countries { get; set; } = [];

    [JsonPropertyName("genres")]
    public List<KinopoiskGenreDto> Genres { get; set; } = [];

    [JsonPropertyName("rating")]
    public string? Rating { get; set; }

    [JsonPropertyName("ratingVoteCount")]
    public int RatingVoteCount { get; set; }

    [JsonPropertyName("posterUrl")]
    public string? PosterUrl { get; set; }

    [JsonPropertyName("posterUrlPreview")]
    public string? PosterUrlPreview { get; set; }
}

/// <summary>
/// Result of search-by-keyword (films + pagination and count).
/// </summary>
public record KinopoiskSearchByKeywordResult(
    IReadOnlyList<KinopoiskSearchFilmDto> Films,
    int SearchFilmsCountResult,
    int PagesCount,
    string Keyword,
    int Page
);

/// <summary>
/// Episode data from GET /api/v2.2/films/{id}/seasons
/// </summary>
public class KinopoiskEpisodeDto
{
    [JsonPropertyName("seasonNumber")]
    public int SeasonNumber { get; set; }

    [JsonPropertyName("episodeNumber")]
    public int EpisodeNumber { get; set; }

    [JsonPropertyName("nameRu")]
    public string? NameRu { get; set; }

    [JsonPropertyName("nameEn")]
    public string? NameEn { get; set; }

    [JsonPropertyName("synopsis")]
    public string? Synopsis { get; set; }

    [JsonPropertyName("releaseDate")]
    public string? ReleaseDate { get; set; }
}

/// <summary>
/// Season data from GET /api/v2.2/films/{id}/seasons
/// </summary>
public class KinopoiskSeasonDto
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("episodes")]
    public List<KinopoiskEpisodeDto> Episodes { get; set; } = [];
}

/// <summary>
/// Response from GET /api/v2.2/films/{id}/seasons
/// </summary>
public class KinopoiskSeasonsResponseDto
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("items")]
    public List<KinopoiskSeasonDto> Items { get; set; } = [];
}
