// https://github.com/noseratio/coroutines-talk

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Coroutines
{
    /// <summary>
    /// This is just to illustrate what "await foreach" does behind the scene
    /// </summary>
    public static partial class TaskExtensions
    {
        public static async ValueTask ForEachAsync<T>(
            this IAsyncEnumerable<T> @this, 
            Action<T> action, 
            CancellationToken token = default)
        {
            await using var enumerator = @this.GetAsyncEnumerator(token);
            while (await enumerator.MoveNextAsync())
            {
                action(enumerator.Current);
            }
        }

        public static async ValueTask ForEachAsync<T>(
            this IAsyncEnumerable<T> @this,
            Func<T, Task> func,
            CancellationToken token = default)
        {
            await using var enumerator = @this.GetAsyncEnumerator(token);
            while (await enumerator.MoveNextAsync())
            {
                await func(enumerator.Current);
            }
        }

        public static async ValueTask ForEachAsync<T>(
            this IAsyncEnumerable<T> @this,
            Func<T, ValueTask> func,
            CancellationToken token = default)
        {
            await using var enumerator = @this.GetAsyncEnumerator(token);
            while (await enumerator.MoveNextAsync())
            {
                await func(enumerator.Current);
            }
        }
    }
}