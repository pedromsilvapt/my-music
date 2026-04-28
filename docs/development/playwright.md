# Playwright

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
