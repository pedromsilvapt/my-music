using System.Text.Json;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using Shouldly;

namespace MyMusic.IntegrationTests.Base;

public abstract class IntegrationTestBase : PageTest
{
    public static readonly string BaseUrl =
        Environment.GetEnvironmentVariable("BASE_URL") is { } envUrl && !string.IsNullOrEmpty(envUrl)
            ? envUrl
            : "http://localhost:5001";

    protected IAPIRequestContext RequestContext { get; private set; } = null!;
    protected string UserName { get; } = $"Test-{Guid.NewGuid()}";
    protected long UserId { get; private set; }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        RequestContext = await Playwright.APIRequest.NewContextAsync(new()
        {
            BaseURL = BaseUrl,
            ExtraHTTPHeaders = new Dictionary<string, string>
            {
                ["X-MyMusic-UserName"] = UserName,
            },
        });

        await CreateTestUser();

        await Context.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
        {
            { "X-MyMusic-UserName", UserName }
        });

        await Page.GotoAsync(BaseUrl);
    }

    public override async Task DisposeAsync()
    {
        await RemoveTestUser();
        await RequestContext.DisposeAsync();
        await base.DisposeAsync();
    }

    protected virtual async Task CreateTestUser()
    {
        var response = await RequestContext.PostAsync("/api/users", new()
        {
            DataObject = new
            {
                user = new
                {
                    username = UserName,
                    name = UserName,
                },
            },
        });

        response.Ok.ShouldBeTrue($"Failed to create test user: {response.Status} {response.StatusText}");

        var json = await response.JsonAsync();
        UserId = json?.GetProperty("user").GetProperty("id").GetInt64()
            ?? throw new InvalidOperationException("Failed to get user ID from response");
    }

    protected virtual async Task RemoveTestUser()
    {
        var response = await RequestContext.DeleteAsync($"/api/users/{UserId}");
        response.Ok.ShouldBeTrue($"Failed to delete test user: {response.Status} {response.StatusText}");
    }
}
