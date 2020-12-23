using System;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Prometheus;

namespace Swisschain.Sdk.Metrics.Grpc
{
    public class PrometheusMetricsInterceptor : Interceptor
    {
        private static Counter GrpcCallInCount = Prometheus.Metrics
            .CreateCounter("swiss_grpc_call_in_count",
                "Counter of calls grpc methods. Counter applied before execute logic",
                new CounterConfiguration { LabelNames = new[] { "controller", "method" } });

        private static Counter GrpcCallOutCount = Prometheus.Metrics
            .CreateCounter("siwss_grpc_call_out_count",
                "Counter of calls grpc methods. Counter applied after execute logic",
                new CounterConfiguration { LabelNames = new[] { "controller", "method", "status" } });

        private static readonly Gauge GrpcCallProcessCount = Prometheus.Metrics
            .CreateGauge("siwss_grpc_call_process_count",
                "Counter of active calls of grpc methods.",
                new GaugeConfiguration { LabelNames = new[] { "controller", "method" } });

        public static readonly Histogram GrpcCallDelaySec = Prometheus.Metrics
            .CreateHistogram("siwss_grpc_call_delay_sec",
                "Histogram of grpc call delay in second.",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "controller", "method" },
                    Buckets = new double[] { double.PositiveInfinity }
                });

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request,
            ServerCallContext context,
            UnaryServerMethod<TRequest, TResponse> continuation)
        {
            var method = context.Method;
            var prm = context.Method.Split('/');
            var controller = prm.Length >= 2 ? prm[1] : "unknown";

            using (GrpcCallProcessCount.WithLabels(controller, method).TrackInProgress())
            {
                using (GrpcCallDelaySec.Labels(controller, method).NewTimer())
                {
                    GrpcCallInCount.WithLabels(controller, method).Inc();

                    try
                    {
                        var resp = await continuation(request, context);

                        GrpcCallOutCount.WithLabels(controller, method, context.Status.StatusCode.ToString()).Inc();

                        return resp;
                    }
                    catch (Exception)
                    {
                        GrpcCallOutCount.WithLabels(controller, method, "exception").Inc();
                        throw;
                    }
                }
            }
        }
    }
}