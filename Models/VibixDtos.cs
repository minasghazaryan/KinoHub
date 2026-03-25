using System.Text.Json.Serialization;

namespace KinoHub.Web.Models;

public class VibixVideoDto
{
    [JsonPropertyName("name_rus")]
    public string? NameRus { get; set; }

    [JsonPropertyName("name_eng")]
    public string? NameEng { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("iframe_url")]
    public string? IframeUrl { get; set; }

    [JsonPropertyName("embed_code")]
    public string? EmbedCode { get; set; }

    [JsonPropertyName("quality")]
    public string? Quality { get; set; }

    [JsonPropertyName("voiceovers")]
    public List<VibixVoiceoverDto> Voiceovers { get; set; } = [];
}

public class VibixVoiceoverDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
