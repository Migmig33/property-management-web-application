//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Web;
//using System.Runtime.Caching;
//namespace WebApplication1.Helper
//{
//    public static class CacheHelper
//    {
//        private static readonly MemoryCache _cache = MemoryCache.Default;

//        public static T GetOrAdd<T>(string key, int minutes, Func<T> load)
//        {
//            if (_cache.Contains(key))

//                return (T)_cache.Get(key);

//                T data = load();
//                _cache.Set(key, data, DateTimeOffset.Now.AddMinutes(minutes));
//            return data;


//        }
//        public static void Remove(string key) => _cache.Remove(key);

//    }
//}


using System;
using System.Collections.Concurrent;
using System.Runtime.Caching;

namespace WebApplication1.Helper
{
    public static class CacheHelper
    {
        private static readonly MemoryCache _cache =
            MemoryCache.Default;

        // Separate locks for separate cache keys
        private static readonly ConcurrentDictionary<string, object>
            _keyLocks = new ConcurrentDictionary<string, object>();

        private static object GetKeyLock(string key)
        {
            return _keyLocks.GetOrAdd(
                key,
                k => new object()
            );
        }

        public static T GetOrAdd<T>(
            string key,
            int minutes,
            Func<T> load)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key is required.");

            if (load == null)
                throw new ArgumentNullException("load");

            // 1. Check cache before acquiring lock
            object cached = _cache.Get(key);

            if (cached != null)
                return (T)cached;

            // 2. Only one request can rebuild this cache key
            lock (GetKeyLock(key))
            {
                // 3. Check again because another request
                // may have already populated the cache
                cached = _cache.Get(key);

                if (cached != null)
                    return (T)cached;

                // 4. Execute database loading once
                T data = load();

                // 5. Cache successfully returned data
                _cache.Set(
                    key,
                    data,
                    DateTimeOffset.UtcNow.AddMinutes(
                        Math.Max(1, minutes)
                    )
                );

                return data;
            }
        }

        public static void Remove(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            lock (GetKeyLock(key))
            {
                _cache.Remove(key);
            }
        }
    }
}
