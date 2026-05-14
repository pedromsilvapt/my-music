using MyMusic.IntegrationTests.Fixtures;
using MyMusic.OpenTelemetry.XUnit;
using Microsoft.Extensions.Configuration;

namespace MyMusic.IntegrationTests.Tests.Sync;

public class MobileSyncTests(ITestOutputHelper output) : SyncTestsBase(output)
{
    protected override ISyncApplication CreateApplication(IConfiguration configuration, IntegrationTestTelemetry telemetry)
        => new MobileCliApplication(configuration, telemetry);
}
