// https://github.com/noseratio/coroutines-talk

#nullable enable

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Coroutines
{
    public class Interval
    {
        private readonly Stopwatch _stopwatch = new Stopwatch();

        public Interval()
        {
            _stopwatch.Start();
        }

        public void Reset()
        {
            _stopwatch.Reset();
        }

        public async ValueTask Delay(int intervalMs, CancellationToken token)
        {
            var delay = intervalMs - (int)_stopwatch.ElapsedMilliseconds;
            if (delay > 0)
            {
                await Task.Delay(delay, token);
            }
            Reset();
            token.ThrowIfCancellationRequested();
        }
    }
}
