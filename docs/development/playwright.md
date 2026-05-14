# Playwright

## Video and Trace Recording

Integration tests support optional video and trace recording for debugging test failures.

### Enable Recording

Set the `PLAYWRIGHT_RECORD_VIDEO` environment variable to `true`:

```bash
# Run tests with recording enabled
PLAYWRIGHT_RECORD_VIDEO=true dotnet test MyMusic.IntegrationTests

# Or via runsettings
dotnet test -- Playwright.LaunchOptions.Headless=false
```

Or add to `.runsettings`:
```xml
<EnvironmentVariables>
  <PLAYWRIGHT_RECORD_VIDEO>true</PLAYWRIGHT_RECORD_VIDEO>
</EnvironmentVariables>
```

### Output Location

When enabled, recordings are saved to:
- **Videos**: `MyMusic.IntegrationTests/test-results/videos/*.webm`
- **Traces**: `MyMusic.IntegrationTests/test-results/traces/*.zip`

### View Traces

After recording, view traces with the Playwright trace viewer:

```bash
npx playwright show-trace MyMusic.IntegrationTests/test-results/traces/MyTestClass-20240502-123456.zip
```

The trace viewer shows:
- Screenshots at each step
- DOM snapshots
- Network requests
- Console logs

### Videos

Videos are in WebM format and can be played in any browser or media player (VLC, etc.).

### Storage

The `test-results/` directory is gitignored. Recordings are only created when the environment variable is set, so they won't fill up disk space during normal test runs.

---

## Version Synchronization

The Playwright version used by the integration test Docker image must stay in sync with the .NET package version.

### Files to Keep in Sync

1. **Earthfile** (`Earthfile` line 112):
   ```dockerfile
   RUN pnpm install -g playwright@^1.59 && \
   ```

2. **Integration Tests Project** (`MyMusic.IntegrationTests/MyMusic.IntegrationTests.csproj` line 13):
   ```xml
   <PackageReference Include="Microsoft.Playwright.Xunit.v3" Version="1.59.0" />
   ```

### Rule

When updating the Playwright .NET package in `MyMusic.IntegrationTests.csproj`, **always** update the `pnpm install` version in the `Earthfile` to match. Mismatched versions can cause browser binary incompatibilities and test failures in the Docker environment.

---

## Getting the Root Element (`<html>` or `<body>`)

```csharp
// The root html element
ILocator root = page.Locator(":root");
ILocator html = page.Locator("html");
ILocator body = page.Locator("body");
```

---

## The Main Ways to Get an `ILocator` from `IPage`

### By CSS Selector
```csharp
page.Locator("div")                        // tag name
page.Locator(".my-class")                  // class name
page.Locator("#my-id")                     // id
page.Locator(".parent > .child")           // CSS combinator
page.Locator("input[type='email']")        // attribute
page.Locator("div.card.active")            // multiple classes
```

### By Test ID
```csharp
page.GetByTestId("submit-button")          // [data-testid="submit-button"]
```

### By Role (semantic / accessible)
```csharp
page.GetByRole(AriaRole.Button, new() { Name = "Submit" })
page.GetByRole(AriaRole.Heading, new() { Level = 1 })
page.GetByRole(AriaRole.Textbox)
page.GetByRole(AriaRole.Link, new() { Name = "Home" })
```

### By Label (form inputs)
```csharp
page.GetByLabel("Email address")
page.GetByLabel("Password")
```

### By Placeholder
```csharp
page.GetByPlaceholder("Search...")
```

### By Text Content
```csharp
page.GetByText("Welcome back")             // partial match by default
page.GetByText("Welcome back", new() { Exact = true })
```

### By Alt Text (images)
```csharp
page.GetByAltText("Company logo")
```

### By Title Attribute
```csharp
page.GetByTitle("Close dialog")
```

---

## Chaining — Narrowing Scope

Once you have a locator, all the same methods are available on it, which is how you scope components:

```csharp
// IPage -> ILocator -> ILocator -> ...
ILocator form      = page.GetByTestId("login-form");
ILocator emailInput = form.GetByLabel("Email");
ILocator submitBtn  = form.GetByRole(AriaRole.Button, new() { Name = "Submit" });

// CSS chaining
ILocator card       = page.Locator(".card-list > .card").First;
ILocator cardTitle  = card.Locator(".card-title");
```

---

## Filtering

```csharp
// Filter by text content
page.GetByTestId("card-item")
    .Filter(new() { HasText = "My Report" });

// Filter by a child element existing inside it
page.GetByTestId("card-item")
    .Filter(new() { Has = page.GetByTestId("card-badge") });

// Combine both
page.GetByTestId("card-item")
    .Filter(new() {
        HasText = "My Report",
        Has = page.GetByTestId("card-badge")
    });
```

---

## Picking from Multiple Matches

```csharp
page.Locator(".card").First          // first match
page.Locator(".card").Last           // last match
page.Locator(".card").Nth(2)         // zero-based index
```

---

## Quick Reference

| Goal | Method |
|---|---|
| Tag name | `page.Locator("div")` |
| Class | `page.Locator(".my-class")` |
| ID | `page.Locator("#my-id")` |
| CSS | `page.Locator("div > p.active")` |
| Attribute | `page.Locator("input[type='email']")` |
| Test ID | `page.GetByTestId("my-id")` |
| ARIA role | `page.GetByRole(AriaRole.Button)` |
| Form label | `page.GetByLabel("Email")` |
| Placeholder | `page.GetByPlaceholder("Search")` |
| Visible text | `page.GetByText("Submit")` |
| Alt text | `page.GetByAltText("Logo")` |
| Root element | `page.Locator(":root")` |

All of these work identically on `ILocator` as on `IPage`, which is what makes the scoped component pattern composable all the way down.
