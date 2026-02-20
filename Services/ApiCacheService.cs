using Microsoft.Extensions.Caching.Memory;

namespace KinoHub.Web.Services;

public class ApiCacheService(IMemoryCache cache) : IApiCacheService
{
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan AbsoluteExpiration = TimeSpan.FromMinutes(30);

    public async Task<T?> GetOrCreateAsync<T>(string provider, string id, Func<CancellationToken, Task<T?>> factory, CancellationToken cancellationToken = default) where T : class
    {
        var key = $"api:{provider}:{id}";

        if (cache.TryGetValue(key, out T? cached))
            return cached;

        var value = await factory(cancellationToken);

        var options = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(SlidingExpiration)
            .SetAbsoluteExpiration(AbsoluteExpiration);

        cache.Set(key, value, options);
        return value;
    }
}
