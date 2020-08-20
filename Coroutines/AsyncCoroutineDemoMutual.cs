// https://github.com/noseratio/coroutines-talk

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Coroutines
{
    public static class AsyncCoroutineDemoMutual
    {
        private static async IAsyncEnumerable<int> CoroutineA(
            IAsyncCoroutineProxy<int> coroutineProxy,
            [EnumeratorCancellation] CancellationToken token)
        {
            var coroutineB = await coroutineProxy.AsAsyncEnumerable(token);

            // await for coroutineB to advance by 40 steps
            await foreach (var step in coroutineB)
            {
                if (step >= 40)
                    break;
            }

            var inputIdler = new InputIdler();
            var interval = new Interval();

            for (int i = 0; i < 80; i++)
            {
                await inputIdler.Yield(token);

                Console.SetCursorPosition(0, 0);
                Console.Write($"{nameof(CoroutineA)}: {new String('A', i)}");

                await interval.Delay(25, token);
                yield return i;
            }
        }

        /// <summary>
        /// CoroutineB yields to CoroutineA
        /// </summary>
        private static async IAsyncEnumerable<int> CoroutineB(
            [EnumeratorCancellation] CancellationToken token)
        {
            var inputIdler = new InputIdler();
            var interval = new Interval();

            for (int i = 0; i < 80; i++)
            {
                await inputIdler.Yield(token);

                Console.SetCursorPosition(0, 1);
                Console.Write($"{nameof(CoroutineB)}: {new String('B', i)}");

                await interval.Delay(50, token);
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
            var proxyA = new AsyncCoroutineProxy<int>();
            var proxyB = new AsyncCoroutineProxy<int>();

            // start both coroutines
            await Task.WhenAll(
                proxyA.RunAsync(token => CoroutineA(proxyB, token), token),
                proxyB.RunAsync(token => CoroutineB(token), token));
        }
    }
}
