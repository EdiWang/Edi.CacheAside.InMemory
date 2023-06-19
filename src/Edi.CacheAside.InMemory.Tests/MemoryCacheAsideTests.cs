using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace Edi.CacheAside.InMemory.Tests
{
    [TestFixture]
    public class MemoryCacheAsideTests
    {
        [Test]
        public void GetOrCreate_NullKey_ReturnsDefault()
        {
            // Arrange
            var mockCache = new Mock<IMemoryCache>();
            var cacheAside = new MemoryCacheAside(mockCache.Object);

            // Act
            var result = cacheAside.GetOrCreate<string>("testPartition", null, (_) => "testValue");

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public async Task GetOrCreateAsync_NullKey_ReturnsDefault()
        {
            // Arrange
            var mockCache = new Mock<IMemoryCache>();
            var cacheAside = new MemoryCacheAside(mockCache.Object);

            // Act
            var result = await cacheAside.GetOrCreateAsync<string>("testPartition", null, (_) => Task.FromResult("testValue"));

            // Assert
            Assert.IsNull(result);
        }

        //[Test]
        //public void GetOrCreate_PartitionAndKey_AddsToCachePartition()
        //{
        //    // Arrange
        //    var mockCacheEntry = new Mock<ICacheEntry>();
        //    var mockCache = new Mock<IMemoryCache>();
        //    mockCache.Setup(m => m.GetOrCreate(
        //            It.IsAny<object>(),
        //            It.IsAny<Func<ICacheEntry, string>>()))
        //        .Returns("testValue")
        //        .Callback<object, Func<ICacheEntry, string>>((_, factory) => factory(mockCacheEntry.Object));
        //    var cacheAside = new MemoryCacheAside(mockCache.Object);

        //    // Act
        //    var result = cacheAside.GetOrCreate<string>("testPartition", "testKey", (_) => "testValue");

        //    // Assert
        //    Assert.AreEqual("testValue", result);
        //    Assert.IsTrue(cacheAside.CachePartitions.ContainsKey("testPartition"));
        //    Assert.IsTrue(cacheAside.CachePartitions["testPartition"].Contains("testKey"));
        //}

        //[Test]
        //public async Task GetOrCreateAsync_PartitionAndKey_AddsToCachePartition()
        //{
        //    // Arrange
        //    var mockCacheEntry = new Mock<ICacheEntry>();
        //    var mockCache = new Mock<IMemoryCache>();
        //    mockCache.Setup(m => m.GetOrCreateAsync(
        //            It.IsAny<object>(),
        //            It.IsAny<Func<ICacheEntry, Task<string>>>()))
        //        .ReturnsAsync("testValue")
        //        .Callback<object, Func<ICacheEntry, Task<string>>>((_, factory) => factory(mockCacheEntry.Object));
        //    var cacheAside = new MemoryCacheAside(mockCache.Object);

        //    // Act
        //    var result = await cacheAside.GetOrCreateAsync<string>("testPartition", "testKey", (_) => Task.FromResult("testValue"));

        //    // Assert
        //    Assert.AreEqual("testValue", result);
        //    Assert.IsTrue(cacheAside.CachePartitions.ContainsKey("testPartition"));
        //    Assert.IsTrue(cacheAside.CachePartitions["testPartition"].Contains("testKey"));
        //}

        [Test]
        public void Clear_CachesExist_RemovesAllFromCache()
        {
            // Arrange
            var mockCache = new Mock<IMemoryCache>();
            var cacheAside = new MemoryCacheAside(mockCache.Object);
            cacheAside.CachePartitions["testPartition1"] = new[] { "testKey1", "testKey2" }.ToList();
            cacheAside.CachePartitions["testPartition2"] = new[] { "testKey3" }.ToList();

            mockCache.Setup(m => m.Remove(It.IsAny<object>()))
                .Callback<object>((_) => { });

            mockCache.Setup(m => m.Remove(It.Is<object>(key => key.ToString() == "testPartition1-testKey1")))
                .Callback<object>((_) => { });

            mockCache.Setup(m => m.Remove(It.Is<object>(key => key.ToString() == "testPartition1-testKey2")))
                .Callback<object>((_) => { });

            mockCache.Setup(m => m.Remove(It.Is<object>(key => key.ToString() == "testPartition2-testKey3")))
                .Callback<object>((_) => { });

            // Act            
            cacheAside.Clear();

            // Assert
            mockCache.Verify(m => m.Remove(It.Is<object>(key => key.ToString() == "testPartition1-testKey1")), Times.Once);
            mockCache.Verify(m => m.Remove(It.Is<object>(key => key.ToString() == "testPartition1-testKey2")), Times.Once);
            mockCache.Verify(m => m.Remove(It.Is<object>(key => key.ToString() == "testPartition2-testKey3")), Times.Once);
            mockCache.Verify(m => m.Remove(It.IsAny<object>()), Times.Exactly(3));
        }

        [Test]
        public void Remove_PartitionNotExist_NothingHappens()
        {
            // Arrange
            var mockCache = new Mock<IMemoryCache>();
            var cacheAside = new MemoryCacheAside(mockCache.Object);

            // Act            
            Assert.DoesNotThrow(() => cacheAside.Remove("testPartitionNotExist"));
        }

        [Test]
        public void Remove_CacheKeysExist_RemovesAllFromCache()
        {
            // Arrange
            var mockCache = new Mock<IMemoryCache>();
            var cacheAside = new MemoryCacheAside(mockCache.Object);
            cacheAside.CachePartitions["testPartition1"] = new[] { "testKey1", "testKey2" }.ToList();

            mockCache.Setup(m => m.Remove("testPartition1-testKey1"));
            mockCache.Setup(m => m.Remove("testPartition1-testKey2"));

            // Act            
            cacheAside.Remove("testPartition1");

            // Assert
            mockCache.Verify(m => m.Remove("testPartition1-testKey1"), Times.Once);
            mockCache.Verify(m => m.Remove("testPartition1-testKey2"), Times.Once);
        }

        [Test]
        public void Remove_KeyNotExist_NothingHappens()
        {
            // Arrange
            var mockCache = new Mock<IMemoryCache>();
            var cacheAside = new MemoryCacheAside(mockCache.Object);

            // Act            
            Assert.DoesNotThrow(() => cacheAside.Remove("testPartition", "testKeyNotExist"));
        }

        [Test]
        public void Remove_KeyExist_RemovesFromCache()
        {
            // Arrange
            var mockCache = new Mock<IMemoryCache>();
            var cacheAside = new MemoryCacheAside(mockCache.Object);
            cacheAside.CachePartitions["testPartition"] = new[] { "testKey", "testKey2" }.ToList();

            mockCache.Setup(m => m.Remove("testPartition-testKey"));

            // Act            
            cacheAside.Remove("testPartition", "testKey");

            // Assert
            mockCache.Verify(m => m.Remove("testPartition-testKey"), Times.Once);
        }
    }
}