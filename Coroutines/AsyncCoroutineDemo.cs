// https://github.com/noseratio/coroutines-talk

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Coroutines
{
    public static class AsyncCoroutineDemo
    {
        private static async IAsyncEnumerable<int> CoroutineA(
            [EnumeratorCancellation] CancellationToken token)
        {
            var inputIdler = new InputIdler();
            for (int i = 0; i < 80; i++)
            {
                // yield to the event loop to process any keyboard/mouse input first
                await inputIdler.Yield(token);

                // now we could use Task.Run for this
                // but let's pretend this code must execute on the UI thread 
                Console.SetCursorPosition(0, 0);
                Console.Write($"{nameof(CoroutineA)}: {new String('A', i)}");

                yield return i;
            }
        }

        private static async IAsyncEnumerable<int> CoroutineB(
            [EnumeratorCancellation] CancellationToken token)
        {
            var inputIdler = new InputIdler();
            for (int i = 0; i < 80; i++)
            {
                // yield to the event loop to process any keyboard/mouse input first
                await inputIdler.Yield(token);

                Console.SetCursorPosition(0, 1);
                Console.Write($"{nameof(CoroutineB)}: {new String('B', i)}");

                // slow down CoroutineB
                await Task.Delay(25, token);
                yield return i;
            }
        }

        public static async ValueTask DemoAsync(CancellationToken token)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                Console.Clear();
                await RunCoroutinesAsync<int>(
                    intervalMs: 50, 
                    token, 
                    CoroutineA, CoroutineB);
            }
        }

        private static async ValueTask RunCoroutinesAsync<T>(
            int intervalMs,
            CancellationToken token,
            params Func<CancellationToken, IAsyncEnumerable<T>>[] coroutines)
        {
            var tasks = coroutines.Select(async c => 
            {
                var interval = new Interval();
                await foreach (var item in c(token).WithCancellation(token))
                {
                    await interval.Delay(intervalMs, token);
                }
            });

            await Task.WhenAll(tasks); 
        }

        static readonly object _lock = new Object();
    }
}
