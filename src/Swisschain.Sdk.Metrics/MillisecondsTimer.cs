using System;
using System.Diagnostics;
using System.Linq;
using Prometheus;

namespace Swisschain.Sdk.Metrics
{
    public class MillisecondsTimer: IDisposable
    {
        private readonly Gauge _gauge;
        private readonly string[] _labels;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public MillisecondsTimer(Gauge gauge, params string[] labels)
        {
            _gauge = gauge;
            _labels = labels;
        }

        public void Dispose()
        {
            _stopwatch.Stop();

            if (_labels?.Any() == true)
                _gauge.WithLabels(_labels).Set(_stopwatch.Elapsed.TotalMilliseconds);
            else
                _gauge.Set(_stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}
