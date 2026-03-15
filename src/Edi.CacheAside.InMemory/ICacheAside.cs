namespace Edi.CacheAside.InMemory;

public interface ICacheAside : IDisposable
{
    TItem? GetOrCreate<TItem>(string partition, string key, Func<TItem> factory, TimeSpan? expiration = null);
    Task<TItem?> GetOrCreateAsync<TItem>(string partition, string key, Func<Task<TItem>> factory, TimeSpan? expiration = null);
    void Clear();
    void Remove(string partition);
    void Remove(string partition, string key);
}