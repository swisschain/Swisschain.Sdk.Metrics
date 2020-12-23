using Prometheus;

namespace Swisschain.Sdk.Metrics
{
    public static class GaugeExtensions
    {
        public static MillisecondsTimer NewMillisecondsTimer(this Gauge gauge, params string[] labels)
            => new MillisecondsTimer(gauge, labels);
    }
}