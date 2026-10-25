namespace Marketplace.Core;

/// <summary>Provides a narrow cache seam for profile lookups.</summary>
public interface IProfileCache
{
    /// <summary>Gets a value or creates it through the supplied asynchronous factory.</summary>
    Task<T?> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T?>> factory, CancellationToken cancellationToken) where T : class;
}

/// <summary>Bounded, thread-safe least-recently-used cache for immutable profile projections.</summary>
public sealed class LruProfileCache(TimeProvider timeProvider) : IProfileCache
{
    private const int Capacity = 1000;
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);
    private readonly object gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly LinkedList<string> recency = [];

    /// <inheritdoc />
    public async Task<T?> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T?>> factory, CancellationToken cancellationToken) where T : class
    {
        lock (gate)
        {
            if (entries.TryGetValue(key, out var entry) && entry.ExpiresAt > timeProvider.GetUtcNow())
            {
                recency.Remove(entry.Node);
                recency.AddFirst(entry.Node);
                return entry.Value as T;
            }
            Remove(key);
        }

        var created = await factory(cancellationToken).ConfigureAwait(false);
        if (created is null) return null;

        lock (gate)
        {
            Remove(key);
            var node = recency.AddFirst(key);
            entries[key] = new Entry(created, timeProvider.GetUtcNow().Add(Ttl), node);
            if (entries.Count > Capacity && recency.Last is { } tail) Remove(tail.Value);
        }
        return created;
    }

    private void Remove(string key)
    {
        if (!entries.Remove(key, out var old)) return;
        recency.Remove(old.Node);
    }

    private sealed record Entry(object Value, DateTimeOffset ExpiresAt, LinkedListNode<string> Node);
}
