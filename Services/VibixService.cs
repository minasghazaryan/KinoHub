using System.Net.Http.Json;
using KinoHub.Web.Models;

namespace KinoHub.Web.Services;

public class VibixService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
{
    private const string DefaultBaseUrl = "https://vibix.org";

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

            return await response.Content.ReadFromJsonAsync<VibixVideoDto>(cancellationToken);
        }
        catch
        {
            return null;
        }
    }
}
