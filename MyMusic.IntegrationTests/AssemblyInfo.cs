using MyMusic.OpenTelemetry.XUnit;
using MyMusic.OpenTelemetry.XUnit.Telemetry;

[assembly: TestFramework(typeof(OpenTelemetryTestFramework))]

[assembly: TelemetryServiceName("MyMusic.IntegrationTests")]