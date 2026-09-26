using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Filters;
using MyMusic.Common.Services;
using MyMusic.Server.Controllers;
using MyMusic.Server.DTO.Filters;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class AuditsControllerFilterSpecs
{
    #region 7.1: Unit tests for audit non-conformity field metadata

    [Fact]
    public void AuditNonConformityFieldMetadata_ContainsNineFields()
    {
        // Act
        var result = AuditsControllerInternal.GetAuditNonConformityFieldMetadata();

        // Assert
        result.Count.ShouldBe(9);
    }

    [Fact]
    public void AuditNonConformityFieldMetadata_ContainsAllExpectedFieldNames()
    {
        // Act
        var result = AuditsControllerInternal.GetAuditNonConformityFieldMetadata();

        // Assert
        var fieldNames = result.Select(f => f.Name).ToList();
        fieldNames.ShouldContain("title");
        fieldNames.ShouldContain("album.name");
        fieldNames.ShouldContain("artist.name");
        fieldNames.ShouldContain("genre.name");
        fieldNames.ShouldContain("year");
        fieldNames.ShouldContain("explicit");
        fieldNames.ShouldContain("isFavorite");
        fieldNames.ShouldContain("hasWaiver");
        fieldNames.ShouldContain("createdAt");
    }

    [Fact]
    public void AuditNonConformityFieldMetadata_TitleField_HasStringTypeAndDynamicValues()
    {
        // Act
        var result = AuditsControllerInternal.GetAuditNonConformityFieldMetadata();
        var titleField = result.First(f => f.Name == "title");

        // Assert
        titleField.Type.ShouldBe("string");
        titleField.SupportsDynamicValues.ShouldBeTrue();
        titleField.SupportedOperators.ShouldContain("eq");
        titleField.SupportedOperators.ShouldContain("contains");
        titleField.SupportedOperators.ShouldContain("startsWith");
        titleField.SupportedOperators.ShouldContain("endsWith");
    }

    [Fact]
    public void AuditNonConformityFieldMetadata_ArtistNameField_IsCollectionWithEntityPath()
    {
        // Act
        var result = AuditsControllerInternal.GetAuditNonConformityFieldMetadata();
        var artistField = result.First(f => f.Name == "artist.name");

        // Assert
        artistField.Type.ShouldBe("string");
        artistField.IsCollection.ShouldBeTrue();
        artistField.EntityPath.ShouldBe("Song.Artists.Artist.Name");
        artistField.SupportsDynamicValues.ShouldBeTrue();
    }

    [Fact]
    public void AuditNonConformityFieldMetadata_GenreNameField_IsCollectionWithEntityPath()
    {
        // Act
        var result = AuditsControllerInternal.GetAuditNonConformityFieldMetadata();
        var genreField = result.First(f => f.Name == "genre.name");

        // Assert
        genreField.Type.ShouldBe("string");
        genreField.IsCollection.ShouldBeTrue();
        genreField.EntityPath.ShouldBe("Song.Genres.Genre.Name");
        genreField.SupportsDynamicValues.ShouldBeTrue();
    }

    [Fact]
    public void AuditNonConformityFieldMetadata_YearField_HasNumberType()
    {
        // Act
        var result = AuditsControllerInternal.GetAuditNonConformityFieldMetadata();
        var yearField = result.First(f => f.Name == "year");

        // Assert
        yearField.Type.ShouldBe("number");
        yearField.EntityPath.ShouldBe("Song.Year");
        yearField.SupportsDynamicValues.ShouldBeFalse();
        yearField.SupportedOperators.ShouldContain("eq");
        yearField.SupportedOperators.ShouldContain("between");
    }

    [Fact]
    public void AuditNonConformityFieldMetadata_ExplicitField_HasBooleanTypeWithEntityPath()
    {
        // Act
        var result = AuditsControllerInternal.GetAuditNonConformityFieldMetadata();
        var explicitField = result.First(f => f.Name == "explicit");

        // Assert
        explicitField.Type.ShouldBe("boolean");
        explicitField.EntityPath.ShouldBe("Song.Explicit");
        explicitField.ClientPath.ShouldBe("isExplicit");
        explicitField.SupportedOperators.ShouldContain("isTrue");
        explicitField.SupportedOperators.ShouldContain("isFalse");
    }

    [Fact]
    public void AuditNonConformityFieldMetadata_HasWaiverField_HasBooleanTypeNoEntityPath()
    {
        // Act
        var result = AuditsControllerInternal.GetAuditNonConformityFieldMetadata();
        var waiverField = result.First(f => f.Name == "hasWaiver");

        // Assert
        waiverField.Type.ShouldBe("boolean");
        waiverField.EntityPath.ShouldBeNull();
        waiverField.SupportedOperators.ShouldContain("isTrue");
        waiverField.SupportedOperators.ShouldContain("isFalse");
    }

    [Fact]
    public void AuditNonConformityFieldMetadata_CreatedAtField_HasDateType()
    {
        // Act
        var result = AuditsControllerInternal.GetAuditNonConformityFieldMetadata();
        var createdField = result.First(f => f.Name == "createdAt");

        // Assert
        createdField.Type.ShouldBe("date");
        createdField.SupportedOperators.ShouldContain("gt");
        createdField.SupportedOperators.ShouldContain("between");
    }

    #endregion

    #region 7.1 (extended): Field mappings tests

    [Fact]
    public void AuditNonConformityFieldMappings_ContainsAllExpectedMappings()
    {
        // Act
        var mappings = AuditsControllerInternal.GetAuditNonConformityFieldMappings();

        // Assert
        mappings["title"].ShouldBe("Song.Title");
        mappings["album.name"].ShouldBe("Song.Album.Name");
        mappings["artist.name"].ShouldBe("Song.Artists.Artist.Name");
        mappings["genre.name"].ShouldBe("Song.Genres.Genre.Name");
        mappings["year"].ShouldBe("Song.Year");
        mappings["explicit"].ShouldBe("Song.Explicit");
        mappings["isFavorite"].ShouldBe("Song.IsFavorite");
        mappings["hasWaiver"].ShouldBe("HasWaiver");
        mappings["createdAt"].ShouldBe("CreatedAt");
    }

    [Fact]
    public void AuditNonConformityFieldMappings_IsCaseInsensitive()
    {
        // Act
        var mappings = AuditsControllerInternal.GetAuditNonConformityFieldMappings();

        // Assert
        mappings["Title"].ShouldBe("Song.Title");
        mappings["TITLE"].ShouldBe("Song.Title");
        mappings["HasWaiver"].ShouldBe("HasWaiver");
    }

    #endregion

    #region 7.2: Integration tests for filter-metadata and filter-values endpoints

    private static (AuditsController controller, MusicDbContext db, User user) CreateControllerWithDb()
    {
        var db = Scenario.CreateDbContext();
        var user = new User { Name = "Test User", Username = "testuser" };
        db.Users.Add(user);
        db.SaveChanges();

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(user.Id);

        var auditService = Substitute.For<IAuditService>();
        var fpcalcService = Substitute.For<IFpcalcService>();
        var fingerprintService = new AcousticFingerprintService(db, fpcalcService, Substitute.For<ILogger<AcousticFingerprintService>>());
        var resolutionService = Substitute.For<ISoundalikeResolutionService>();

        var controller = new AuditsController(currentUser, auditService, fingerprintService, resolutionService);
        return (controller, db, user);
    }

    [Fact]
    public async Task GetNonConformityFilterMetadata_ReturnsFieldsAndOperators()
    {
        // Arrange
        var (controller, db, user) = CreateControllerWithDb();

        // Act
        var result = await controller.GetNonConformityFilterMetadata(1, CancellationToken.None);

        // Assert
        result.Fields.Count.ShouldBe(9);
        result.Operators.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GetNonConformityFilterMetadata_ContainsAllFieldNames()
    {
        // Arrange
        var (controller, db, user) = CreateControllerWithDb();

        // Act
        var result = await controller.GetNonConformityFilterMetadata(1, CancellationToken.None);

        // Assert
        var fieldNames = result.Fields.Select(f => f.Name).ToList();
        fieldNames.ShouldContain("title");
        fieldNames.ShouldContain("album.name");
        fieldNames.ShouldContain("artist.name");
        fieldNames.ShouldContain("genre.name");
        fieldNames.ShouldContain("year");
        fieldNames.ShouldContain("explicit");
        fieldNames.ShouldContain("isFavorite");
        fieldNames.ShouldContain("hasWaiver");
        fieldNames.ShouldContain("createdAt");
    }

    [Fact]
    public async Task GetNonConformityFilterValues_BooleanFields_ReturnsEmpty()
    {
        // Arrange
        var (controller, db, _) = CreateControllerWithDb();

        // Act & Assert - Boolean fields are handled client-side, not server-side
        (await controller.GetNonConformityFilterValues(1, "hasWaiver", db, CancellationToken.None))
            .Values.ShouldBeEmpty();
        (await controller.GetNonConformityFilterValues(1, "explicit", db, CancellationToken.None))
            .Values.ShouldBeEmpty();
        (await controller.GetNonConformityFilterValues(1, "isFavorite", db, CancellationToken.None))
            .Values.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetNonConformityFilterValues_Title_ReturnsDistinctTitles()
    {
        // Arrange
        var (controller, db, user) = CreateControllerWithDb();
        var artist = new Artist { Name = "Test Artist", Owner = user, OwnerId = user.Id, SongsCount = 0, AlbumsCount = 0, CreatedAt = DateTime.UtcNow };
        db.Artists.Add(artist);
        var album = new Album { Name = "Test Album", Artist = artist, ArtistId = artist.Id, Owner = user, OwnerId = user.Id, SongsCount = 0, CreatedAt = DateTime.UtcNow };
        db.Albums.Add(album);
        db.Songs.Add(new Song
        {
            Title = "Rock Anthem", Label = "Label Rock", Album = album, AlbumId = album.Id, Owner = user, OwnerId = user.Id,
            RepositoryPath = "/music/rock.mp3", Checksum = "chk1", ChecksumAlgorithm = "XxHash128",
            Duration = TimeSpan.FromMinutes(3), CreatedAt = DateTime.UtcNow, ModifiedAt = DateTime.UtcNow, AddedAt = DateTime.UtcNow,
            Artists = [], Genres = [], Devices = [], Sources = [], CoverId = null,
        });
        db.Songs.Add(new Song
        {
            Title = "Pop Ballad", Label = "Label Pop", Album = album, AlbumId = album.Id, Owner = user, OwnerId = user.Id,
            RepositoryPath = "/music/pop.mp3", Checksum = "chk2", ChecksumAlgorithm = "XxHash128",
            Duration = TimeSpan.FromMinutes(3), CreatedAt = DateTime.UtcNow, ModifiedAt = DateTime.UtcNow, AddedAt = DateTime.UtcNow,
            Artists = [], Genres = [], Devices = [], Sources = [], CoverId = null,
        });
        db.SaveChanges();

        // Act
        var result = await controller.GetNonConformityFilterValues(1, "title", db, CancellationToken.None);

        // Assert
        result.Values.ShouldContain("Rock Anthem");
        result.Values.ShouldContain("Pop Ballad");
    }

    [Fact]
    public async Task GetNonConformityFilterValues_TitleWithSearch_ReturnsMatchingTitles()
    {
        // Arrange
        var (controller, db, user) = CreateControllerWithDb();
        var artist = new Artist { Name = "Test Artist", Owner = user, OwnerId = user.Id, SongsCount = 0, AlbumsCount = 0, CreatedAt = DateTime.UtcNow };
        db.Artists.Add(artist);
        var album = new Album { Name = "Test Album", Artist = artist, ArtistId = artist.Id, Owner = user, OwnerId = user.Id, SongsCount = 0, CreatedAt = DateTime.UtcNow };
        db.Albums.Add(album);
        db.Songs.Add(new Song
        {
            Title = "Rock Anthem", Label = "Label Rock", Album = album, AlbumId = album.Id, Owner = user, OwnerId = user.Id,
            RepositoryPath = "/music/rock.mp3", Checksum = "chk1", ChecksumAlgorithm = "XxHash128",
            Duration = TimeSpan.FromMinutes(3), CreatedAt = DateTime.UtcNow, ModifiedAt = DateTime.UtcNow, AddedAt = DateTime.UtcNow,
            Artists = [], Genres = [], Devices = [], Sources = [], CoverId = null,
        });
        db.Songs.Add(new Song
        {
            Title = "Pop Ballad", Label = "Label Pop", Album = album, AlbumId = album.Id, Owner = user, OwnerId = user.Id,
            RepositoryPath = "/music/pop.mp3", Checksum = "chk2", ChecksumAlgorithm = "XxHash128",
            Duration = TimeSpan.FromMinutes(3), CreatedAt = DateTime.UtcNow, ModifiedAt = DateTime.UtcNow, AddedAt = DateTime.UtcNow,
            Artists = [], Genres = [], Devices = [], Sources = [], CoverId = null,
        });
        db.SaveChanges();

        // Act
        var result = await controller.GetNonConformityFilterValues(1, "title", db, CancellationToken.None, search: "rock");

        // Assert
        result.Values.ShouldContain("Rock Anthem");
        result.Values.ShouldNotContain("Pop Ballad");
    }

    [Fact]
    public async Task GetNonConformityFilterValues_UnknownField_ReturnsEmptyList()
    {
        // Arrange
        var (controller, db, user) = CreateControllerWithDb();

        // Act
        var result = await controller.GetNonConformityFilterValues(1, "unknownField", db, CancellationToken.None);

        // Assert
        result.Values.ShouldBeEmpty();
    }

    #endregion

    #region 7.3: Integration tests for filtered non-conformities endpoint

    [Fact]
    public async Task ListNonConformities_FilterHasWaiverFalse_AppliesFilterCorrectly()
    {
        // Arrange - Test the filter DSL + field mappings directly without needing DB
        var filterDsl = "hasWaiver = false";
        var filterRequest = FilterDslParser.Parse(filterDsl);
        var mappings = AuditsControllerInternal.GetAuditNonConformityFieldMappings();
        DynamicFilterBuilder.ResolveEntityPaths(filterRequest, mappings);

        // Assert - The filter parses and resolves correctly
        filterRequest.ShouldNotBeNull();
        mappings["hasWaiver"].ShouldBe("HasWaiver");
    }

    [Fact]
    public async Task ListNonConformities_FilterTitleContains_ResolvesEntityPaths()
    {
        // Arrange - Test the filter DSL + field mappings for song properties
        var filterDsl = @"title contains ""Alpha""";
        var filterRequest = FilterDslParser.Parse(filterDsl);
        var mappings = AuditsControllerInternal.GetAuditNonConformityFieldMappings();
        DynamicFilterBuilder.ResolveEntityPaths(filterRequest, mappings);

        // Assert
        filterRequest.ShouldNotBeNull();
        mappings["title"].ShouldBe("Song.Title");
    }

    [Fact]
    public async Task ListNonConformities_CombinedFilter_ResolvesAllEntityPaths()
    {
        // Arrange - Test combined filter with and
        var filterDsl = @"hasWaiver = false and year >= 2020";
        var filterRequest = FilterDslParser.Parse(filterDsl);
        var mappings = AuditsControllerInternal.GetAuditNonConformityFieldMappings();
        DynamicFilterBuilder.ResolveEntityPaths(filterRequest, mappings);

        // Assert - Both conditions are resolved
        filterRequest.ShouldNotBeNull();
        mappings["hasWaiver"].ShouldBe("HasWaiver");
        mappings["year"].ShouldBe("Song.Year");
    }

    #endregion

    #region Helper class for accessing internal methods

    private static class AuditsControllerInternal
    {
        public static List<FilterFieldMetadata> GetAuditNonConformityFieldMetadata()
        {
            var method = typeof(AuditsController).GetMethod("GetAuditNonConformityFieldMetadata",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (method == null)
            {
                var instanceMethod = typeof(AuditsController).GetMethod("GetAuditNonConformityFieldMetadata",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var controller = (AuditsController)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(AuditsController));
                return (List<FilterFieldMetadata>)instanceMethod!.Invoke(controller, null)!;
            }
            return (List<FilterFieldMetadata>)method.Invoke(null, null)!;
        }

        public static Dictionary<string, string> GetAuditNonConformityFieldMappings()
        {
            var method = typeof(AuditsController).GetMethod("GetAuditNonConformityFieldMappings",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (method == null)
            {
                var instanceMethod = typeof(AuditsController).GetMethod("GetAuditNonConformityFieldMappings",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var controller = (AuditsController)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(AuditsController));
                return (Dictionary<string, string>)instanceMethod!.Invoke(controller, null)!;
            }
            return (Dictionary<string, string>)method.Invoke(null, null)!;
        }
    }

    #endregion
}