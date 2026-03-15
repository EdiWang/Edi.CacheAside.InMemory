using Microsoft.Extensions.Caching.Memory;
using Moq;
using System.Collections.Concurrent;
using Xunit;

namespace Edi.CacheAside.InMemory.Tests;

public class MemoryCacheAsideTests : IDisposable
{
    private readonly Mock<IMemoryCache> _memoryCacheMock;
    private readonly MemoryCacheAside _cacheAside;
    private readonly ConcurrentDictionary<object, object> _cacheStorage;

    public MemoryCacheAsideTests()
    {
        _memoryCacheMock = new Mock<IMemoryCache>();
        _cacheStorage = new ConcurrentDictionary<object, object>();

        // Mock TryGetValue for cache lookups
        _memoryCacheMock.Setup(x => x.TryGetValue(It.IsAny<object>(), out It.Ref<object>.IsAny))
            .Returns(new TryGetValueCallback((object key, out object value) =>
            {
                return _cacheStorage.TryGetValue(key, out value);
            }));

        // Mock CreateEntry for cache creation
        _memoryCacheMock.Setup(x => x.CreateEntry(It.IsAny<object>()))
            .Returns<object>(key =>
            {
                var mockEntry = new Mock<ICacheEntry>();
                
                // Setup Key as read-only property
                mockEntry.SetupGet(e => e.Key).Returns(key);
                
                // Setup Value property
                mockEntry.SetupProperty(e => e.Value);
                
                // Setup other common properties that might be accessed
                mockEntry.SetupProperty(e => e.AbsoluteExpiration);
                mockEntry.SetupProperty(e => e.AbsoluteExpirationRelativeToNow);
                mockEntry.SetupProperty(e => e.SlidingExpiration);
                mockEntry.SetupProperty(e => e.Priority);
                mockEntry.SetupProperty(e => e.Size);

                // Setup PostEvictionCallbacks as a real list for SetOptions support
                mockEntry.SetupGet(e => e.PostEvictionCallbacks)
                    .Returns(new List<PostEvictionCallbackRegistration>());

                // When Value is set, store it in our dictionary
                mockEntry.SetupSet(e => e.Value = It.IsAny<object>())
                    .Callback<object>(value => _cacheStorage.TryAdd(key, value));

                return mockEntry.Object;
            });

        // Mock Remove for cache removal
        _memoryCacheMock.Setup(x => x.Remove(It.IsAny<object>()))
            .Callback<object>(key => _cacheStorage.TryRemove(key, out _));

        _cacheAside = new MemoryCacheAside(_memoryCacheMock.Object);
    }

    // Delegate for TryGetValue callback
    private delegate bool TryGetValueCallback(object key, out object value);

    public void Dispose()
    {
        _cacheAside?.Dispose();
    }

