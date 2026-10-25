using FluentAssertions;
using Marketplace.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Marketplace.Tests;

/// <summary>Unit tests for cache and search value behavior.</summary>
[TestClass]
public sealed class LruProfileCacheTests
{
    /// <summary>Ensures recent cache entries avoid a second source call.</summary>
    [TestMethod]
    public async Task GetOrCreateAsync_ReturnsCachedValueBeforeExpiry()
    {
        var clock = new TestTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        IProfileCache cache = new LruProfileCache(clock);
        var calls = 0;

        var first = await cache.GetOrCreateAsync("customer:1", _ => Task.FromResult<Profile?>(new Profile(++calls)), CancellationToken.None);
        var second = await cache.GetOrCreateAsync("customer:1", _ => Task.FromResult<Profile?>(new Profile(++calls)), CancellationToken.None);

        first.Should().BeEquivalentTo(second);
        calls.Should().Be(1);
    }

    /// <summary>Ensures expired cache entries are fetched again.</summary>
    [TestMethod]
    public async Task GetOrCreateAsync_RefreshesExpiredValue()
    {
        var clock = new TestTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        IProfileCache cache = new LruProfileCache(clock);
        var calls = 0;

        await cache.GetOrCreateAsync("contractor:1", _ => Task.FromResult<Profile?>(new Profile(++calls)), CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(61));
        var refreshed = await cache.GetOrCreateAsync("contractor:1", _ => Task.FromResult<Profile?>(new Profile(++calls)), CancellationToken.None);

        refreshed!.Version.Should().Be(2);
    }

    /// <summary>Ensures LIKE metacharacters are escaped rather than executed as wildcards.</summary>
    [TestMethod]
    public void EscapeLike_EscapesSqlWildcardCharacters()
    {
        SearchText.EscapeLike("50%_ready\\now").Should().Be("50\\%\\_ready\\\\now");
    }

    private sealed record Profile(int Version);
    private sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset current = now;
        public override DateTimeOffset GetUtcNow() => current;
        public void Advance(TimeSpan duration) => current = current.Add(duration);
    }
}
