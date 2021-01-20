# Swisschain.Sdk.Metrics registration

Add to setup.cs file

```csharp
	public sealed class Startup : SwisschainStartup<AppConfig>
	{
		public Startup(IConfiguration configuration)
		    : base(configuration)
		{
		}

		protected override void ConfigureExt(IApplicationBuilder app, IWebHostEnvironment env)
		{
			app.UseMetricServer();
		}
	}
```

Create metric

```csharp
    public static class InternalMetrics
    {
        public static readonly Counter QuoteInCount = Metrics
            .CreateCounter("quote_in_count",
                "Counter of received quote.",
                new CounterConfiguration {LabelNames = new[] {"exchange", "symbol"}});

        public static readonly Gauge QuoteInDelayMilliseconds = Metrics
            .CreateGauge("quote_in_delay_ms",
                "Gauge of received quote delay between occurring and receiving in milliseconds.",
                new GaugeConfiguration {LabelNames = new[] {"exchange", "symbol"}});

		public static readonly Gauge HedgeOrderProcessMilliseconds = Metrics
            .CreateGauge("hedge_order_process_ms",
                "Gauge of hedge order process elapsed time in milliseconds.",
                new GaugeConfiguration {LabelNames = new[] {"symbol"}});
	}
```

Usage


```

InternalMetrics.QuoteInCount.WithLabels(Venue, symbol).Inc();

```

# Grpc service traker

```

	public sealed class Startup : SwisschainStartup<AppConfig>
	{
		public Startup(IConfiguration configuration)
		    : base(configuration)
		{
		}

		protected override void ConfigureGrpcServiceOptions(GrpcServiceOptions options)
		{
		    options.Interceptors.Add<PrometheusMetricsInterceptor>();
		}
	}

```

# Grpc client traker

```

using var grpcChannel = GrpcChannel.ForAddress(serverGrpcUrl);

var invoker = _grpcChannel.Intercept(new PrometheusMetricsInterceptor());

var monitoringClient = new Monitoring.MonitoringClient(invoker);

var responce = await monitoringClient.IsAliveAsync(new IsAliveRequest());

```

# Http service traker

```csharp
	public sealed class Startup : SwisschainStartup<AppConfig>
	{
		public Startup(IConfiguration configuration)
		    : base(configuration)
		{
		}

		protected override void ConfigureExt(IApplicationBuilder app, IWebHostEnvironment env)
		{
			app.UseMiddleware<PrometheusMetricsMiddleware>();
		}
	}
```
