using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;
using Xunit.Sdk;
using Xunit.v3;

namespace MyMusic.OpenTelemetry.XUnit.Telemetry;

public class OpenTelemetryTestFramework : XunitTestFramework
{
    private static readonly AsyncLocal<OpenTelemetryTestFramework?> CurrentAsyncLocal = new();

    public static OpenTelemetryTestFramework? Current => CurrentAsyncLocal.Value;

    public OpenTelemetryTestFramework()
    {
        CurrentAsyncLocal.Value = this;
    }

    public ServiceProvider? Services { get; private set; }

    public LoggerProvider? LoggerProvider { get; private set; }

    public TracerProvider? TracerProvider { get; private set; }

    public OtelConfig? OtelConfig { get; private set; }

    public void InitializeServices()
    {
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var otelConfig = config.GetSection("OpenTelemetry").Get<OtelConfig>() ?? new OtelConfig();
        var serviceName = TelemetryServiceNameAttribute.DiscoverServiceName();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddOpenTelemetry()
            .WithMyMusicTelemetry(otelConfig, serviceName);

        Services = serviceCollection.BuildServiceProvider();
        TracerProvider = Services.GetService<TracerProvider>();
        LoggerProvider = Services.GetService<LoggerProvider>();
        OtelConfig = otelConfig;
    }

    public async ValueTask DisposeServicesAsync()
    {
        var timeoutMs = OtelConfig?.ForceFlushTimeoutMilliseconds ?? 30000;

        TracerProvider?.ForceFlush(timeoutMs);
        LoggerProvider?.ForceFlush(timeoutMs);

        LoggerProvider?.Dispose();
        TracerProvider?.Dispose();
        Services?.Dispose();
        Services = null;
    }

    protected override ITestFrameworkExecutor CreateExecutor(Assembly assembly)
    {
        var testAssembly = new Xunit.v3.XunitTestAssembly(assembly, null, assembly.GetName().Version);
        var executor = new OpenTelemetryTestFrameworkExecutor(testAssembly);
        DisposalTracker.Add(executor);

        return executor;
    }

    public override async ValueTask DisposeAsync()
    {
        await DisposeServicesAsync();
        await base.DisposeAsync();
    }
}

public class OpenTelemetryTestFrameworkExecutor(Xunit.v3.IXunitTestAssembly testAssembly) :
    TestFrameworkExecutor<Xunit.v3.IXunitTestCase>((ITestAssembly)testAssembly)
{
    protected new Xunit.v3.IXunitTestAssembly TestAssembly { get; } = testAssembly;

    protected override ITestFrameworkDiscoverer CreateDiscoverer()
    {
        return new Xunit.v3.XunitTestFrameworkDiscoverer(TestAssembly);
    }

    public override async ValueTask RunTestCases(
        IReadOnlyCollection<Xunit.v3.IXunitTestCase> testCases,
        IMessageSink executionMessageSink,
        ITestFrameworkExecutionOptions executionOptions,
        CancellationToken cancellationToken)
    {
        var framework = OpenTelemetryTestFramework.Current!;
        framework.InitializeServices();

        try
        {
            await OpenTelemetryTestAssemblyRunner.Instance.Run(
                TestAssembly, testCases, executionMessageSink, executionOptions, cancellationToken);
        }
        finally
        {
            await framework.DisposeServicesAsync();
        }
    }
}
