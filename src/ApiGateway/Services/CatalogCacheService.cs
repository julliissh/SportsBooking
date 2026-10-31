using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace ApiGateway.Services;

public interface ICatalogCacheService
{
    long Hits { get; }
    long Misses { get; }
    void RecordHit();
    void RecordMiss();
    void Invalidate();
    IChangeToken GetExpirationToken();
}

public class CatalogCacheService : ICatalogCacheService
{
    private long _hits;
    private long _misses;
    private CancellationTokenSource _cts = new();

    public long Hits => Interlocked.Read(ref _hits);
    public long Misses => Interlocked.Read(ref _misses);

    public void RecordHit() => Interlocked.Increment(ref _hits);
    public void RecordMiss() => Interlocked.Increment(ref _misses);

    public void Invalidate()
    {
        var oldCts = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
        try
        {
            oldCts.Cancel();
            oldCts.Dispose();
        }
        catch
        {
        }
    }

    public IChangeToken GetExpirationToken()
    {
        return new CancellationChangeToken(_cts.Token);
    }
}
