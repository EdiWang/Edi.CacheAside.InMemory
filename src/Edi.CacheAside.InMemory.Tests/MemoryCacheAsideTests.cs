using Microsoft.Extensions.Caching.Memory;
using Moq;
using System.Collections.Concurrent;

namespace Edi.CacheAside.InMemory.Tests;

[TestFixture]
public class MemoryCacheAsideTests
{
    private Mock<IMemoryCache> _memoryCacheMock;
    private MemoryCacheAside _cacheAside;
    private ConcurrentDictionary<object, object> _cacheStorage;

    [SetUp]
    public void SetUp()
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

    [TearDown]
    public void TearDown()
    {
        _cacheAside?.Dispose();
    }

    [Test]
    public void Constructor_WithNullMemoryCache_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MemoryCacheAside(null!));
    }

    [Test]
    public void GetOrCreate_WithValidParameters_ReturnsValue()
    {
        // Arrange
        const string partition = "test-partition";
        const string key = "test-key";
        const string expectedValue = "test-value";

        // Act
        var result = _cacheAside.GetOrCreate(partition, key, _ => expectedValue);

        // Assert
        Assert.That(result, Is.EqualTo(expectedValue));
        Assert.That(_cacheAside.CachePartitions.ContainsKey(partition), Is.True);
        Assert.That(_cacheAside.CachePartitions[partition], Contains.Item(key));
    }

    [TestCase(null)]
    public void GetOrCreate_WithInvalidPartition_ArgumentNullException(string partition)
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _cacheAside.GetOrCreate(partition, "key", _ => "value"));
    }

    [TestCase("")]
    [TestCase(" ")]
    public void GetOrCreate_WithInvalidPartition_ThrowsArgumentException(string partition)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _cacheAside.GetOrCreate(partition, "key", _ => "value"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void GetOrCreate_WithInvalidKey_ReturnsDefault(string key)
    {
        // Act
        var result = _cacheAside.GetOrCreate<string>("partition", key, _ => "value");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void GetOrCreate_WithNullFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _cacheAside.GetOrCreate<string>("partition", "key", null!));
    }

    [Test]
    public void GetOrCreate_CalledTwiceWithSameKey_ReturnsCachedValue()
    {
        // Arrange
        const string partition = "test-partition";
        const string key = "test-key";
        var callCount = 0;

        // Act
        var result1 = _cacheAside.GetOrCreate(partition, key, _ => $"value-{++callCount}");
        var result2 = _cacheAside.GetOrCreate(partition, key, _ => $"value-{++callCount}");

        // Assert
        Assert.That(result1, Is.EqualTo("value-1"));
        Assert.That(result2, Is.EqualTo("value-1"));
        Assert.That(callCount, Is.EqualTo(1));
    }

    [Test]
    public async Task GetOrCreateAsync_WithValidParameters_ReturnsValue()
    {
        // Arrange
        const string partition = "test-partition";
        const string key = "test-key";
        const string expectedValue = "test-value";

        // Act
        var result = await _cacheAside.GetOrCreateAsync(partition, key, _ => Task.FromResult(expectedValue));

        // Assert
        Assert.That(result, Is.EqualTo(expectedValue));
        Assert.That(_cacheAside.CachePartitions.ContainsKey(partition), Is.True);
        Assert.That(_cacheAside.CachePartitions[partition], Contains.Item(key));
    }

    [TestCase(null)]
    public void GetOrCreateAsync_WithInvalidPartition_ThrowsArgumentNullException(string partition)
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(() => _cacheAside.GetOrCreateAsync(partition, "key", _ => Task.FromResult("value")));
    }

    [TestCase("")]
    [TestCase(" ")]
    public void GetOrCreateAsync_WithInvalidPartition_ThrowsArgumentException(string partition)
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => _cacheAside.GetOrCreateAsync(partition, "key", _ => Task.FromResult("value")));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public async Task GetOrCreateAsync_WithInvalidKey_ReturnsDefault(string key)
    {
        // Act
        var result = await _cacheAside.GetOrCreateAsync<string>("partition", key, _ => Task.FromResult("value"));

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void GetOrCreateAsync_WithNullFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(() => _cacheAside.GetOrCreateAsync<string>("partition", "key", null!));
    }

    [Test]
    public async Task GetOrCreateAsync_CalledTwiceWithSameKey_ReturnsCachedValue()
    {
        // Arrange
        const string partition = "test-partition";
        const string key = "test-key";
        var callCount = 0;

        // Act
        var result1 = await _cacheAside.GetOrCreateAsync(partition, key, _ => Task.FromResult($"value-{++callCount}"));
        var result2 = await _cacheAside.GetOrCreateAsync(partition, key, _ => Task.FromResult($"value-{++callCount}"));

        // Assert
        Assert.That(result1, Is.EqualTo("value-1"));
        Assert.That(result2, Is.EqualTo("value-1"));
        Assert.That(callCount, Is.EqualTo(1));
    }

    [Test]
    public void Clear_RemovesAllCacheEntriesAndPartitions()
    {
        // Arrange
        _cacheAside.GetOrCreate("partition1", "key1", _ => "value1");
        _cacheAside.GetOrCreate("partition1", "key2", _ => "value2");
        _cacheAside.GetOrCreate("partition2", "key3", _ => "value3");

        // Act
        _cacheAside.Clear();

        // Assert
        Assert.That(_cacheAside.CachePartitions, Is.Empty);
        _memoryCacheMock.Verify(x => x.Remove("partition1-key1"), Times.Once);
        _memoryCacheMock.Verify(x => x.Remove("partition1-key2"), Times.Once);
        _memoryCacheMock.Verify(x => x.Remove("partition2-key3"), Times.Once);
    }

    [Test]
    public void Remove_WithValidPartition_RemovesAllKeysInPartition()
    {
        // Arrange
        const string partition = "test-partition";
        _cacheAside.GetOrCreate(partition, "key1", _ => "value1");
        _cacheAside.GetOrCreate(partition, "key2", _ => "value2");
        _cacheAside.GetOrCreate("other-partition", "key3", _ => "value3");

        // Act
        _cacheAside.Remove(partition);

        // Assert
        Assert.That(_cacheAside.CachePartitions.ContainsKey(partition), Is.False);
        Assert.That(_cacheAside.CachePartitions.ContainsKey("other-partition"), Is.True);
        _memoryCacheMock.Verify(x => x.Remove("test-partition-key1"), Times.Once);
        _memoryCacheMock.Verify(x => x.Remove("test-partition-key2"), Times.Once);
        _memoryCacheMock.Verify(x => x.Remove("other-partition-key3"), Times.Never);
    }

    [TestCase(null)]
    public void Remove_WithInvalidPartition_ThrowsArgumentNullException(string partition)
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _cacheAside.Remove(partition));
    }

    [TestCase("")]
    [TestCase(" ")]
    public void Remove_WithInvalidPartition_ThrowsArgumentException(string partition)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _cacheAside.Remove(partition));
    }

    [Test]
    public void Remove_WithNonExistentPartition_DoesNotThrow()
    {
        // Act & Assert
        Assert.DoesNotThrow(() => _cacheAside.Remove("non-existent"));
    }

    [Test]
    public void Remove_WithPartitionAndKey_RemovesSpecificCacheEntry()
    {
        // Arrange
        const string partition = "test-partition";
        const string key = "test-key";
        _cacheAside.GetOrCreate(partition, key, _ => "value");
        _cacheAside.GetOrCreate(partition, "other-key", _ => "other-value");

        // Act
        _cacheAside.Remove(partition, key);

        // Assert
        _memoryCacheMock.Verify(x => x.Remove("test-partition-test-key"), Times.Once);
        _memoryCacheMock.Verify(x => x.Remove("test-partition-other-key"), Times.Never);
        // Note: CachePartitions still contains the partition as ConcurrentBag doesn't support efficient removal
        Assert.That(_cacheAside.CachePartitions.ContainsKey(partition), Is.True);
    }

    [TestCase(null, "key")]
    [TestCase("partition", null)]
    public void Remove_WithInvalidPartitionOrKey_ThrowsArgumentNullException(string partition, string key)
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _cacheAside.Remove(partition, key));
    }

    [TestCase("", "key")]
    [TestCase(" ", "key")]
    [TestCase("partition", "")]
    [TestCase("partition", " ")]
    public void Remove_WithInvalidPartitionOrKey_ThrowsArgumentException(string partition, string key)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _cacheAside.Remove(partition, key));
    }

    [Test]
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
                    _cacheAside.GetOrCreate($"partition-{taskIndex}", $"key-{j}", _ => $"value-{taskIndex}-{j}");
                }
            });
        }

        Task.WaitAll(tasks);

        // Assert
        Assert.That(_cacheAside.CachePartitions.Count, Is.EqualTo(taskCount));
        foreach (var partition in _cacheAside.CachePartitions)
        {
            Assert.That(partition.Value.Count, Is.EqualTo(operationsPerTask));
        }
    }

    [Test]
    public void Dispose_CallsClear()
    {
        // Arrange
        _cacheAside.GetOrCreate("partition", "key", _ => "value");

        // Act
        _cacheAside.Dispose();

        // Assert
        Assert.That(_cacheAside.CachePartitions, Is.Empty);
        _memoryCacheMock.Verify(x => x.Remove("partition-key"), Times.Once);
    }

    [Test]
    public void Dispose_CalledMultipleTimes_OnlyClearsOnce()
    {
        // Arrange
        _cacheAside.GetOrCreate("partition", "key", _ => "value");

        // Act
        _cacheAside.Dispose();
        _cacheAside.Dispose();

        // Assert
        _memoryCacheMock.Verify(x => x.Remove("partition-key"), Times.Once);
    }

    [Test]
    public void GetOrCreate_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _cacheAside.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => _cacheAside.GetOrCreate("partition", "key", _ => "value"));
    }

    [Test]
    public void GetOrCreateAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _cacheAside.Dispose();

        // Act & Assert
        Assert.ThrowsAsync<ObjectDisposedException>(() => _cacheAside.GetOrCreateAsync("partition", "key", _ => Task.FromResult("value")));
    }

    [Test]
    public void Clear_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _cacheAside.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => _cacheAside.Clear());
    }

    [Test]
    public void Remove_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _cacheAside.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => _cacheAside.Remove("partition"));
    }

    [Test]
    public void BuildCacheKey_CreatesCorrectFormat()
    {
        // This tests the internal behavior through public methods
        // Arrange & Act
        _cacheAside.GetOrCreate("test-partition", "test-key", _ => "value");

        // Assert
        _memoryCacheMock.Verify(x => x.CreateEntry("test-partition-test-key"), Times.Once);
    }
}