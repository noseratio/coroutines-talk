// https://github.com/noseratio/coroutines-talk

#nullable enable

using System.Threading.Tasks;

namespace Coroutines
{
    //TODO: cancellation
    public class TimerSource: SimpleValueTaskSource
    {
        private readonly System.Windows.Forms.Timer _timer;

        public TimerSource(int interval)
        {
            _timer = new System.Windows.Forms.Timer { Interval = interval };
            _timer.Tick += (s, e) => Complete();
            _timer.Start();
        }

        public ValueTask NextTickAsync() =>
            GetValueTask();

        public override void Close()
        {
            _timer.Dispose();
        }
    }
}
