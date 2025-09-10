using Microsoft.Extensions.Caching.Memory;

namespace MultiSportTracker.Services
{
    // Simple wrapper around IMemoryCache (useful for testing + central TTL)
    public class CacheService
    {
        private readonly IMemoryCache _cache;
        public CacheService(IMemoryCache cache) => _cache = cache;

        public bool TryGet<T>(string key, out T value)
        {
            if (_cache.TryGetValue(key, out object? obj) && obj is T t)
            {
                value = t;
                return true;
            }
            value = default!;
            return false;
        }

        public void Set<T>(string key, T value, int minutes = 5)
        {
            _cache.Set(key, value, TimeSpan.FromMinutes(minutes));
        }
    }
}
