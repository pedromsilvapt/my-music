## Context

The MyMusic project uses Playwright for integration testing the React frontend. Tests use the Page Object Model pattern:
- `Page` classes (e.g., `HomePage`) receive `IPage` and extend `BasePage`
- `Component` classes (e.g., `TopbarComponent`) receive `ILocator` for scoped element access
- Locators use `data-testid` attributes for stable selection

The songs page uses a generic `Collection` component which renders as a `CollectionTable` by default. The table has virtualized rows and columns defined by a schema. Currently, no test IDs exist on navbar items or table cells.

## Goals / Non-Goals

**Goals:**
- Add test IDs to all sidebar nav items
- Add test IDs to Collection table cells with format `collection-cell-{column}-{rowKey}`
- Create page objects following existing patterns
- Create one end-to-end test for songs page visibility

**Non-Goals:**
- Complex test scenarios (filtering, sorting, pagination)
- Testing list or grid views
- Testing individual song detail pages

## Decisions

### 1. Sidebar Navigation Test IDs

Add `data-testid` to each `NavLink` in `app.tsx`:

```tsx
<NavLink
  data-testid="nav-songs"
  renderRoot={(props) => <Link to={"/songs"} {...props} />}
  leftSection={<IconMusic stroke={2}/>}
  label="Songs"
/>
```

**Test ID naming**: `nav-{route}` pattern (nav-songs, nav-albums, nav-artists, etc.)

### 2. NavbarComponent Structure

Following `TopbarComponent` pattern, create a component with one locator per nav item:

```csharp
public class NavbarComponent(ILocator root)
{
    public ILocator SongsLink => root.GetByTestId("nav-songs");
    public ILocator AlbumsLink => root.GetByTestId("nav-albums");
    public ILocator ArtistsLink => root.GetByTestId("nav-artists");
    public ILocator PlaylistsLink => root.GetByTestId("nav-playlists");
    public ILocator DevicesLink => root.GetByTestId("nav-devices");
    public ILocator HistoryLink => root.GetByTestId("nav-history");
    public ILocator AuditsLink => root.GetByTestId("nav-audits");
    public ILocator PurchasesLink => root.GetByTestId("nav-purchases");
    public ILocator SettingsLink => root.GetByTestId("nav-settings");
    public ILocator HomeLink => root.GetByTestId("nav-home");
    public ILocator PlayerLink => root.GetByTestId("nav-player");
}
```

**Rationale**: One locator per item is explicit, type-safe, and matches existing patterns.

### 3. Collection Table Cell Test IDs

Add `data-testid` to each `Table.Td` in `CollectionTableRowInner`:

```tsx
<Table.Td
  key={col.name}
  data-testid={`collection-cell-${col.name}-${itemId}`}
  style={{...}}
>
  {col.render(row, virtualRow.index, items)}
</Table.Td>
```

For list/grid views, the "column" placeholder contains view-specific slots (title, subtitle, etc.).

**Test ID format**: `collection-cell-{column}-{rowKey}`
- `column`: Column name from schema (e.g., "title", "album", "artists")
- `rowKey`: Entity ID from `schema.key(row)`

### 4. CollectionComponent Structure

```csharp
public class CollectionComponent(ILocator root)
{
    public ILocator Container => root;
    
    public ILocator GetCell(string column, object rowKey) =>
        root.GetByTestId($"collection-cell-{column}-{rowKey}");
    
    public async Task<int> GetRowCountAsync()
    {
        var rows = root.Locator("tr[data-index]");
        return await rows.CountAsync();
    }
    
    public async Task WaitForVisibleAsync()
    {
        await root.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }
}
```

### 5. SongsPage Structure

```csharp
public class SongsPage(IPage page) : BasePage(page)
{
    public NavbarComponent Navbar => new(Page.GetByTestId("navbar"));
    public CollectionComponent Collection => new(Page.GetByTestId("collection"));
}
```

**Note**: The collection container in `collection.tsx` needs a root `data-testid="collection"` attribute.

### 6. File Structure

```
MyMusic.IntegrationTests/
├── Pages/
│   ├── BasePage.cs
│   ├── HomePage.cs
│   ├── SongsPage.cs           # NEW
│   └── Components/
│       ├── TopbarComponent.cs
│       ├── NavbarComponent.cs  # NEW
│       └── CollectionComponent.cs  # NEW
└── Tests/
    └── Songs/
        └── SongsPageTests.cs  # NEW
```

### 7. Root Collection Test ID

The `Collection` component's root `Flex` needs a test ID:

```tsx
return <Flex 
  ref={containerRef} 
  direction="column" 
  style={{height: `100%`}}
  data-testid="collection"
>
```

This allows the `SongsPage.Collection` locator to find the entire collection container.

## Risks / Trade-offs

- **[Virtualized rows]** → Playwright may not see all rows if they're not rendered. Mitigation: use `GetRowCountAsync()` for approximate count, or scroll to load more.
- **[Column name stability]** → Column names in schema could change. Mitigation: column names are stable identifiers, not display names.
- **[Test ID proliferation]** → Adding test IDs to every cell could bloat HTML. Mitigation: test IDs are small and only in dev/test scenarios.
