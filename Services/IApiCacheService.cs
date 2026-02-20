namespace KinoHub.Web.Services;

/// <summary>Cache for external API responses. Key pattern: api:{provider}:{id}.</summary>
public interface IApiCacheService
{
    /// <summary>Gets a value from cache or creates it via the factory. Uses sliding (10 min) and absolute (30 min) expiration.</summary>
    Task<T?> GetOrCreateAsync<T>(string provider, string id, Func<CancellationToken, Task<T?>> factory, CancellationToken cancellationToken = default) where T : class;
}
