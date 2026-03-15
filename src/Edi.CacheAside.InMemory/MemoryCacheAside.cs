using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Edi.CacheAside.InMemory;

public class MemoryCacheAside(IMemoryCache memoryCache, IOptions<CacheAsideOptions>? options = null) : ICacheAside
{
    /* Create Key-Value mapping for cache divisions to workaround
     * https://github.com/aspnet/Caching/issues/422
     * This blog will need cache keys for post ids or category ids and need to be cleared later
     * Key               | Value
     * ------------------+--------------------------------------
     * PostCountCategory | { "<guid>", "<guid>", ... }
     * Post              | { "<guid>", "<guid>", "<guid"> ... }
     * General           | { "avatar", ... }
     */
    internal ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> CachePartitions { get; } = new();

    private readonly IMemoryCache _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
    private readonly CacheAsideOptions _options = options?.Value ?? new CacheAsideOptions();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new();
    private bool _disposed;

    public TItem? GetOrCreate<TItem>(string partition, string key, Func<TItem> factory, TimeSpan? expiration = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partition);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        ThrowIfDisposed();

        var cacheKey = BuildCacheKey(partition, key);

        if (_memoryCache.TryGetValue(cacheKey, out TItem? cached))
        {
            return cached;
        }

        var semaphore = _keyLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        semaphore.Wait();
        try
        {
            if (_memoryCache.TryGetValue(cacheKey, out cached))
            {
                return cached;
            }

            var value = factory();
            SetCacheEntry(cacheKey, value, partition, key, expiration);
            AddToPartition(partition, key);
            return value;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<TItem?> GetOrCreateAsync<TItem>(string partition, string key, Func<Task<TItem>> factory, TimeSpan? expiration = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partition);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        ThrowIfDisposed();

        var cacheKey = BuildCacheKey(partition, key);

        if (_memoryCache.TryGetValue(cacheKey, out TItem? cached))
        {
            return cached;
        }

        var semaphore = _keyLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        try
        {
            if (_memoryCache.TryGetValue(cacheKey, out cached))
            {
                return cached;
            }

            var value = await factory();
            SetCacheEntry(cacheKey, value, partition, key, expiration);
            AddToPartition(partition, key);
            return value;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public void Clear()
    {
        ThrowIfDisposed();

        var allKeys = CachePartitions
            .SelectMany(kvp => kvp.Value.Keys.Select(key => BuildCacheKey(kvp.Key, key)))
            .ToList();

        foreach (var key in allKeys)
        {
            _memoryCache.Remove(key);
        }

        CachePartitions.Clear();
        _keyLocks.Clear();
    }

    public void Remove(string partition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partition);
        ThrowIfDisposed();

        if (!CachePartitions.TryGetValue(partition, out var cacheKeys))
            return;

        foreach (var key in cacheKeys.Keys)
        {
            var cacheKey = BuildCacheKey(partition, key);
            _memoryCache.Remove(cacheKey);
            _keyLocks.TryRemove(cacheKey, out _);
        }

        CachePartitions.TryRemove(partition, out _);
    }

    public void Remove(string partition, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partition);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ThrowIfDisposed();

        var cacheKey = BuildCacheKey(partition, key);
        _memoryCache.Remove(cacheKey);
        _keyLocks.TryRemove(cacheKey, out _);

        if (CachePartitions.TryGetValue(partition, out var keys))
        {
            keys.TryRemove(key, out _);

            if (keys.IsEmpty)
            {
                CachePartitions.TryRemove(partition, out _);
            }
        }
    }

    private void SetCacheEntry<TItem>(string cacheKey, TItem value, string partition, string key, TimeSpan? expiration)
    {
        var entryOptions = new MemoryCacheEntryOptions();

        var effectiveExpiration = expiration ?? _options.DefaultExpiration;
        if (effectiveExpiration.HasValue)
        {
            entryOptions.AbsoluteExpirationRelativeToNow = effectiveExpiration;
        }

        entryOptions.RegisterPostEvictionCallback((evictedKey, val, reason, state) =>
        {
            RemoveFromPartition(partition, key);
        });

        _memoryCache.Set(cacheKey, value, entryOptions);
    }

    private void AddToPartition(string partitionKey, string cacheKey)
    {
        var partition = CachePartitions.GetOrAdd(partitionKey, _ => new ConcurrentDictionary<string, byte>());
        partition.TryAdd(cacheKey, 0);
    }

    private void RemoveFromPartition(string partitionKey, string cacheKey)
    {
        if (CachePartitions.TryGetValue(partitionKey, out var partition))
        {
            partition.TryRemove(cacheKey, out _);

            if (partition.IsEmpty)
            {
                CachePartitions.TryRemove(partitionKey, out _);
            }
        }
    }

    private static string BuildCacheKey(string partition, string key) => $"{partition}::{key}";

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Clear();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}