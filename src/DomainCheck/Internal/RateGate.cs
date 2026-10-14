using System.Diagnostics;

namespace DomainCheck.Internal;

/// <summary>Ensures a minimum interval between operations, across threads.</summary>
internal sealed class RateGate(TimeSpan minimumInterval)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan? _last;

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        if (minimumInterval <= TimeSpan.Zero)
        {
            return;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_last is { } last)
            {
                var wait = minimumInterval - (_clock.Elapsed - last);
                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
                }
            }

            _last = _clock.Elapsed;
        }
        finally
        {
            _lock.Release();
        }
    }
}
