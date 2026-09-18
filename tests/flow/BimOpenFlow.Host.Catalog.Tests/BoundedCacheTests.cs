namespace BimOpenFlow.Host.Catalog.Tests;

[TestFixture]
public sealed class BoundedCacheTests
{
    [Test]
    public void SecondGet_ReusesTheStoredValueWithoutCallingTheFactory()
    {
        var calls = 0;
        var cache = new BoundedCache<string, int>(2);
        Assert.That(cache.GetOrAdd("a", _ => ++calls), Is.EqualTo(1));
        Assert.That(cache.GetOrAdd("a", _ => ++calls), Is.EqualTo(1));
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void OverCapacity_EvictsTheLeastRecentlyUsedKey()
    {
        var built = new List<string>();
        var cache = new BoundedCache<string, string>(2);
        foreach (var key in new[] { "a", "b" })
            cache.GetOrAdd(key, k => Build(built, k));
        cache.GetOrAdd("a", k => Build(built, k)); // "b" is now least recent
        cache.GetOrAdd("c", k => Build(built, k));

        Assert.That(cache.Count, Is.EqualTo(2));
        cache.GetOrAdd("a", k => Build(built, k));
        cache.GetOrAdd("b", k => Build(built, k));
        Assert.That(built, Is.EqualTo(new[] { "a", "b", "c", "b" }));
    }

    [Test]
    public void ZeroCapacity_IsRejected()
        => Assert.Throws<ArgumentOutOfRangeException>(() => _ = new BoundedCache<string, string>(0));

    private static string Build(List<string> log, string key)
    {
        log.Add(key);
        return key;
    }
}
