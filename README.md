# MemoryCacheAside

A thread-safe implementation of the Cache-Aside pattern using `IMemoryCache` with support for partitioned cache management.

## Overview

`MemoryCacheAside` provides a wrapper around `IMemoryCache` that implements the Cache-Aside pattern with the added benefit of cache partitioning. This allows for organized cache management where related cache entries can be grouped together and cleared as a unit.

## Features

- **Partitioned Caching**: Organize cache entries into logical partitions for better management
- **Thread-Safe Operations**: Built with concurrent data structures for safe multi-threaded access
- **Bulk Operations**: Clear entire partitions or the entire cache at once
- **Async Support**: Provides both synchronous and asynchronous cache operations
- **IDisposable**: Properly cleans up resources when disposed

## Cache Partitioning

The class maintains a mapping of cache partitions to handle scenarios where you need to clear related cache entries together. This addresses limitations in `IMemoryCache` where you can't easily enumerate or group cache keys.

**Example Partition Structure:**

```
* Key               | Value
* ------------------+--------------------------------------
* PostCountCategory | { "<guid>", "<guid>", ... }
* Post              | { "<guid>", "<guid>", "<guid"> ... }
* General           | { "avatar", ... }
```

## Usage Examples

### Basic Usage

```csharp
// Dependency injection setup services.AddMemoryCache(); services.AddScoped<ICacheAside, MemoryCacheAside>();

public BlogService(ICacheAside cache)
{
    _cache = cache;
}

public async Task<Post> GetPostAsync(Guid postId)
{
    return await _cache.GetOrCreateAsync("Post", postId.ToString(), async entry =>
    {
        entry.SlidingExpiration = TimeSpan.FromMinutes(30);
        return await _repository.GetPostAsync(postId);
    });
}

public void InvalidatePostCache(Guid postId)
{
    _cache.Remove("Post", postId.ToString());
}

public void InvalidateAllPosts()
{
    _cache.Remove("Post");
}

```

### Partitioned Cache Management

```csharp
// Cache different types of data in separate partitions 
var userProfile = _cache.GetOrCreate("UserProfile", userId, entry => 
{ 
    entry.AbsoluteExpirationRelativeToNow TimeSpan.FromHours(1); 
    return GetUserProfileFromDatabase(userId); 
});

var userSettings = _cache.GetOrCreate("UserSettings", userId, entry => 
{ 
    entry.SlidingExpiration = TimeSpan.FromMinutes(15); 
    return GetUserSettingsFromDatabase(userId); 
});

// Clear all user profile data but keep settings 
_cache.Remove("UserProfile");

// Clear everything 
_cache.Clear();
```

## Thread Safety

The class uses `ConcurrentDictionary` and `ConcurrentBag` for thread-safe partition management. All operations are safe to call from multiple threads concurrently.

## Performance Considerations

- **ConcurrentBag Duplicates**: The partition tracking may contain duplicate keys for performance reasons. This is acceptable as `ConcurrentBag` prioritizes performance over uniqueness.
- **Memory Usage**: Partition metadata is kept in memory until explicitly cleared.
- **Key Removal**: Individual key removal from partitions is not performed for efficiency, relying on partition-level or full cache clearing.
