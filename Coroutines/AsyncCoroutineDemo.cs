// https://github.com/noseratio/coroutines-talk

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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

        private static async Task DriveCoroutinesAsync<T>(
            int intervalMs,
            CancellationToken token,
            params Func<CancellationToken, IAsyncEnumerable<T>>[] coroutines)
        {
            var tasks = coroutines.Select(async coroutine => 
            {
                var interval = new Interval();
                await foreach (var item in coroutine(token).WithCancellation(token))
                {
                    await interval.Delay(intervalMs, token);
                }
            });

            await Task.WhenAll(tasks); 
        }

        public static async Task DemoAsync(CancellationToken token)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                Console.Clear();
                await DriveCoroutinesAsync<int>(
                    intervalMs: 50,
                    token,
                    CoroutineA, CoroutineB);
            }
        }
    }
}
