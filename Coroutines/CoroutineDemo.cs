// https://github.com/noseratio/coroutines-talk

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Coroutines
{
    public static class CoroutineDemo
    {
        // how is this different from making CoroutineA async ?
        // - it's driven externally at externally controlled intervall
        // - upon every step, it can yield useful info the external driver

        private static IEnumerable<int> CoroutineA()
        {
            for (int i = 0; i < 80; i++)
            {
                Console.SetCursorPosition(0, 0);
                Console.Write($"{nameof(CoroutineA)}: {new String('A', i)}");
                yield return i;
            }
        }

        private static IEnumerable<int> CoroutineB()
        {
            for (int i = 0; i < 80; i++)
            {
                Console.SetCursorPosition(0, 1);
                Console.Write($"{nameof(CoroutineB)}: {new String('B', i)}");
                yield return i;
            }
        }

        public static async ValueTask DemoAsync(CancellationToken token)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                Console.Clear();
                await RunCoroutinesAsync(token);
            }
        }
        private static async ValueTask RunCoroutinesAsync(CancellationToken token)
        {
            // combine two IEnumerable sequences into one and get an IEnumerator for it
            using var combined = CoroutineCombinator<int>.Combine(
                CoroutineA, 
                CoroutineB)
                .GetEnumerator();

            var tcs = new TaskCompletionSource<bool>();
            using var rego = token.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: true);

            using var timer = new System.Windows.Forms.Timer { Interval = 50 };
            timer.Tick += (s, e) =>
            {
                try
                {
                    // upon each timer tick,
                    // pull/execute the next slice 
                    // of the combined coroutine code flow
                    if (!combined.MoveNext())
                    {
                        tcs.TrySetResult(true);
                    }
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                    throw;
                }
            };

            timer.Start();
            await tcs.Task;
        }
    }
}
