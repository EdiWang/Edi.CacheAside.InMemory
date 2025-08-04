using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace Edi.CacheAside.InMemory;

public class MemoryCacheAside(IMemoryCache memoryCache) : ICacheAside, IDisposable
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
    public ConcurrentDictionary<string, ConcurrentBag<string>> CachePartitions { get; } = new();

    private readonly IMemoryCache _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
    private bool _disposed;

    public TItem GetOrCreate<TItem>(string partition, string key, Func<ICacheEntry, TItem> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partition);
        if (string.IsNullOrWhiteSpace(key))
            return default;
        ArgumentNullException.ThrowIfNull(factory);

        ThrowIfDisposed();

        var cacheKey = BuildCacheKey(partition, key);
        AddToPartition(partition, key);
        return _memoryCache.GetOrCreate(cacheKey, factory);
    }

    public Task<TItem> GetOrCreateAsync<TItem>(string partition, string key, Func<ICacheEntry, Task<TItem>> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partition);
        if (string.IsNullOrWhiteSpace(key))
            return Task.FromResult(default(TItem));
        ArgumentNullException.ThrowIfNull(factory);

        ThrowIfDisposed();

        var cacheKey = BuildCacheKey(partition, key);
        AddToPartition(partition, key);
        return _memoryCache.GetOrCreateAsync(cacheKey, factory)!;
    }

    public void Clear()
    {
        ThrowIfDisposed();

        var allKeys = CachePartitions
            .SelectMany(kvp => kvp.Value.Select(key => BuildCacheKey(kvp.Key, key)))
            .ToList();

        foreach (var key in allKeys)
        {
            _memoryCache.Remove(key);
        }

        CachePartitions.Clear();
    }

    public void Remove(string partition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partition);
        ThrowIfDisposed();

        if (!CachePartitions.TryGetValue(partition, out var cacheKeys))
            return;

        foreach (var key in cacheKeys)
        {
            _memoryCache.Remove(BuildCacheKey(partition, key));
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

        // Note: We don't remove from CachePartitions here as ConcurrentBag doesn't support efficient removal
        // This is a trade-off for thread safety. The partition will be cleaned up on Clear() or Remove(partition)
    }

    private void AddToPartition(string partitionKey, string cacheKey)
    {
        // Use GetOrAdd for thread-safe initialization
        var partition = CachePartitions.GetOrAdd(partitionKey, _ => new ConcurrentBag<string>());

        // ConcurrentBag allows duplicates, but that's acceptable for this use case
        // as it's more performant than checking for existence
        partition.Add(cacheKey);
    }

    private static string BuildCacheKey(string partition, string key) => $"{partition}-{key}";

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