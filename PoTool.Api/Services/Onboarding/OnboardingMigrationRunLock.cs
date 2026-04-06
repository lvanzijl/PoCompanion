using System.Collections.Concurrent;

namespace PoTool.Api.Services.Onboarding;

public interface IOnboardingMigrationRunLock
{
    Task<IAsyncDisposable?> TryAcquireAsync(string lockKey, CancellationToken cancellationToken);
}

public sealed class OnboardingMigrationRunLock : IOnboardingMigrationRunLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IAsyncDisposable?> TryAcquireAsync(string lockKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockKey);

        var semaphore = _locks.GetOrAdd(lockKey.Trim(), static _ => new SemaphoreSlim(1, 1));
        if (!await semaphore.WaitAsync(0, cancellationToken))
        {
            return null;
        }

        return new Releaser(semaphore);
    }

    private sealed class Releaser : IAsyncDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private int _released;

        public Releaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                _semaphore.Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}
