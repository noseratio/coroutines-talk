// https://github.com/noseratio/coroutines-talk

#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Coroutines
{
    public class InputIdler : SimpleValueTaskSource
    {
        private CancellationToken _token = default;

        private void OnIdle(object? s, EventArgs e)
        {
            if (!AnyInputMessage() || _token.IsCancellationRequested)
            {
                Complete();
            }
        }

        public override void Close()
        {
            System.Windows.Forms.Application.Idle -= OnIdle;
        }

        public async ValueTask Yield(CancellationToken token)
        {
            // if there is any input in the queue, 
            // await for Application.Idle event
            if (AnyInputMessage())
            {
                System.Windows.Forms.Application.Idle += OnIdle;
                try
                {
                    await GetValueTask();
                }
                finally
                {
                    System.Windows.Forms.Application.Idle -= OnIdle;
                }
            }
            token.ThrowIfCancellationRequested();
        }

        public static bool AnyInputMessage()
        {
            uint status = GetQueueStatus(QS_INPUT);
            return (status >> 16) != 0;
        }

        [DllImport("user32.dll")]
        private static extern uint GetQueueStatus(uint flags);

        private const uint QS_KEY = 0x0001;
        private const uint QS_MOUSEMOVE = 0x0002;
        private const uint QS_MOUSEBUTTON = 0x0004;
        private const uint QS_POSTMESSAGE = 0x0008;
        private const uint QS_TIMER = 0x0010;
        private const uint QS_PAINT = 0x0020;
        private const uint QS_SENDMESSAGE = 0x0040;
        private const uint QS_HOTKEY = 0x0080;
        private const uint QS_ALLPOSTMESSAGE = 0x0100;
        private const uint QS_RAWINPUT = 0x0400;

        private const uint QS_MOUSE = (QS_MOUSEMOVE | QS_MOUSEBUTTON);

        private const uint QS_INPUT = (QS_MOUSE | QS_KEY | QS_RAWINPUT);
        private const uint QS_ALLEVENTS = (QS_INPUT | QS_POSTMESSAGE | QS_TIMER | QS_PAINT | QS_HOTKEY);

        private const uint QS_ALLINPUT = 0x4FF;
    }
}
