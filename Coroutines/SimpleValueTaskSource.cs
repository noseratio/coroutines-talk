// https://github.com/noseratio/coroutines-talk

#nullable enable

using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Coroutines
{
    //TODO: cancellation
    public abstract class SimpleValueTaskSource: IDisposable, IValueTaskSource
    {
        private short _currentTaskToken = 1;
        private bool _isTaskCompleted = false;
        private (Action<object>?, object?) _continuation;

        public abstract void Close();

        protected void Complete()
        {
            _isTaskCompleted = true;
            var (callback, state) = _continuation;
            _continuation = default;
            callback?.Invoke(state!);
        }

        protected ValueTask GetValueTask() =>
            new ValueTask(this, _currentTaskToken);

        public void Dispose() => Close();

        void IValueTaskSource.GetResult(short token)
        {
            ThrowIfInvalidToken(token);
            ThrowIfIncomplete();
            _isTaskCompleted = false;
            _currentTaskToken += 2; // we don't want this to ever be zero
            _currentTaskToken &= short.MaxValue;
        }

        ValueTaskSourceStatus IValueTaskSource.GetStatus(short token)
        {
            ThrowIfInvalidToken(token);
            return _isTaskCompleted ?
                ValueTaskSourceStatus.Succeeded :
                ValueTaskSourceStatus.Pending;
        }

        void IValueTaskSource.OnCompleted(Action<object>? continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            ThrowIfInvalidToken(token);
            ThrowIfMultipleContinuations();
            _continuation = (continuation, state);
        }

        #region Throw helpers
        private void ThrowIfInvalidToken(short token)
        {
            if (_currentTaskToken != token)
            {
                throw new InvalidOperationException(nameof(ThrowIfInvalidToken));
            }
        }

        private void ThrowIfIncomplete()
        {
            if (!_isTaskCompleted)
            {
                throw new InvalidOperationException(nameof(ThrowIfIncomplete));
            }
        }

        private void ThrowIfMultipleContinuations()
        {
            if (_continuation != default)
            {
                throw new InvalidOperationException(nameof(ThrowIfMultipleContinuations));
            }
        }
        #endregion
    }
}
