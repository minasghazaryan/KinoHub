using System.Net.Http.Json;
using KinoHub.Web.Models;

namespace KinoHub.Web.Services;

public class VibixService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
{
    private const string DefaultBaseUrl = "https://vibix.org";

    /// <summary>
    /// Fetches video player data by Kinopoisk ID from Vibix API.
    /// Returns null if not found or on error.
    /// </summary>
    public async Task<VibixVideoDto?> GetVideoByKpIdAsync(int kpId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Vibix");
        var baseUrl = configuration["Vibix:BaseUrl"]?.TrimEnd('/') ?? DefaultBaseUrl;
        var url = $"{baseUrl}/api/v1/publisher/videos/kp/{kpId}";
        try
        {
            var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;
            var dto = await response.Content.ReadFromJsonAsync<VibixVideoDto>(cancellationToken);
            return dto;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Fetches the list of voiceovers from GET /api/v1/publisher/videos/voiceovers.
    /// </summary>
    public async Task<VibixVoiceoversResponseDto?> GetVoiceoversAsync(CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Vibix");
        var baseUrl = configuration["Vibix:BaseUrl"]?.TrimEnd('/') ?? DefaultBaseUrl;
        var url = $"{baseUrl}/api/v1/publisher/videos/voiceovers";
        try
        {
            var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;
            var dto = await response.Content.ReadFromJsonAsync<VibixVoiceoversResponseDto>(cancellationToken);
            return dto;
        }
        catch
        {
            return null;
        }
    }
}