    [Fact]
    public void Constructor_WithNullMemoryCache_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MemoryCacheAside(null!));
    }

    [Fact]
    public void GetOrCreate_WithValidParameters_ReturnsValue()
    {
        // Arrange
        const string partition = "test-partition";
        const string key = "test-key";
        const string expectedValue = "test-value";

        // Act
        var result = _cacheAside.GetOrCreate(partition, key, () => expectedValue);

        // Assert
        Assert.Equal(expectedValue, result);
        Assert.True(_cacheAside.CachePartitions.ContainsKey(partition));
        Assert.True(_cacheAside.CachePartitions[partition].ContainsKey(key));
    }

    [Theory]
    [InlineData(null)]
    public void GetOrCreate_WithInvalidPartition_ThrowsArgumentNullException(string partition)
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _cacheAside.GetOrCreate(partition, "key", () => "value"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void GetOrCreate_WithInvalidPartition_ThrowsArgumentException(string partition)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _cacheAside.GetOrCreate(partition, "key", () => "value"));
    }

    [Theory]
    [InlineData(null)]
    public void GetOrCreate_WithNullKey_ThrowsArgumentNullException(string key)
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _cacheAside.GetOrCreate<string>("partition", key, () => "value"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void GetOrCreate_WithEmptyOrWhitespaceKey_ThrowsArgumentException(string key)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _cacheAside.GetOrCreate<string>("partition", key, () => "value"));
    }

    [Fact]
    public void GetOrCreate_WithNullFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _cacheAside.GetOrCreate<string>("partition", "key", (Func<string>)null!));
    }

    [Fact]
    public void GetOrCreate_CalledTwiceWithSameKey_ReturnsCachedValue()
    {
        // Arrange
        const string partition = "test-partition";
        const string key = "test-key";
        var callCount = 0;

        // Act
        var result1 = _cacheAside.GetOrCreate(partition, key, () => $"value-{++callCount}");
        var result2 = _cacheAside.GetOrCreate(partition, key, () => $"value-{++callCount}");

        // Assert
        Assert.Equal("value-1", result1);
        Assert.Equal("value-1", result2);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetOrCreateAsync_WithValidParameters_ReturnsValue()
    {
        // Arrange
        const string partition = "test-partition";
        const string key = "test-key";
        const string expectedValue = "test-value";

        // Act
        var result = await _cacheAside.GetOrCreateAsync(partition, key, () => Task.FromResult(expectedValue));

        // Assert
        Assert.Equal(expectedValue, result);
        Assert.True(_cacheAside.CachePartitions.ContainsKey(partition));
        Assert.True(_cacheAside.CachePartitions[partition].ContainsKey(key));
    }

    [Theory]
    [InlineData(null)]
    public async Task GetOrCreateAsync_WithInvalidPartition_ThrowsArgumentNullException(string partition)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _cacheAside.GetOrCreateAsync(partition, "key", () => Task.FromResult("value")));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetOrCreateAsync_WithInvalidPartition_ThrowsArgumentException(string partition)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _cacheAside.GetOrCreateAsync(partition, "key", () => Task.FromResult("value")));
    }

    [Theory]
    [InlineData(null)]
    public async Task GetOrCreateAsync_WithNullKey_ThrowsArgumentNullException(string key)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _cacheAside.GetOrCreateAsync<string>("partition", key, () => Task.FromResult("value")));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetOrCreateAsync_WithEmptyOrWhitespaceKey_ThrowsArgumentException(string key)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _cacheAside.GetOrCreateAsync<string>("partition", key, () => Task.FromResult("value")));
    }

    [Fact]
    public async Task GetOrCreateAsync_WithNullFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _cacheAside.GetOrCreateAsync<string>("partition", "key", (Func<Task<string>>)null!));
    }

    [Fact]
    public async Task GetOrCreateAsync_CalledTwiceWithSameKey_ReturnsCachedValue()
    {
        // Arrange
        const string partition = "test-partition";
        const string key = "test-key";
        var callCount = 0;

        // Act
        var result1 = await _cacheAside.GetOrCreateAsync(partition, key, () => Task.FromResult($"value-{++callCount}"));
        var result2 = await _cacheAside.GetOrCreateAsync(partition, key, () => Task.FromResult($"value-{++callCount}"));

        // Assert
        Assert.Equal("value-1", result1);
        Assert.Equal("value-1", result2);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Clear_RemovesAllCacheEntriesAndPartitions()
    {
        // Arrange
        _cacheAside.GetOrCreate("partition1", "key1", () => "value1");
        _cacheAside.GetOrCreate("partition1", "key2", () => "value2");
        _cacheAside.GetOrCreate("partition2", "key3", () => "value3");

        // Act
        _cacheAside.Clear();

        // Assert
        Assert.Empty(_cacheAside.CachePartitions);
        _memoryCacheMock.Verify(x => x.Remove("partition1::key1"), Times.Once);
        _memoryCacheMock.Verify(x => x.Remove("partition1::key2"), Times.Once);
        _memoryCacheMock.Verify(x => x.Remove("partition2::key3"), Times.Once);
    }

    [Fact]
    public void Remove_WithValidPartition_RemovesAllKeysInPartition()
    {
        // Arrange
        const string partition = "test-partition";
        _cacheAside.GetOrCreate(partition, "key1", () => "value1");
        _cacheAside.GetOrCreate(partition, "key2", () => "value2");
        _cacheAside.GetOrCreate("other-partition", "key3", () => "value3");

        // Act
        _cacheAside.Remove(partition);

        // Assert
        Assert.False(_cacheAside.CachePartitions.ContainsKey(partition));
        Assert.True(_cacheAside.CachePartitions.ContainsKey("other-partition"));
        _memoryCacheMock.Verify(x => x.Remove("test-partition::key1"), Times.Once);
        _memoryCacheMock.Verify(x => x.Remove("test-partition::key2"), Times.Once);
        _memoryCacheMock.Verify(x => x.Remove("other-partition::key3"), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    public void Remove_WithInvalidPartition_ThrowsArgumentNullException(string partition)
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _cacheAside.Remove(partition));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Remove_WithInvalidPartition_ThrowsArgumentException(string partition)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _cacheAside.Remove(partition));
    }

    [Fact]
    public void Remove_WithNonExistentPartition_DoesNotThrow()
    {
        // Act & Assert
        _cacheAside.Remove("non-existent");
    }

    [Fact]
    public void Remove_WithPartitionAndKey_RemovesSpecificCacheEntry()
    {
        // Arrange
        const string partition = "test-partition";
        const string key = "test-key";
        _cacheAside.GetOrCreate(partition, key, () => "value");
        _cacheAside.GetOrCreate(partition, "other-key", () => "other-value");

        // Act
        _cacheAside.Remove(partition, key);

        // Assert
        _memoryCacheMock.Verify(x => x.Remove("test-partition::test-key"), Times.Once);
        _memoryCacheMock.Verify(x => x.Remove("test-partition::other-key"), Times.Never);
        Assert.True(_cacheAside.CachePartitions.ContainsKey(partition));
        Assert.False(_cacheAside.CachePartitions[partition].ContainsKey(key));
        Assert.True(_cacheAside.CachePartitions[partition].ContainsKey("other-key"));
    }

    [Theory]
    [InlineData(null, "key")]
    [InlineData("partition", null)]
    public void Remove_WithInvalidPartitionOrKey_ThrowsArgumentNullException(string partition, string key)
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _cacheAside.Remove(partition, key));
    }

    [Theory]
    [InlineData("", "key")]
    [InlineData(" ", "key")]
    [InlineData("partition", "")]
    [InlineData("partition", " ")]
    public void Remove_WithInvalidPartitionOrKey_ThrowsArgumentException(string partition, string key)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _cacheAside.Remove(partition, key));
    }

    [Fact]
    public void CachePartitions_IsThreadSafe()
    {
        // Arrange
        const int taskCount = 10;
        const int operationsPerTask = 100;
        var tasks = new Task[taskCount];

        // Act
        for (int i = 0; i < taskCount; i++)
        {
            int taskIndex = i;
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < operationsPerTask; j++)
                {
                    _cacheAside.GetOrCreate($"partition-{taskIndex}", $"key-{j}", () => $"value-{taskIndex}-{j}");
                }
            });
        }

        Task.WaitAll(tasks);

        // Assert
        Assert.Equal(taskCount, _cacheAside.CachePartitions.Count);
        foreach (var partition in _cacheAside.CachePartitions)
        {
            Assert.Equal(operationsPerTask, partition.Value.Count);
        }
    }

    [Fact]
    public void Dispose_CallsClear()
    {
        // Arrange
        _cacheAside.GetOrCreate("partition", "key", () => "value");

        // Act
        _cacheAside.Dispose();

        // Assert
        Assert.Empty(_cacheAside.CachePartitions);
        _memoryCacheMock.Verify(x => x.Remove("partition::key"), Times.Once);
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_OnlyClearsOnce()
    {
        // Arrange
        _cacheAside.GetOrCreate("partition", "key", () => "value");

        // Act
        _cacheAside.Dispose();
        _cacheAside.Dispose();

        // Assert
        _memoryCacheMock.Verify(x => x.Remove("partition::key"), Times.Once);
    }

    [Fact]
    public void GetOrCreate_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _cacheAside.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => _cacheAside.GetOrCreate("partition", "key", () => "value"));
    }

    [Fact]
    public async Task GetOrCreateAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _cacheAside.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _cacheAside.GetOrCreateAsync("partition", "key", () => Task.FromResult("value")));
    }

    [Fact]
    public void Clear_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _cacheAside.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => _cacheAside.Clear());
    }

    [Fact]
    public void Remove_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _cacheAside.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => _cacheAside.Remove("partition"));
    }

    [Fact]
    public void BuildCacheKey_CreatesCorrectFormat()
    {
        // This tests the internal behavior through public methods
        // Arrange & Act
        _cacheAside.GetOrCreate("test-partition", "test-key", () => "value");

        // Assert
        _memoryCacheMock.Verify(x => x.CreateEntry("test-partition::test-key"), Times.Once);
    }

    [Fact]
    public void GetOrCreate_ConcurrentCallsForSameKey_FactoryExecutedOnce()
    {
        // Arrange - use a real MemoryCache so stampede protection is fully exercised
        using var realCache = new MemoryCache(new MemoryCacheOptions());
        using var cacheAside = new MemoryCacheAside(realCache);

        var factoryCallCount = 0;
        var barrier = new Barrier(10);

        // Act
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            barrier.SignalAndWait();
            return cacheAside.GetOrCreate("partition", "key", () =>
            {
                Interlocked.Increment(ref factoryCallCount);
                Thread.Sleep(50); // simulate work
                return "value";
            });
        })).ToArray();

        Task.WaitAll(tasks);

        // Assert
        Assert.Equal(1, factoryCallCount);
        Assert.All(tasks, t => Assert.Equal("value", t.Result));
    }

    [Fact]
    public async Task GetOrCreateAsync_ConcurrentCallsForSameKey_FactoryExecutedOnce()
    {
        // Arrange
        using var realCache = new MemoryCache(new MemoryCacheOptions());
        using var cacheAside = new MemoryCacheAside(realCache);

        var factoryCallCount = 0;
        var barrier = new Barrier(10);

        // Act
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
        {
            barrier.SignalAndWait();
            return await cacheAside.GetOrCreateAsync("partition", "key", async () =>
            {
                Interlocked.Increment(ref factoryCallCount);
                await Task.Delay(50);
                return "value";
            });
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(1, factoryCallCount);
        Assert.All(tasks, t => Assert.Equal("value", t.Result));
    }

    [Fact]
    public void GetOrCreate_WithEviction_RemovesFromPartition()
    {
        // Arrange - use real MemoryCache with tiny expiration to trigger eviction
        using var realCache = new MemoryCache(new MemoryCacheOptions());
        using var cacheAside = new MemoryCacheAside(realCache);

        cacheAside.GetOrCreate("partition", "key", () => "value", TimeSpan.FromMilliseconds(50));
        Assert.True(cacheAside.CachePartitions.ContainsKey("partition"));

        // Act - wait for expiration and trigger compaction
        Thread.Sleep(100);
        realCache.TryGetValue("partition::key", out _); // triggers lazy expiration check

        // Allow eviction callback to execute
        Thread.Sleep(50);

        // Assert
        Assert.False(cacheAside.CachePartitions.ContainsKey("partition"));
    }

    [Fact]
    public void GetOrCreate_WithPerCallExpiration_AppliesExpiration()
    {
        // Arrange
        using var realCache = new MemoryCache(new MemoryCacheOptions());
        using var cacheAside = new MemoryCacheAside(realCache);

        // Act
        cacheAside.GetOrCreate("partition", "key", () => "value", TimeSpan.FromMilliseconds(50));

        // Assert - value should exist immediately
        var result = cacheAside.GetOrCreate("partition", "key", () => "other-value");
        Assert.Equal("value", result);

        // Wait for expiration
        Thread.Sleep(100);

        // Value should be gone, factory should produce new value
        var newResult = cacheAside.GetOrCreate("partition", "key", () => "new-value", TimeSpan.FromMinutes(5));
        Assert.Equal("new-value", newResult);
    }

    [Fact]
    public void GetOrCreate_WithDefaultExpiration_AppliesWhenNoPerCallExpiration()
    {
        // Arrange
        using var realCache = new MemoryCache(new MemoryCacheOptions());
        var options = Microsoft.Extensions.Options.Options.Create(new CacheAsideOptions
        {
            DefaultExpiration = TimeSpan.FromMilliseconds(50)
        });
        using var cacheAside = new MemoryCacheAside(realCache, options);

        // Act
        cacheAside.GetOrCreate("partition", "key", () => "value");

        // Assert - value should exist immediately
        Assert.Equal("value", cacheAside.GetOrCreate("partition", "key", () => "other"));

        // Wait for default expiration
        Thread.Sleep(100);

        // Value should be expired
        var newResult = cacheAside.GetOrCreate("partition", "key", () => "new-value");
        Assert.Equal("new-value", newResult);
    }

    [Fact]
    public void GetOrCreate_PerCallExpirationOverridesDefault()
    {
        // Arrange
        using var realCache = new MemoryCache(new MemoryCacheOptions());
        var options = Microsoft.Extensions.Options.Options.Create(new CacheAsideOptions
        {
            DefaultExpiration = TimeSpan.FromMinutes(30)
        });
        using var cacheAside = new MemoryCacheAside(realCache, options);

        // Act - use very short per-call expiration that overrides the long default
        cacheAside.GetOrCreate("partition", "key", () => "value", TimeSpan.FromMilliseconds(50));

        // Wait for per-call expiration (much shorter than default)
        Thread.Sleep(100);

        // Assert - value should be expired despite long default
        var result = cacheAside.GetOrCreate("partition", "key", () => "new-value");
        Assert.Equal("new-value", result);
    }
}