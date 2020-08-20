// https://github.com/noseratio/coroutines-talk

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Coroutines
{
    public interface IAsyncCoroutineProxy<T>
    {
        public Task<IAsyncEnumerable<T>> AsAsyncEnumerable(CancellationToken token = default);
    }

    public class AsyncCoroutineProxy<T> : IAsyncCoroutineProxy<T>
    {
        readonly TaskCompletionSource<IAsyncEnumerable<T>> _proxyTcs =
            new TaskCompletionSource<IAsyncEnumerable<T>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public AsyncCoroutineProxy()
        {
        }

        async Task<IAsyncEnumerable<T>> IAsyncCoroutineProxy<T>.AsAsyncEnumerable(CancellationToken token)
        {
            using var _ = token.Register(() => _proxyTcs.TrySetCanceled(), useSynchronizationContext: false);
            return await _proxyTcs.Task;
        }

        public async Task RunAsync(Func<CancellationToken, IAsyncEnumerable<T>> coroutine, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var channel = Channel.CreateUnbounded<T>();
            var writer = channel.Writer;
            var proxy = channel.Reader.ReadAllAsync(token);
            _proxyTcs.SetResult(proxy); 
            
            try
            {
                await foreach (var item in coroutine(token).WithCancellation(token))
                {
                    await writer.WriteAsync(item, token);
                }
                writer.Complete();
            }
            catch (Exception ex)
            {
                writer.Complete(ex);
                throw;
            }
        }
    }

    public static class AsyncCoroutineExtensions
    {
        public async static ValueTask<IAsyncEnumerator<T>> AsAsyncEnumerator<T>(
            this IAsyncCoroutineProxy<T> @this,
            CancellationToken token = default)
        {
            return (await @this.AsAsyncEnumerable(token)).GetAsyncEnumerator(token);
        }

        public async static ValueTask<T> GetNextAsync<T>(this IAsyncEnumerator<T> @this)
        {
            if (!await @this.MoveNextAsync())
            {
                throw new IndexOutOfRangeException(nameof(GetNextAsync));
            }
            return @this.Current;
        }
    }
}
