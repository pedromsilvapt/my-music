## Phase 1: Fixtures with Existing APIs

### 1.1 Develop DevicesFixture

- [x] 1.1.1 Create `MyMusic.IntegrationTests/Fixtures/` directory
- [x] 1.1.2 Create `MyMusic.IntegrationTests/Fixtures/Models/DeviceData.cs` record
- [x] 1.1.3 Create `MyMusic.IntegrationTests/Fixtures/DevicesFixture.cs` with `Data` property
- [x] 1.1.4 Implement `SeedAsync()` using `POST /api/devices` endpoint
- [x] 1.1.5 Fetch sample device data from `http://localhost/api/devices` (port 80)
- [x] 1.1.6 Parse JSON response and create devices via API for test user

### 1.2 Build DevicesFixture

- [x] 1.2.1 Run `dotnet build MyMusic.IntegrationTests` to verify compilation
- [x] 1.2.2 Fix any compilation errors

### 1.3 Test DevicesFixture

- [x] 1.3.1 Create test file `MyMusic.IntegrationTests/Tests/Fixtures/DevicesFixtureTests.cs`
- [x] 1.3.2 Write test: `SeedAsync_CreatesDevices` - verify devices appear in database
- [x] 1.3.3 Write test: `Data_ContainsSeededDevices` - verify Data property populated
- [ ] 1.3.4 Run `dotnet test MyMusic.IntegrationTests` to verify tests pass

### 1.4 Develop PlaylistsFixture

- [x] 1.4.1 Create `MyMusic.IntegrationTests/Fixtures/Models/PlaylistData.cs` record
- [x] 1.4.2 Create `MyMusic.IntegrationTests/Fixtures/PlaylistsFixture.cs` with `Data` property
- [x] 1.4.3 Implement `SeedAsync()` using `POST /api/playlists` endpoint
- [x] 1.4.4 Fetch sample playlist data from `http://localhost/api/playlists` (port 80)
- [x] 1.4.5 Parse JSON response and create playlists via API for test user

### 1.5 Build PlaylistsFixture

- [x] 1.5.1 Run `dotnet build MyMusic.IntegrationTests` to verify compilation
- [x] 1.5.2 Fix any compilation errors

### 1.6 Test PlaylistsFixture

- [x] 1.6.1 Create test file `MyMusic.IntegrationTests/Tests/Fixtures/PlaylistsFixtureTests.cs`
- [x] 1.6.2 Write test: `SeedAsync_CreatesPlaylists` - verify playlists appear in database
- [x] 1.6.3 Write test: `Data_ContainsSeededPlaylists` - verify Data property populated
- [ ] 1.6.4 Run `dotnet test MyMusic.IntegrationTests` to verify tests pass

## Phase 2: SongsFixture with Upload Support

### 2.1 Develop Test Audio File

- [x] 2.1.1 Create `MyMusic.IntegrationTests/Fixtures/TestFiles/` directory
- [x] 2.1.2 Create minimal test MP3 file (can be empty or synthetic)
- [x] 2.1.3 Mark file as embedded resource in `.csproj`

### 2.2 Develop Song Data Models

- [x] 2.2.1 Create `MyMusic.IntegrationTests/Fixtures/Models/SongData.cs` record
- [x] 2.2.2 Create `MyMusic.IntegrationTests/Fixtures/Models/ArtistData.cs` record
- [x] 2.2.3 Create `MyMusic.IntegrationTests/Fixtures/Models/AlbumData.cs` record
- [x] 2.2.4 Create `MyMusic.IntegrationTests/Fixtures/Models/GenreData.cs` record

### 2.3 Develop SongsFixture

- [x] 2.3.1 Create `MyMusic.IntegrationTests/Fixtures/SongsFixture.cs` with `Data` property
- [x] 2.3.2 Implement `SeedAsync()` using `POST /api/songs/upload` endpoint
- [x] 2.3.3 Use multipart form data with test audio file
- [x] 2.3.4 Parse response to get created song ID and metadata
- [x] 2.3.5 Fetch sample song data from `http://localhost/api/songs` (port 80)

### 2.4 Build SongsFixture

- [x] 2.4.1 Run `dotnet build MyMusic.IntegrationTests` to verify compilation
- [x] 2.4.2 Fix any compilation errors

### 2.5 Test SongsFixture

- [x] 2.5.1 Create test file `MyMusic.IntegrationTests/Tests/Fixtures/SongsFixtureTests.cs`
- [x] 2.5.2 Write test: `SeedAsync_UploadsSong` - verify song appears in database
- [x] 2.5.3 Write test: `Data_ContainsSeededSongs` - verify Data property populated
- [x] 2.5.4 Write test: `SeedAsync_CreatesArtistsAndAlbums` - verify related entities created
- [ ] 2.5.5 Run `dotnet test MyMusic.IntegrationTests` to verify tests pass

## Phase 3: Backend Endpoints for Artists/Albums/Genres

### 3.1 Develop Artists API

- [x] 3.1.1 Create `POST /api/artists` endpoint in `ArtistsController.cs`
- [x] 3.1.2 Create request/response DTOs for artist creation
- [x] 3.1.3 Implement artist creation logic
- [x] 3.1.4 Run `dotnet build` to verify compilation

