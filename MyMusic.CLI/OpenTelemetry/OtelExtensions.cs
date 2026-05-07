using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MyMusic.OpenTelemetry;

[Obsolete("This class is a copy from MyMusic.OpenTelemetry project. " +
           "Reference the MyMusic.OpenTelemetry project instead once the build system supports cross-directory project references.")]
public static class OtelExtensions
{
    public static TracerProviderBuilder AddOtlpTracing(
        this TracerProviderBuilder tracingBuilder,
        OtelConfig config,
        params string[] sourceNames)
    {
        return tracingBuilder
            .AddHttpClientInstrumentation()
            .AddAspNetCoreInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddSource(sourceNames)
            .AddOtlpExporter(options =>
            {
                options.Endpoint = config.GetTracesEndpoint();
                options.Protocol = config.GetProtocol();
            });
    }

    public static LoggerProviderBuilder AddOtlpLogging(
        this LoggerProviderBuilder loggingBuilder,
        OtelConfig config)
    {
        return loggingBuilder
            .AddOtlpExporter(options =>
            {
                options.Endpoint = config.GetLogsEndpoint();
                options.Protocol = config.GetProtocol();
            });
    }

    public static MeterProviderBuilder AddOtlpMetrics(
        this MeterProviderBuilder metricsBuilder,
        OtelConfig config)
    {
        return metricsBuilder
            .AddOtlpExporter(options =>
            {
                options.Endpoint = config.GetMetricsEndpoint();
                options.Protocol = config.GetProtocol();
            });
    }

    public static IServiceCollection AddMyMusicOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        var config = configuration.GetSection("OpenTelemetry").Get<OtelConfig>() ?? new OtelConfig();

        if (!config.Enabled)
        {
            return services;
        }

        services.AddOpenTelemetry()
            .WithMyMusicTelemetry(config, serviceName);

        return services;
    }

    public static IOpenTelemetryBuilder WithMyMusicTelemetry(
        this IOpenTelemetryBuilder openTelemetryBuilder,
        OtelConfig config,
        params string[] sourceNames)
    {
        var serviceName = sourceNames.FirstOrDefault() ?? Assembly.GetExecutingAssembly().GetName().Name;

        if (serviceName is null)
        {
            throw new NotSupportedException("Executing Assembly Name is null");
        }

        openTelemetryBuilder.ConfigureResource(builder => builder.AddService(serviceName))
            .WithTracing(tracing => tracing.AddOtlpTracing(config, sourceNames))
            .WithLogging(logging => logging.AddOtlpLogging(config))
            .WithMetrics(metrics => metrics.AddOtlpMetrics(config));

        return openTelemetryBuilder;
    }

    #region OtelConfig Extensions

    public static OtlpExportProtocol GetProtocol(this OtelConfig config) =>
        config.Protocol.Equals("http/protobuf", StringComparison.OrdinalIgnoreCase)
            ? OtlpExportProtocol.HttpProtobuf
            : OtlpExportProtocol.Grpc;

    public static Uri GetTracesEndpoint(this OtelConfig config) =>
        new(config.TracesEndpoint ?? $"{config.Endpoint}/v1/traces");

    public static Uri GetLogsEndpoint(this OtelConfig config) =>
        new(config.LogsEndpoint ?? $"{config.Endpoint}/v1/logs");

    public static Uri GetMetricsEndpoint(this OtelConfig config) =>
        new(config.MetricsEndpoint ?? $"{config.Endpoint}/v1/metrics");

    #endregion OtelConfig Extensions

}
