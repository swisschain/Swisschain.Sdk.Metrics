using System;
using System.Reflection;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Prometheus;

namespace Swisschain.Sdk.Metrics.Grpc
{
    public class PrometheusMetricsInterceptor : Interceptor
    {
        private static Counter ServerGrpcCallInCount = Prometheus.Metrics
            .CreateCounter("swiss_grpc_server_call_in_count",
                "Counter of calls grpc methods. Counter applied before execute logic",
                new CounterConfiguration { LabelNames = new[] { "host", "controller", "method" } });

        private static Counter ServerGrpcCallOutCount = Prometheus.Metrics
            .CreateCounter("siwss_grpc_server_call_out_count",
                "Counter of calls grpc methods. Counter applied after execute logic",
                new CounterConfiguration { LabelNames = new[] { "host", "controller", "method", "status" } });

        private static readonly Gauge ServerGrpcCallProcessCount = Prometheus.Metrics
            .CreateGauge("siwss_grpc_server_call_process_count",
                "Counter of active calls of grpc methods.",
                new GaugeConfiguration { LabelNames = new[] { "host", "controller", "method" } });

        private static readonly Histogram ServerGrpcCallDelaySec = Prometheus.Metrics
            .CreateHistogram("siwss_grpc_server_call_delay_sec",
                "Histogram of grpc call delay in second.",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "host", "controller", "method" },
                    Buckets = new double[] { double.PositiveInfinity }
                });


        private static Counter ClientGrpcCallInCount = Prometheus.Metrics
            .CreateCounter("swiss_grpc_client_call_in_count",
                "Counter of calls grpc methods. Counter applied before execute logic",
                new CounterConfiguration { LabelNames = new[] { "host", "controller", "method" } });

        private static Counter ClientGrpcCallOutCount = Prometheus.Metrics
            .CreateCounter("siwss_grpc_client_call_out_count",
                "Counter of calls grpc methods. Counter applied after execute logic",
                new CounterConfiguration { LabelNames = new[] { "host", "controller", "method", "status" } });

        private static readonly Gauge ClientGrpcCallProcessCount = Prometheus.Metrics
            .CreateGauge("siwss_grpc_client_call_process_count",
                "Counter of active calls of grpc methods.",
                new GaugeConfiguration { LabelNames = new[] { "host", "controller", "method" } });

        private static readonly Histogram ClientGrpcCallDelaySec = Prometheus.Metrics
            .CreateHistogram("siwss_grpc_client_call_delay_sec",
                "Histogram of grpc call delay in second.",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "host", "controller", "method" },
                    Buckets = new double[] { double.PositiveInfinity }
                });

        private static string HostName;

        static PrometheusMetricsInterceptor()
        {
            var defaultName = Assembly.GetEntryAssembly()?.FullName ?? "unknown";
            HostName = Environment.GetEnvironmentVariable("HOST") ?? defaultName;
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request,
            ServerCallContext context,
            UnaryServerMethod<TRequest, TResponse> continuation)
        {
            var method = context.Method;
            var prm = context.Method.Split('/');
            var controller = prm.Length >= 2 ? prm[1] : "unknown";

            using (ServerGrpcCallProcessCount.WithLabels(HostName, controller, method).TrackInProgress())
            {
                using (ServerGrpcCallDelaySec.Labels(HostName, controller, method).NewTimer())
                {
                    ServerGrpcCallInCount.WithLabels(HostName, controller, method).Inc();

                    try
                    {
                        var resp = await continuation(request, context);

                        ServerGrpcCallOutCount.WithLabels(HostName, controller, method, context.Status.StatusCode.ToString()).Inc();

                        return resp;
                    }
                    catch (Exception)
                    {
                        ServerGrpcCallOutCount.WithLabels(HostName, controller, method, "exception").Inc();
                        throw;
                    }
                }
            }
        }

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context,
            BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            var method = context.Method.Name;
            var controller = context.Method.ServiceName;

            using (ClientGrpcCallProcessCount.WithLabels(HostName, controller, method).TrackInProgress())
            {
                using (ClientGrpcCallDelaySec.Labels(HostName, controller, method).NewTimer())
                {
                    ClientGrpcCallInCount.WithLabels(HostName, controller, method).Inc();

                    try
                    {
                        var resp = continuation(request, context);

                        ClientGrpcCallOutCount.WithLabels(HostName, controller, method, "success").Inc();

                        return resp;
                    }
                    catch (Exception)
                    {
                        ClientGrpcCallOutCount.WithLabels(HostName, controller, method, "exception").Inc();
                        throw;
                    }
                }
            }
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context,
            AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            var method = context.Method.Name;
            var controller = context.Method.ServiceName;

            var clientGrpcCallProcessCount = ClientGrpcCallProcessCount.WithLabels(HostName, controller, method).TrackInProgress();

            var clientGrpcCallDelaySec = ClientGrpcCallDelaySec.Labels(HostName, controller, method).NewTimer();
                
            ClientGrpcCallInCount.WithLabels(HostName, controller, method).Inc();

            try
            {
                var resp = continuation(request, context);

                resp.ResponseAsync.ContinueWith(task =>
                {
                    clientGrpcCallProcessCount.Dispose();
                    clientGrpcCallDelaySec.Dispose();

                    string status;
                    try
                    {
                        status = resp.GetStatus().StatusCode.ToString();
                    }
                    catch (Exception)
                    {
                        status = "unknown";
                    }
                    ClientGrpcCallOutCount.WithLabels(HostName, controller, method, status).Inc();
                });

                resp.ResponseAsync.ContinueWith(task =>
                {
                    ClientGrpcCallOutCount.WithLabels(HostName, controller, method, "exception").Inc();
                }, TaskContinuationOptions.NotOnFaulted);

                return resp;
            }
            catch (Exception)
            {
                ClientGrpcCallOutCount.WithLabels(HostName, controller, method, "exception").Inc();
                throw;
            }
        }
    }
}