### 3.2 Test Artists API

- [ ] 3.2.1 Write unit tests for `POST /api/artists` endpoint
- [ ] 3.2.2 Run `dotnet test` to verify tests pass

### 3.3 Develop ArtistsFixture

- [x] 3.3.1 Create `MyMusic.IntegrationTests/Fixtures/ArtistsFixture.cs` with `Data` property
- [x] 3.3.2 Implement `SeedAsync()` using `POST /api/artists` endpoint
- [x] 3.3.3 Fetch sample artist data from `http://localhost/api/artists` (port 80)
- [x] 3.3.4 Run `dotnet build MyMusic.IntegrationTests` to verify compilation

### 3.4 Test ArtistsFixture

- [x] 3.4.1 Create test file `MyMusic.IntegrationTests/Tests/Fixtures/ArtistsFixtureTests.cs`
- [x] 3.4.2 Write test: `SeedAsync_CreatesArtists` - verify artists appear in database
- [ ] 3.4.3 Run `dotnet test MyMusic.IntegrationTests` to verify tests pass

### 3.5 Develop Albums API

- [x] 3.5.1 Create `POST /api/albums` endpoint in `AlbumsController.cs` (or create controller)
- [x] 3.5.2 Create request/response DTOs for album creation
- [x] 3.5.3 Implement album creation logic (requires artist reference)
- [x] 3.5.4 Run `dotnet build` to verify compilation

### 3.6 Test Albums API

- [ ] 3.6.1 Write unit tests for `POST /api/albums` endpoint
- [ ] 3.6.2 Run `dotnet test` to verify tests pass

### 3.7 Develop AlbumsFixture

- [x] 3.7.1 Create `MyMusic.IntegrationTests/Fixtures/AlbumsFixture.cs` with `Data` property
- [x] 3.7.2 Implement `SeedAsync()` using `POST /api/albums` endpoint
- [x] 3.7.3 Fetch sample album data from `http://localhost/api/albums` (port 80)
- [x] 3.7.4 Run `dotnet build MyMusic.IntegrationTests` to verify compilation

### 3.8 Test AlbumsFixture

- [x] 3.8.1 Create test file `MyMusic.IntegrationTests/Tests/Fixtures/AlbumsFixtureTests.cs`
- [x] 3.8.2 Write test: `SeedAsync_CreatesAlbums` - verify albums appear in database
- [ ] 3.8.3 Run `dotnet test MyMusic.IntegrationTests` to verify tests pass

### 3.9 Develop Genres API

- [x] 3.9.1 Create `GenresController.cs` with `POST /api/genres` endpoint
- [x] 3.9.2 Create request/response DTOs for genre creation
- [x] 3.9.3 Implement genre creation logic
- [x] 3.9.4 Run `dotnet build` to verify compilation

### 3.10 Test Genres API

- [ ] 3.10.1 Write unit tests for `POST /api/genres` endpoint
- [ ] 3.10.2 Run `dotnet test` to verify tests pass

### 3.11 Develop GenresFixture

- [x] 3.11.1 Create `MyMusic.IntegrationTests/Fixtures/GenresFixture.cs` with `Data` property
- [x] 3.11.2 Implement `SeedAsync()` using `POST /api/genres` endpoint
- [x] 3.11.3 Fetch sample genre data from `http://localhost/api/genres` (port 80)
- [x] 3.11.4 Run `dotnet build MyMusic.IntegrationTests` to verify compilation

### 3.12 Test GenresFixture

- [x] 3.12.1 Create test file `MyMusic.IntegrationTests/Tests/Fixtures/GenresFixtureTests.cs`
- [x] 3.12.2 Write test: `SeedAsync_CreatesGenres` - verify genres appear in database
- [ ] 3.12.3 Run `dotnet test MyMusic.IntegrationTests` to verify tests pass

## Phase 4: Integration and Documentation

### 4.1 Develop Composite Fixture

- [x] 4.1.1 Create `MyMusic.IntegrationTests/Fixtures/AllDataFixture.cs` that seeds all fixtures
- [x] 4.1.2 Implement composite `SeedAsync()` that calls all individual fixtures
- [x] 4.1.3 Implement composite `ClearAsync()` for cleanup
- [x] 4.1.4 Run `dotnet build MyMusic.IntegrationTests` to verify compilation

### 4.2 Test Composite Fixture

- [x] 4.2.1 Create test file `MyMusic.IntegrationTests/Tests/Fixtures/AllDataFixtureTests.cs`
- [x] 4.2.2 Write test: `SeedAsync_CreatesAllEntities` - verify all entities created
- [ ] 4.2.3 Run `dotnet test MyMusic.IntegrationTests` to verify tests pass

### 4.3 Update AGENTS.md

- [x] 4.3.1 Add section documenting fixture usage pattern
- [x] 4.3.2 Add example code for using fixtures in tests

### 4.4 Final Verification

- [ ] 4.4.1 Run `dotnet build` on entire solution
- [ ] 4.4.2 Run `dotnet test` on entire solution
- [ ] 4.4.3 Verify all integration tests pass
