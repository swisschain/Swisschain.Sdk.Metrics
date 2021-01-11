using System;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Prometheus;

namespace Swisschain.Sdk.Metrics.Rest
{
    public class PrometheusMetricsMiddleware
    {
        private static Counter ServerHttpCallInCount = Prometheus.Metrics
            .CreateCounter("swiss_http_server_call_in_count",
                "Counter of calls http methods. Counter applied before execute logic",
                new CounterConfiguration { LabelNames = new[] { "host", "path" } });

        private static Counter ServerHttpCallOutCount = Prometheus.Metrics
            .CreateCounter("siwss_http_server_call_out_count",
                "Counter of calls http methods. Counter applied after execute logic",
                new CounterConfiguration { LabelNames = new[] { "host", "path", "status" } });

        private static readonly Gauge ServerHttpCallProcessCount = Prometheus.Metrics
            .CreateGauge("siwss_http_server_call_process_count",
                "Counter of active calls of http methods.",
                new GaugeConfiguration { LabelNames = new[] { "host", "path" } });



        private static readonly Histogram ServerHttpCallAvgDelaySec = Prometheus.Metrics
            .CreateHistogram("siwss_http_server_call_delay_sec",
                "Histogram of http call delay in second.",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "host", "path" },
                    Buckets = new double[] { double.PositiveInfinity }
                });

        private static readonly Gauge ServerHttpCallLastDelaySec = Prometheus.Metrics
            .CreateGauge("siwss_http_server_call_delay_sec_last",
                "Histogram of http call delay in second.",
                new GaugeConfiguration { LabelNames = new[] { "host", "path" } });


        private readonly RequestDelegate _next;
        private static string HostName;

        public PrometheusMetricsMiddleware(RequestDelegate next)
        {
            _next = next;
            var defaultName = Assembly.GetEntryAssembly()?.GetName().Name ?? "unknown";
            HostName = Environment.GetEnvironmentVariable("HOST") ?? defaultName;
        }

        public async Task Invoke(HttpContext context)
        {
            var path = context.Request.Path.ToString();

            // skip isalive method from statistic
            if (path.ToLower().Contains("isalive"))
            {
                await _next.Invoke(context);
                return;
            }

            using (ServerHttpCallProcessCount.WithLabels(HostName, path).TrackInProgress())
            {
                using (ServerHttpCallAvgDelaySec.Labels(HostName, path).NewTimer())
                {
                    using (ServerHttpCallLastDelaySec.NewMillisecondsTimer(HostName, path))
                    {
                        try
                        {
                            ServerHttpCallInCount.WithLabels(HostName, path).Inc();

                            await _next.Invoke(context);

                            var statusCode = context.Response?.StatusCode.ToString() ?? "unknown";

                            ServerHttpCallOutCount.WithLabels(HostName, path, statusCode).Inc();
                        }
                        catch (Exception)
                        {
                            ServerHttpCallOutCount.WithLabels(HostName, path, "exception").Inc();
                            throw;
                        }
                    }
                }
            }

            
        }
    }
}