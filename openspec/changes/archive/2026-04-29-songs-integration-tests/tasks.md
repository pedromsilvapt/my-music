## Phase 1: Add Test IDs to React Components

### 1.1 Add Navbar Test IDs

- [x] 1.1.1 Add `data-testid="navbar"` to `AppShell.Navbar` in `app.tsx`
- [x] 1.1.2 Add `data-testid="nav-player"` to Player NavLink
- [x] 1.1.3 Add `data-testid="nav-home"` to Home NavLink
- [x] 1.1.4 Add `data-testid="nav-songs"` to Songs NavLink
- [x] 1.1.5 Add `data-testid="nav-albums"` to Albums NavLink
- [x] 1.1.6 Add `data-testid="nav-artists"` to Artists NavLink
- [x] 1.1.7 Add `data-testid="nav-playlists"` to Playlists NavLink
- [x] 1.1.8 Add `data-testid="nav-devices"` to Devices NavLink
- [x] 1.1.9 Add `data-testid="nav-history"` to History NavLink
- [x] 1.1.10 Add `data-testid="nav-audits"` to Audits NavLink
- [x] 1.1.11 Add `data-testid="nav-purchases"` to Purchases NavLink
- [x] 1.1.12 Add `data-testid="nav-settings"` to Settings NavLink

### 1.2 Add Collection Test IDs

- [x] 1.2.1 Add `data-testid="collection"` to root `Flex` in `collection.tsx`
- [x] 1.2.2 Add `data-testid={`collection-cell-${col.name}-${itemId}`}` to `Table.Td` in `collection-table.tsx`

### 1.3 Verify Build

- [x] 1.3.1 Run `cd MyMusic.Client && npm run build` to verify no TypeScript errors
- [x] 1.3.2 Run `cd MyMusic.Client && npm run lint` to verify no lint errors

## Phase 2: Create Page Objects

### 2.1 Create NavbarComponent

- [x] 2.1.1 Create `MyMusic.IntegrationTests/Pages/Components/NavbarComponent.cs`
- [x] 2.1.2 Add locator properties for all nav links (SongsLink, AlbumsLink, etc.)

### 2.2 Create CollectionComponent

- [x] 2.2.1 Create `MyMusic.IntegrationTests/Pages/Components/CollectionComponent.cs`
- [x] 2.2.2 Add `GetCell(column, rowKey)` method
- [x] 2.2.3 Add `GetRowCountAsync()` method
- [x] 2.2.4 Add `WaitForVisibleAsync()` method

### 2.3 Update HomePage

- [x] 2.3.1 Add `Navbar` property to `HomePage.cs` using `NavbarComponent`

### 2.4 Create SongsPage

- [x] 2.4.1 Create `MyMusic.IntegrationTests/Pages/SongsPage.cs`
- [x] 2.4.2 Add `Navbar` property using `NavbarComponent`
- [x] 2.4.3 Add `Collection` property using `CollectionComponent`

### 2.5 Verify Build

- [x] 2.5.1 Run `dotnet build MyMusic.IntegrationTests` to verify compilation

## Phase 3: Create Integration Test

### 3.1 Create Test File

- [x] 3.1.1 Create `MyMusic.IntegrationTests/Tests/Songs/` directory
- [x] 3.1.2 Create `MyMusic.IntegrationTests/Tests/Songs/SongsPageTests.cs`

### 3.2 Write Test

- [x] 3.2.1 Write `SongsPage_ShouldDisplayCollection` test:
  - Navigate to home page
  - Click Songs nav link
  - Wait for collection to be visible
  - Assert row count > 0

### 3.3 Run Test

- [x] 3.3.1 Start backend server (`dotnet run --project MyMusic.Server`)
- [x] 3.3.2 Start frontend server (`cd MyMusic.Client && npm run dev`)
- [x] 3.3.3 Run `dotnet test MyMusic.IntegrationTests --filter "FullyQualifiedName~SongsPageTests"`
- [x] 3.3.4 Verify test passes

## Phase 4: Final Verification

- [x] 4.1 Run `dotnet build` on entire solution
- [x] 4.2 Run existing integration tests to ensure no regressions
