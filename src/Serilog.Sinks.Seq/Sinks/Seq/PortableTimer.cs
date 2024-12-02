// Copyright 2013-2016 Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Serilog.Debugging;

namespace Serilog.Sinks.Seq;

sealed class PortableTimer : IDisposable
{
    readonly object _stateLock = new();

    readonly Func<CancellationToken, Task> _onTick;
    readonly CancellationTokenSource _cancel = new();

    readonly Timer _timer;

    bool _running;
    bool _disposed;

    public PortableTimer(Func<CancellationToken, Task> onTick)
    {
        _onTick = onTick ?? throw new ArgumentNullException(nameof(onTick));
        _timer = new Timer(_ => OnTick(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start(TimeSpan interval)
    {
        if (interval < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));

        lock (_stateLock)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PortableTimer));

            _timer.Change(interval, Timeout.InfiniteTimeSpan);
        }
    }

    async void OnTick()
    {
        try
        {
            lock (_stateLock)
            {
                if (_disposed || _running)
                {
                    // Timer callbacks may be overlapped; if the sink is still shipping logs when the next interval
                    // begins, skip this interval to avoid piling up threads.
                    return;
                }

                _running = true;
            }

            if (!_cancel.Token.IsCancellationRequested)
            {
                await _onTick(_cancel.Token);
            }
        }
        catch (OperationCanceledException tcx)
        {
            SelfLog.WriteLine("The timer was canceled during invocation: {0}", tcx);
        }
        finally
        {
            lock (_stateLock)
            {
                _running = false;
                Monitor.PulseAll(_stateLock);
            }
        }
    }

    public void Dispose()
    {
        _cancel.Cancel();

        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            while (_running)
            {
                Monitor.Wait(_stateLock);
            }

            _timer.Dispose();
            _disposed = true;
        }
    }
}