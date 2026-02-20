using System.Text.Json.Serialization;

namespace KinoHub.Web.Models;

/// <summary>
/// Response from GET /api/v1/publisher/videos/kp/{kpId}.
/// API returns snake_case: id, name, name_rus, name_eng, name_original, type, year, kp_id, imdb_id,
/// kp_rating, kp_votes, imdb_rating, imdb_votes, iframe_url, embed_code, persons, voiceovers, tags,
/// poster_url, backdrop_url, duration, quality, genre[], country[], description, description_short, uploaded_at.
/// </summary>
public class VibixVideoDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("name_rus")]
    public string? NameRus { get; set; }

    [JsonPropertyName("name_eng")]
    public string? NameEng { get; set; }

    [JsonPropertyName("name_original")]
    public string? NameOriginal { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("kp_id")]
    public int KpId { get; set; }

    [JsonPropertyName("imdb_id")]
    public string? ImdbId { get; set; }

    [JsonPropertyName("kp_rating")]
    public double KpRating { get; set; }

    [JsonPropertyName("kp_votes")]
    public int KpVotes { get; set; }

    [JsonPropertyName("imdb_rating")]
    public double ImdbRating { get; set; }

    [JsonPropertyName("imdb_votes")]
    public int ImdbVotes { get; set; }

    [JsonPropertyName("iframe_url")]
    public string? IframeUrl { get; set; }

    [JsonPropertyName("embed_code")]
    public string? EmbedCode { get; set; }

    [JsonPropertyName("persons")]
    public List<VibixPersonDto> Persons { get; set; } = [];

    [JsonPropertyName("voiceovers")]
    public List<VibixVoiceoverDto> Voiceovers { get; set; } = [];

    [JsonPropertyName("tags")]
    public List<VibixTagDto> Tags { get; set; } = [];

    [JsonPropertyName("poster_url")]
    public string? PosterUrl { get; set; }

    [JsonPropertyName("backdrop_url")]
    public string? BackdropUrl { get; set; }

    [JsonPropertyName("duration")]
    public int Duration { get; set; }

    [JsonPropertyName("quality")]
    public string? Quality { get; set; }

    [JsonPropertyName("genre")]
    public List<string> Genre { get; set; } = [];

    [JsonPropertyName("country")]
    public List<string> Country { get; set; } = [];

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("description_short")]
    public string? DescriptionShort { get; set; }

    [JsonPropertyName("uploaded_at")]
    public string? UploadedAt { get; set; }
}

public class VibixPersonDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("name_eng")]
    public string? NameEng { get; set; }

    [JsonPropertyName("name_anyway")]
    public string? NameAnyway { get; set; }

    [JsonPropertyName("occupation")]
    public string? Occupation { get; set; }
}

public class VibixVoiceoverDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class VibixTagDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

/// <summary>
/// Response from GET /api/v1/publisher/videos/voiceovers.
/// </summary>
public class VibixVoiceoversResponseDto
{
    [JsonPropertyName("data")]
    public List<VibixVoiceoverDto> Data { get; set; } = [];

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
